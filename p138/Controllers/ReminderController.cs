using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;
using DiabetesPatientApp.Services;

namespace DiabetesPatientApp.Controllers
{
    public class ReminderController : Controller
    {
        private readonly IReminderService _reminderService;
        private readonly IFootPressureService _footPressureService;
        private readonly IWoundService _woundService;
        private readonly IConsultationService _consultationService;
        private readonly DiabetesDbContext _context;

        public ReminderController(
            IReminderService reminderService,
            IFootPressureService footPressureService,
            IWoundService woundService,
            IConsultationService consultationService,
            DiabetesDbContext context)
        {
            _reminderService = reminderService;
            _footPressureService = footPressureService;
            _woundService = woundService;
            _consultationService = consultationService;
            _context = context;
        }

        private int GetUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        // 提醒列表
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var reminders = await _reminderService.GetUserRemindersAsync(userId);
            var bloodSugarCompletion = await _reminderService.GetTodayBloodSugarCompletionAsync(userId);
            ViewBag.BloodSugarCompletion = bloodSugarCompletion;

            var footPressureStatus = await GetFootPressureReminderStatusAsync(userId);
            ViewBag.FootPressureCompleted = footPressureStatus.Completed;
            ViewBag.FootPressureStatusCode = footPressureStatus.StatusCode;
            ViewBag.FootPressureStatusText = footPressureStatus.StatusText;
            ViewBag.FootPressureIncompleteCount = footPressureStatus.IncompleteCount;

            // 伤口监护：本周（以周一为起始）是否已上传伤口照片
            var (woundReminderCompleted, woundReminderStatusCode, woundReminderStatusText, _, _) =
                await GetWoundReminderStatusAsync(userId);
            ViewBag.WoundReminderCompleted = woundReminderCompleted;
            ViewBag.WoundReminderStatusCode = woundReminderStatusCode;
            ViewBag.WoundReminderStatusText = woundReminderStatusText;

            // 聊天记录提醒：医生回复的未读消息条数
            var chatUnread = await _consultationService.GetUnreadMessagesAsync(userId);
            int chatReminderUnreadCount = chatUnread.Count(m => m.Sender != null && (m.Sender.UserType == "Doctor" || m.Sender.UserType == "Nurse"));
            ViewBag.ChatReminderUnreadCount = chatReminderUnreadCount;

            return View(reminders);
        }

