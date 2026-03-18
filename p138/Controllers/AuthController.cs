using System;
using System.Threading.Tasks;
using DiabetesPatientApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DiabetesPatientApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ISystemSettingsService _systemSettings;

        public AuthController(IAuthService authService, ISystemSettingsService systemSettings)
        {
            _authService = authService;
            _systemSettings = systemSettings;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetInt32("UserId") is int userId && userId > 0)
            {
                return RedirectToHomeByRole(HttpContext.Session.GetString("UserType"));
            }

            var reason = (Request.Query["reason"].ToString() ?? "").Trim();
            if (string.Equals(reason, "disabled", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = "你的账号已被停用，已强制下线。请联系管理员处理。";
            }
            else if (string.Equals(reason, "maintenance", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Error = "系统维护中，已强制下线。请稍后重试或联系管理员。";
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string userrole)
        {
            try
            {
                var selectedRole = NormalizeRole(userrole);
                if (selectedRole == null)
                {
                    ViewBag.Error = "请选择正确的用户角色";
                    return View();
                }

                var user = await _authService.LoginAsync(username, password);

                if (!IsAllowedRole(selectedRole, user.UserType))
                {
                    ViewBag.Error = "所选角色与账户类型不匹配";
                    return View();
                }

                // 维护模式：发布维护公告后，仅允许管理员登录
                if (!string.Equals(user.UserType, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    var announcement = await _systemSettings.GetStringAsync(SystemSettingKeys.MaintenanceAnnouncement, "");
                    if (!string.IsNullOrWhiteSpace(announcement))
                    {
                        ViewBag.Error = "系统维护中，暂不开放登录，请稍后重试。";
                        return View();
                    }
                }

                HttpContext.Session.SetInt32("UserId", user.UserId);
                HttpContext.Session.SetString("Username", user.Username ?? string.Empty);
                HttpContext.Session.SetString("FullName", user.FullName ?? user.Username ?? string.Empty);
                HttpContext.Session.SetString("UserType", user.UserType ?? string.Empty);
                HttpContext.Session.SetString("SelectedRole", selectedRole);

                return RedirectToHomeByRole(user.UserType);
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var allow = await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowRegistration, defaultValue: true);
            if (!allow)
            {
                ViewBag.Error = "系统已关闭自助注册，请联系管理员创建账号。";
                return View("Login");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string password, string confirmPassword, string userrole)
        {
            var allow = await _systemSettings.GetBoolAsync(SystemSettingKeys.AllowRegistration, defaultValue: true);
            if (!allow)
            {
                ViewBag.Error = "系统已关闭自助注册，请联系管理员创建账号。";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "两次输入的密码不一致";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                ViewBag.Error = "密码至少需要 6 个字符";
                return View();
            }

            try
            {
                var userType = MapUserType(userrole);
                if (userType == null)
                {
                    ViewBag.Error = "请选择正确的用户角色";
                    return View();
                }

                await _authService.RegisterAsync(username, null, password, username, userType);
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        private IActionResult RedirectToHomeByRole(string? userType)
        {
            if (string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Dashboard");
            }

            if (string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Doctor");
            }

            if (string.Equals(userType, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Admin");
            }

            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        private static string? MapUserType(string? userrole)
        {
            return NormalizeRole(userrole) switch
            {
                "patient" => "Patient",
                "doctor" => "Doctor",
                "admin" => "Admin",
                _ => null
            };
        }

        private static bool IsAllowedRole(string? selectedRole, string? actualUserType)
        {
            var normalizedRole = NormalizeRole(selectedRole);
            if (normalizedRole == null || string.IsNullOrWhiteSpace(actualUserType))
            {
                return false;
            }

            return normalizedRole switch
            {
                "patient" => string.Equals(actualUserType, "Patient", StringComparison.OrdinalIgnoreCase),
                "doctor" => string.Equals(actualUserType, "Doctor", StringComparison.OrdinalIgnoreCase),
                "admin" => string.Equals(actualUserType, "Admin", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        private static string? NormalizeRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return null;
            }

            return role.Trim().ToLowerInvariant() switch
            {
                "patient" => "patient",
                "doctor" => "doctor",
                "admin" => "admin",
                _ => null
            };
        }
    }
}
