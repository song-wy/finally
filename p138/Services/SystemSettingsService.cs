using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;
using Microsoft.EntityFrameworkCore;

namespace DiabetesPatientApp.Services
{
    public static class SystemSettingKeys
    {
        public const string SystemName = "SystemName";
        public const string MaintenanceAnnouncement = "MaintenanceAnnouncement";
        public const string AllowRegistration = "AllowRegistration";
        public const string MaxUploadMB = "MaxUploadMB";
    }

    public interface ISystemSettingsService
    {
        Task<Dictionary<string, string>> GetAllAsync();
        Task<string> GetStringAsync(string key, string defaultValue = "");
        Task<bool> GetBoolAsync(string key, bool defaultValue = false);
        Task<int> GetIntAsync(string key, int defaultValue = 0);
        Task UpsertAsync(string key, string value);
        Task UpsertManyAsync(Dictionary<string, string> values);
    }

    public class SystemSettingsService : ISystemSettingsService
    {
        private readonly DiabetesDbContext _context;

        public SystemSettingsService(DiabetesDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<string, string>> GetAllAsync()
        {
            var items = await _context.SystemSettings
                .AsNoTracking()
                .ToListAsync();
            return items
                .GroupBy(x => x.Key)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First().Value);
        }

        public async Task<string> GetStringAsync(string key, string defaultValue = "")
        {
            var v = await _context.SystemSettings
                .AsNoTracking()
                .Where(x => x.Key == key)
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x => x.Value)
                .FirstOrDefaultAsync();
            return string.IsNullOrWhiteSpace(v) ? defaultValue : v;
        }

        public async Task<bool> GetBoolAsync(string key, bool defaultValue = false)
        {
            var v = await GetStringAsync(key, defaultValue ? "true" : "false");
            return v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   v.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   v.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<int> GetIntAsync(string key, int defaultValue = 0)
        {
            var v = await GetStringAsync(key, defaultValue.ToString());
            return int.TryParse(v.Trim(), out var n) ? n : defaultValue;
        }

        public async Task UpsertAsync(string key, string value)
        {
            key = (key ?? "").Trim();
            if (string.IsNullOrWhiteSpace(key)) return;
            value ??= "";

            var existing = await _context.SystemSettings.FirstOrDefaultAsync(x => x.Key == key);
            if (existing == null)
            {
                _context.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAt = DateTime.Now });
            }
            else
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.Now;
                _context.SystemSettings.Update(existing);
            }
            await _context.SaveChangesAsync();
        }

        public async Task UpsertManyAsync(Dictionary<string, string> values)
        {
            values ??= new Dictionary<string, string>();
            foreach (var kv in values)
            {
                await UpsertAsync(kv.Key, kv.Value);
            }
        }
    }
}

