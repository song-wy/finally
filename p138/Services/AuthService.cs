using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;
using Microsoft.Data.SqlClient;

namespace DiabetesPatientApp.Services
{
    public interface IAuthService
    {
        Task<User> RegisterAsync(string username, string? email, string password, string fullName, string userType);
        Task<User> LoginAsync(string username, string password);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hash);
    }

    public class AuthService : IAuthService
    {
        private readonly DiabetesDbContext _context;
        private readonly string? _legacyConnectionString;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            DiabetesDbContext context,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _context = context;
            _legacyConnectionString = configuration.GetConnectionString("LegacySqlServerConnection");
            _logger = logger;
        }

        public async Task<User> RegisterAsync(string username, string? email, string password, string fullName, string userType)
        {
            var normalizedUsername = NormalizeValue(username);
            if (string.IsNullOrWhiteSpace(normalizedUsername))
                throw new Exception("用户名不能为空");

            if (await _context.Users.AnyAsync(u => u.Username != null && u.Username.ToLower() == normalizedUsername.ToLower()))
                throw new Exception("用户名已存在");

            var emailToUse = string.IsNullOrWhiteSpace(email) ? $"{normalizedUsername}@noreply.local" : email.Trim();
            if (await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == emailToUse.ToLower()))
                throw new Exception("邮箱已存在");

            var user = new User
            {
                Username = normalizedUsername,
                Email = emailToUse,
                PasswordHash = HashPassword(password),
                FullName = string.IsNullOrWhiteSpace(fullName) ? normalizedUsername : fullName.Trim(),
                UserType = string.IsNullOrWhiteSpace(userType) ? "Patient" : userType.Trim(),
                CreatedDate = DateTime.Now,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User> LoginAsync(string username, string password)
        {
            var normalizedUsername = NormalizeValue(username);
            var normalizedPassword = password?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(normalizedPassword))
                throw new Exception("用户名或密码错误");

            var loweredUsername = normalizedUsername.ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == loweredUsername);

            if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !VerifyPassword(normalizedPassword, user.PasswordHash))
            {
                user = await TryRestoreLegacyUserAsync(normalizedUsername, normalizedPassword, user);
            }

            if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !VerifyPassword(normalizedPassword, user.PasswordHash))
                throw new Exception("用户名或密码错误");

            if (!user.IsActive)
                throw new Exception("该账号已被停用，请联系管理员。");

            user.LastLoginDate = DateTime.Now;
            await _context.SaveChangesAsync();
            return user;
        }

        public string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public bool VerifyPassword(string password, string hash)
        {
            var hashOfInput = HashPassword(password);
            return hashOfInput == hash;
        }

        private async Task<User?> TryRestoreLegacyUserAsync(string username, string password, User? localUser)
        {
            if (string.IsNullOrWhiteSpace(_legacyConnectionString))
            {
                return localUser;
            }

            try
            {
                await using var connection = new SqlConnection(_legacyConnectionString);
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT TOP 1 UserId, Username, Email, PasswordHash, FullName, PhoneNumber, DateOfBirth, Gender, UserType, CreatedDate, LastLoginDate, IsActive
FROM Users
WHERE LOWER(LTRIM(RTRIM(Username))) = @username";
                command.Parameters.AddWithValue("@username", username.ToLower());

                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return localUser;
                }

                var legacyPasswordHash = GetNullableString(reader, "PasswordHash");
                if (string.IsNullOrWhiteSpace(legacyPasswordHash) || !VerifyPassword(password, legacyPasswordHash))
                {
                    return localUser;
                }

                var legacyUsername = NormalizeValue(GetNullableString(reader, "Username")) ?? username;
                var legacyEmail = NormalizeValue(GetNullableString(reader, "Email"));
                var emailToUse = await ResolveAvailableEmailAsync(legacyEmail, legacyUsername, localUser?.UserId);

                if (localUser == null)
                {
                    localUser = new User
                    {
                        Username = legacyUsername,
                        CreatedDate = GetNullableDateTime(reader, "CreatedDate") ?? DateTime.Now
                    };
                    _context.Users.Add(localUser);
                }

                localUser.Email = emailToUse;
                localUser.PasswordHash = legacyPasswordHash;
                localUser.FullName = NormalizeValue(GetNullableString(reader, "FullName")) ?? legacyUsername;
                localUser.PhoneNumber = NormalizeValue(GetNullableString(reader, "PhoneNumber"));
                localUser.DateOfBirth = GetNullableDateTime(reader, "DateOfBirth");
                localUser.Gender = NormalizeValue(GetNullableString(reader, "Gender"));
                localUser.UserType = NormalizeValue(GetNullableString(reader, "UserType")) ?? "Patient";
                localUser.LastLoginDate = GetNullableDateTime(reader, "LastLoginDate");
                localUser.IsActive = GetNullableBool(reader, "IsActive") ?? true;

                await _context.SaveChangesAsync();
                return localUser;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "从旧 SQL Server 同步用户失败：{Username}", username);
                return localUser;
            }
        }

        private async Task<string> ResolveAvailableEmailAsync(string? legacyEmail, string username, int? currentUserId)
        {
            var candidate = string.IsNullOrWhiteSpace(legacyEmail)
                ? $"{username}@legacy.local"
                : legacyEmail;

            var loweredCandidate = candidate.ToLower();
            var hasConflict = await _context.Users.AnyAsync(u =>
                u.Email != null &&
                u.Email.ToLower() == loweredCandidate &&
                (!currentUserId.HasValue || u.UserId != currentUserId.Value));

            return hasConflict ? $"{username}@legacy.local" : candidate;
        }

        private static string? NormalizeValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? GetNullableString(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static DateTime? GetNullableDateTime(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }

        private static bool? GetNullableBool(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
        }
    }
}

