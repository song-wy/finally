using System;
using System.Threading.Tasks;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace DiabetesPatientApp.Filters
{
    public class ActiveAccountFilter : IAsyncActionFilter
    {
        private readonly DiabetesDbContext _context;
        private readonly ISystemSettingsService _systemSettings;

        public ActiveAccountFilter(DiabetesDbContext context, ISystemSettingsService systemSettings)
        {
            _context = context;
            _systemSettings = systemSettings;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var http = context.HttpContext;
            var path = http.Request.Path.Value ?? "";

            // 登录/注册页不做拦截，避免死循环
            if (path.StartsWith("/Auth/Login", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Auth/Register", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/Auth/Logout", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            var userId = http.Session.GetInt32("UserId") ?? 0;
            if (userId <= 0)
            {
                await next();
                return;
            }

            // 维护模式：公告不为空时，患者/医生强制下线并禁止访问；管理员不受影响
            var userType = http.Session.GetString("UserType") ?? "";
            if (!string.Equals(userType, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                var announcement = await _systemSettings.GetStringAsync(SystemSettingKeys.MaintenanceAnnouncement, "");
                if (!string.IsNullOrWhiteSpace(announcement))
                {
                    http.Session.Clear();
                    context.Result = new RedirectToActionResult("Login", "Auth", new { reason = "maintenance" });
                    return;
                }
            }

            var isActive = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => u.IsActive)
                .FirstOrDefaultAsync();

            if (!isActive)
            {
                http.Session.Clear();
                context.Result = new RedirectToActionResult("Login", "Auth", new { reason = "disabled" });
                return;
            }

            await next();
        }
    }
}

