using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.ViewModels;

namespace DiabetesPatientApp.Controllers
{
    /// <summary>
    /// 患者端预警触发：汇总血糖异常、足压高风险、伤口异常等
    /// </summary>
    public class AlertController : Controller
    {
        private readonly DiabetesDbContext _context;

        public AlertController(DiabetesDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        /// <summary>
        /// 预警触发列表页（最近30天内的触发记录）
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Login", "Auth");

            var endDate = DateTime.Today.AddDays(1);
            var startDate = DateTime.Today.AddDays(-30);

            // 血糖异常：高血糖或低血糖（仅按日期排序，避免 SQLite 对 TimeSpan 的 ORDER BY 不支持；时间排序在内存中完成）
            var bloodSugarAlerts = await _context.BloodSugarRecords
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.RecordDate >= startDate && r.RecordDate < endDate)
                .Where(r => r.Status == "High" || r.Status == "Low")
                .OrderByDescending(r => r.RecordDate)
                .ToListAsync();
            bloodSugarAlerts = bloodSugarAlerts.OrderByDescending(r => r.RecordDate).ThenByDescending(r => r.RecordTime).ToList();

            // 足压高风险或极高风险（左/右任一侧即计入）
            var footPressureAlerts = await _context.FootPressureRecords
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.RecordDate >= startDate && r.RecordDate < endDate)
                .Where(r =>
                    r.LeftFootStatus == "高风险" || r.LeftFootStatus == "极高风险" ||
                    r.RightFootStatus == "高风险" || r.RightFootStatus == "极高风险")
                .OrderByDescending(r => r.RecordDate)
                .ToListAsync();
            footPressureAlerts = footPressureAlerts.OrderByDescending(r => r.RecordDate).ThenByDescending(r => r.RecordTime).ToList();

            // 伤口异常：感染/渗出/发热/异味
            var woundAlerts = await _context.WoundRecords
                .AsNoTracking()
                .Where(r => r.UserId == userId && r.RecordDate >= startDate && r.RecordDate < endDate)
                .Where(r => r.HasInfection || r.HasDischarge || r.HasFever || r.HasOdor)
                .OrderByDescending(r => r.RecordDate)
                .ToListAsync();
            woundAlerts = woundAlerts.OrderByDescending(r => r.RecordDate).ThenByDescending(r => r.RecordTime).ToList();

            var vm = new AlertIndexViewModel
            {
                BloodSugarAlerts = bloodSugarAlerts,
                FootPressureAlerts = footPressureAlerts,
                WoundAlerts = woundAlerts,
                DateRangeText = $"{startDate:yyyy-MM-dd} 至 {endDate.AddDays(-1):yyyy-MM-dd}"
            };

            return View(vm);
        }
    }
}
