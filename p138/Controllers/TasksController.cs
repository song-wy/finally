using System;
using System.Linq;
using System.Threading.Tasks;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiabetesPatientApp.Controllers
{
    public class TasksController : Controller
    {
        private readonly DiabetesDbContext _context;

        public TasksController(DiabetesDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => HttpContext.Session.GetInt32("UserId") ?? 0;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Doctor");
            }

            var today = DateTime.Today;

            // 确保今日任务存在：基于有效医嘱自动生成（避免一次性生成大量任务）
            var activeOrders = await _context.DoctorOrders
                .AsNoTracking()
                .Where(o => o.PatientId == userId && o.IsActive)
                .Where(o => o.StartDate.Date <= today && (!o.EndDate.HasValue || o.EndDate.Value.Date >= today))
                .ToListAsync();

            if (activeOrders.Count > 0)
            {
                var orderIds = activeOrders.Select(o => o.DoctorOrderId).ToList();
                var existing = await _context.PatientDailyTasks
                    .AsNoTracking()
                    .Where(t => t.PatientId == userId && t.TaskDate == today && orderIds.Contains(t.DoctorOrderId))
                    .Select(t => t.DoctorOrderId)
                    .ToListAsync();

                var existingSet = existing.ToHashSet();
                var toAdd = activeOrders
                    .Where(o => !existingSet.Contains(o.DoctorOrderId))
                    .Select(o => new PatientDailyTask
                    {
                        PatientId = userId,
                        DoctorOrderId = o.DoctorOrderId,
                        TaskDate = today,
                        IsCompleted = false,
                        CompletedAt = null,
                        CreatedAt = DateTime.Now
                    })
                    .ToList();

                if (toAdd.Count > 0)
                {
                    await _context.PatientDailyTasks.AddRangeAsync(toAdd);
                    await _context.SaveChangesAsync();
                }
            }

            var tasks = await _context.PatientDailyTasks
                .AsNoTracking()
                .Join(_context.DoctorOrders.AsNoTracking(),
                    t => t.DoctorOrderId,
                    o => o.DoctorOrderId,
                    (t, o) => new { t, o })
                .Where(x => x.t.PatientId == userId && x.t.TaskDate == today)
                .OrderBy(x => x.o.Category)
                .ThenByDescending(x => x.o.CreatedAt)
                .Select(x => new
                {
                    x.t.PatientDailyTaskId,
                    x.o.Category,
                    x.o.Content,
                    x.t.IsCompleted,
                    x.t.CompletedAt
                })
                .ToListAsync();

            ViewBag.Today = today;
            return View(tasks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var task = await _context.PatientDailyTasks.FirstOrDefaultAsync(t => t.PatientDailyTaskId == id && t.PatientId == userId);
            if (task == null)
            {
                TempData["Error"] = "未找到任务。";
                return RedirectToAction(nameof(Index));
            }

            if (!task.IsCompleted)
            {
                task.IsCompleted = true;
                task.CompletedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = "已完成打卡。";
            return RedirectToAction(nameof(Index));
        }
    }
}