        // 新增提醒
        public async Task<IActionResult> Create()
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var reminders = await _reminderService.GetUserRemindersAsync(userId);
            var bloodSugarCompletion = await _reminderService.GetTodayBloodSugarCompletionAsync(userId);
            ViewBag.BloodSugarCompletion = bloodSugarCompletion;
            return View(reminders);
        }

        // 伤口监护提醒内容：系统默认每周一上传一次伤口照片，未上传则提示；每周自动统计
        [HttpGet]
        public async Task<IActionResult> WoundReminder()
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var (hasUploadedThisWeek, woundReminderStatusCode, woundReminderStatusText, thisWeekMonday, reminderStartTime) =
                await GetWoundReminderStatusAsync(userId);
            var nextMonday = thisWeekMonday.AddDays(7);

            // 近 4 周统计：每周是否已上传
            var weeklyStats = new List<WoundWeekStat>();
            for (int i = 0; i < 4; i++)
            {
                var weekStart = thisWeekMonday.AddDays(-7 * i);
                var weekEnd = weekStart.AddDays(7).AddDays(-1);
                var records = await _woundService.GetRecordsByDateRangeAsync(userId, weekStart, weekEnd);
                bool uploaded = records != null && records.Count > 0;
                string label = $"{weekStart:M/d} - {weekEnd:M/d}";
                weeklyStats.Add(new WoundWeekStat { Label = label, Uploaded = uploaded });
            }

            ViewBag.HasUploadedThisWeek = hasUploadedThisWeek;
            ViewBag.ThisWeekMonday = thisWeekMonday;
            ViewBag.WoundReminderStatusCode = woundReminderStatusCode;
            ViewBag.WoundReminderStatusText = woundReminderStatusText;
            ViewBag.WoundReminderStartTime = reminderStartTime;
            ViewBag.WeeklyStats = weeklyStats;
            ViewBag.WoundCreateUrl = Url.Action("Create", "Wound");

            return View();
        }

        // 足压监测提醒内容
        [HttpGet]
        public async Task<IActionResult> FootPressureReminder()
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var footPressureStatus = await GetFootPressureReminderStatusAsync(userId);
            ViewBag.FootPressureReminderStatusText = footPressureStatus.StatusText;
            ViewBag.FootPressureReminderStatusCode = footPressureStatus.StatusCode;
            ViewBag.FootPressureIncompleteCount = footPressureStatus.IncompleteCount;
            ViewBag.FootPressureThisWeekMonday = footPressureStatus.ThisWeekMonday;
            ViewBag.FootPressureSlots = footPressureStatus.Slots;
            ViewBag.FootPressureCreateUrl = Url.Action("Create", "FootPressure");

            return View(footPressureStatus.Slots);
        }

        // 聊天记录提醒内容
        [HttpGet]
        public async Task<IActionResult> ChatReminder()
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var unread = await _consultationService.GetUnreadMessagesAsync(userId);
            var fromDoctors = unread
                .Where(m => m.Sender != null && (m.Sender.UserType == "Doctor" || m.Sender.UserType == "Nurse"))
                .GroupBy(m => m.Sender!.UserId)
                .Select(g => new ChatReminderItem
                {
                    DoctorId = g.Key,
                    DoctorName = g.First().Sender!.Username ?? "医生",
                    UnreadCount = g.Count()
                })
                .OrderByDescending(x => x.UnreadCount)
                .ToList();

            var doctorIds = fromDoctors.Select(x => x.DoctorId).ToList();
            if (doctorIds.Count > 0)
            {
                var latestFollowUps = await _context.FollowUpRecords
                    .AsNoTracking()
                    .Where(f => f.PatientId == userId && doctorIds.Contains(f.DoctorId))
                    .OrderByDescending(f => f.FollowUpDate)
                    .ThenByDescending(f => f.CreatedDate)
                    .ToListAsync();

                var latestFollowUpByDoctor = latestFollowUps
                    .GroupBy(f => f.DoctorId)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var item in fromDoctors)
                {
                    if (latestFollowUpByDoctor.TryGetValue(item.DoctorId, out var followUp))
                    {
                        item.FollowUpDate = followUp.FollowUpDate;
                        item.FollowUpSummary = followUp.Summary;
                        item.FollowUpAdvice = followUp.Advice ?? string.Empty;
                    }
                }
            }

            return View(fromDoctors);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string mealType, string reminderTime)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            try
            {
                if (!TimeSpan.TryParse(reminderTime, out var time))
                {
                    ModelState.AddModelError("", "时间格式错误");
                    return View();
                }

                await _reminderService.CreateReminderAsync(userId, mealType, time);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"创建失败: {ex.Message}");
                return View();
            }
        }

        // 编辑提醒
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var reminders = await _reminderService.GetUserRemindersAsync(userId);
            var reminder = reminders.FirstOrDefault(r => r.ReminderId == id);
            
            if (reminder == null)
                return NotFound();

            return View(reminder);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, string reminderTime, bool isActive)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var reminders = await _reminderService.GetUserRemindersAsync(userId);
            var reminder = reminders.FirstOrDefault(r => r.ReminderId == id);
            if (reminder == null)
                return NotFound();

            try
            {
                if (!TimeSpan.TryParse(reminderTime, out var time))
                {
                    ModelState.AddModelError("", "时间格式错误");
                    return View(reminder);
                }

                if (!IsTimeWithinPresetRange(reminder.MealType, time))
                {
                    ModelState.AddModelError("", "请选择该提醒对应时间段内的时间");
                    return View(reminder);
                }

                await _reminderService.UpdateReminderAsync(id, time, isActive);
                return RedirectToAction("Create");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"更新失败: {ex.Message}");
                return View(reminder);
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpsertPreset(string mealType, string reminderTime)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            if (!TimeSpan.TryParse(reminderTime, out var time))
                return RedirectToAction("Create");

            var reminders = await _reminderService.GetUserRemindersAsync(userId);
            var existing = reminders.FirstOrDefault(r => string.Equals(r.MealType, mealType, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                await _reminderService.CreateReminderAsync(userId, mealType, time);
            }
            else
            {
                // 已存在则重置为系统默认时间，并启用
                await _reminderService.UpdateReminderAsync(existing.ReminderId, time, true);
            }
            return RedirectToAction("Create");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var reminders = await _reminderService.GetUserRemindersAsync(userId);
            var reminder = reminders.FirstOrDefault(r => r.ReminderId == id);
            if (reminder == null)
                return RedirectToAction("Create");

            await _reminderService.UpdateReminderAsync(id, reminder.ReminderTime, !reminder.IsActive);
            return RedirectToAction("Create");
        }

        // 删除提醒
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            try
            {
                await _reminderService.DeleteReminderAsync(id);
                return RedirectToAction("Create");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"删除失败: {ex.Message}");
                return RedirectToAction("Create");
            }
        }

        // 获取今天的提醒状态
        public async Task<IActionResult> GetTodayStatus()
        {
            var userId = GetUserId();
            if (userId == 0)
                return Json(new { success = false, message = "未登录" });

            try
            {
                var todayRecords = await _reminderService.GetTodayRecordsAsync(userId);
                var reminders = await _reminderService.GetActiveRemindersAsync(userId);

                var status = new
                {
                    success = true,
                    reminders = reminders.Select(r => new
                    {
                        id = r.ReminderId,
                        mealType = r.MealType,
                        reminderTime = r.ReminderTime.ToString(@"hh\:mm"),
                        isRecorded = todayRecords.Any(tr => tr.MealType == NormalizeMealTypeForRecord(r.MealType))
                    }).ToList(),
                    todayRecords = todayRecords.Count,
                    totalReminders = reminders.Count
                };

                return Json(status);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private static string NormalizeMealTypeForRecord(string? mealType)
        {
            if (string.IsNullOrWhiteSpace(mealType)) return string.Empty;
            mealType = mealType.Trim();
            if (mealType.StartsWith("AfterMeal", StringComparison.OrdinalIgnoreCase))
                return "AfterMeal";
            return mealType;
        }

        private static bool IsTimeWithinPresetRange(string? mealType, TimeSpan time)
        {
            // 允许边界值
            var (start, end) = mealType switch
            {
                "Fasting" => (TimeSpan.FromHours(6), TimeSpan.FromHours(8)),
                "AfterMeal1" => (TimeSpan.FromHours(10), TimeSpan.FromHours(11)),
                "AfterMeal2" => (TimeSpan.FromHours(13), TimeSpan.FromHours(14)),
                "AfterMeal3" => (TimeSpan.FromHours(20), TimeSpan.FromHours(22)),
                _ => (TimeSpan.Zero, TimeSpan.FromHours(24))
            };
            return time >= start && time <= end;
        }

        private async Task<(bool Completed, string StatusCode, string StatusText, DateTime ThisWeekMonday, DateTime ReminderStartTime)> GetWoundReminderStatusAsync(int userId)
        {
            var now = GetBeijingNow();
            var today = now.Date;
            int daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var thisWeekMonday = today.AddDays(-daysSinceMonday);
            var nextMonday = thisWeekMonday.AddDays(7);
            var reminderStartTime = thisWeekMonday.AddHours(12);
            var reminderEndTime = thisWeekMonday.AddDays(1);

            var thisWeekRecords = await _woundService.GetRecordsByDateRangeAsync(userId, thisWeekMonday, nextMonday.AddDays(-1));
            bool completed = thisWeekRecords != null && thisWeekRecords.Count > 0;

            var statusCode = "Upcoming";
            var statusText = "未开始";

            if (completed)
            {
                statusCode = "Completed";
                statusText = "已完成";
            }
            else if (now >= reminderEndTime)
            {
                statusCode = "Incomplete";
                statusText = "未完成";
            }
            else if (now >= reminderStartTime)
            {
                statusCode = "Pending";
                statusText = "提醒中";
            }

            return (completed, statusCode, statusText, thisWeekMonday, reminderStartTime);
        }

        private async Task<(bool Completed, string StatusCode, string StatusText, int IncompleteCount, DateTime ThisWeekMonday, List<FootPressureReminderSlotStatus> Slots)> GetFootPressureReminderStatusAsync(int userId)
        {
            var now = GetBeijingNow();
            var today = now.Date;
            int daysSinceMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var thisWeekMonday = today.AddDays(-daysSinceMonday);
            var mondayRecords = await _footPressureService.GetRecordsByDateRangeAsync(userId, thisWeekMonday, thisWeekMonday);

            var slots = new List<FootPressureReminderSlotStatus>
            {
                BuildFootPressureSlotStatus(
                    label: "第一次足压监测提醒",
                    range: "周一 12:00 - 19:00",
                    slotStart: thisWeekMonday.AddHours(12),
                    slotEnd: thisWeekMonday.AddHours(19),
                    now: now,
                    completed: mondayRecords.Any(r => r.RecordDate.Date == thisWeekMonday && r.RecordTime >= TimeSpan.FromHours(12) && r.RecordTime < TimeSpan.FromHours(19))),
                BuildFootPressureSlotStatus(
                    label: "第二次足压监测提醒",
                    range: "周一 19:00 - 24:00",
                    slotStart: thisWeekMonday.AddHours(19),
                    slotEnd: thisWeekMonday.AddDays(1),
                    now: now,
                    completed: mondayRecords.Any(r => r.RecordDate.Date == thisWeekMonday && r.RecordTime >= TimeSpan.FromHours(19)))
            };

            var incompleteCount = slots.Count(s => string.Equals(s.StatusCode, "Incomplete", StringComparison.OrdinalIgnoreCase));
            var pendingCount = slots.Count(s => string.Equals(s.StatusCode, "Pending", StringComparison.OrdinalIgnoreCase));
            var completed = slots.All(s => s.Completed);

            var statusCode = "Upcoming";
            var statusText = "未开始";

            if (completed)
            {
                statusCode = "Completed";
                statusText = "已完成";
            }
            else if (incompleteCount > 0)
            {
                statusCode = "Incomplete";
                statusText = "未完成";
            }
            else if (pendingCount > 0)
            {
                statusCode = "Pending";
                statusText = "待完成";
            }

            return (completed, statusCode, statusText, incompleteCount, thisWeekMonday, slots);
        }

        private static FootPressureReminderSlotStatus BuildFootPressureSlotStatus(string label, string range, DateTime slotStart, DateTime slotEnd, DateTime now, bool completed)
        {
            var statusCode = "Upcoming";
            var statusText = "未开始";

            if (completed)
            {
                statusCode = "Completed";
                statusText = "已完成";
            }
            else if (now >= slotEnd)
            {
                statusCode = "Incomplete";
                statusText = "未完成";
            }
            else if (now >= slotStart)
            {
                statusCode = "Pending";
                statusText = "待完成";
            }

            return new FootPressureReminderSlotStatus
            {
                Label = label,
                Range = range,
                Completed = completed,
                StatusCode = statusCode,
                StatusText = statusText
            };
        }

        private static DateTime GetBeijingNow()
        {
            var utcNow = DateTime.UtcNow;
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, GetBeijingTimeZone());
        }

        private static TimeZoneInfo GetBeijingTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            }
        }

        // 检查是否需要提醒
        public async Task<IActionResult> CheckReminders()
        {
            try
            {
                var dueReminders = await _reminderService.GetDueRemindersAsync();
                
                var remindersData = dueReminders.Select(r => new
                {
                    userId = r.UserId,
                    mealType = r.MealType,
                    reminderTime = r.ReminderTime.ToString(@"hh\:mm"),
                    userName = r.User?.Username
                }).ToList();

                return Json(new { success = true, reminders = remindersData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}

