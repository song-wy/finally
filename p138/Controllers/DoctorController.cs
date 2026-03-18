using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;
using DiabetesPatientApp.Services;
using DiabetesPatientApp.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DiabetesPatientApp.Controllers
{
    public class DoctorController : Controller
    {
        private readonly DiabetesDbContext _context;
        private readonly IHighRiskAlertService _highRiskAlertService;

        public DoctorController(DiabetesDbContext context, IHighRiskAlertService highRiskAlertService)
        {
            _context = context;
            _highRiskAlertService = highRiskAlertService;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId == 0)
            {
                context.Result = RedirectToAction("Login", "Auth");
                return;
            }

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = RedirectToPortalByUserType(userType);
                return;
            }

            base.OnActionExecuting(context);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var today = DateTime.Today;
            var now = DateTime.Now;

            var recentPatients = await _context.Users
                .Where(u => u.UserType == "Patient")
                .OrderByDescending(u => u.CreatedDate)
                .Take(5)
                .Select(u => new DoctorPatientSummary
                {
                    UserId = u.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(u.FullName) ? (u.Username ?? "未命名患者") : u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    CreatedDate = u.CreatedDate,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            var unreadMessages = await _context.ConsultationMessages
                .Include(m => m.Sender)
                .Where(m => m.ReceiverId == doctorId && !m.IsRead && m.Sender != null && m.Sender.UserType == "Patient")
                .OrderByDescending(m => m.CreatedDate)
                .Take(5)
                .Select(m => new DoctorMessageSummary
                {
                    MessageId = m.MessageId,
                    SenderId = m.SenderId,
                    SenderName = string.IsNullOrWhiteSpace(m.Sender!.FullName) ? (m.Sender.Username ?? "未知患者") : m.Sender.FullName!,
                    Preview = string.IsNullOrWhiteSpace(m.MessageContent) ? $"[{m.MessageType ?? "消息"}]" : m.MessageContent!,
                    CreatedDate = m.CreatedDate
                })
                .ToListAsync();

            var consultationMessages = await _context.ConsultationMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m =>
                    (m.SenderId == doctorId && m.Receiver != null && m.Receiver.UserType == "Patient") ||
                    (m.ReceiverId == doctorId && m.Sender != null && m.Sender.UserType == "Patient"))
                .OrderByDescending(m => m.CreatedDate)
                .ToListAsync();

            var consultationPatients = consultationMessages
                .Select(m =>
                {
                    var patient = m.SenderId == doctorId ? m.Receiver : m.Sender;
                    return new
                    {
                        PatientId = patient?.UserId ?? 0,
                        PatientName = string.IsNullOrWhiteSpace(patient?.FullName) ? (patient?.Username ?? "未知患者") : patient.FullName!,
                        patient?.PhoneNumber,
                        LastMessageTime = m.CreatedDate,
                        LastMessagePreview = string.IsNullOrWhiteSpace(m.MessageContent) ? $"[{m.MessageType ?? "消息"}]" : m.MessageContent!,
                        IsUnread = m.ReceiverId == doctorId && !m.IsRead,
                        IsPatientMessage = m.ReceiverId == doctorId
                    };
                })
                .Where(x => x.PatientId > 0)
                .GroupBy(x => new { x.PatientId, x.PatientName, x.PhoneNumber })
                .Select(g =>
                {
                    var latest = g.OrderByDescending(x => x.LastMessageTime).First();
                    return new DoctorConsultationPatientSummary
                    {
                        PatientId = g.Key.PatientId,
                        PatientName = g.Key.PatientName,
                        PhoneNumber = g.Key.PhoneNumber,
                        LastMessageTime = latest.LastMessageTime,
                        LastMessagePreview = latest.LastMessagePreview,
                        UnreadCount = g.Count(x => x.IsUnread),
                        ShowUnreadCount = latest.IsPatientMessage && latest.IsUnread
                    };
                })
                .OrderByDescending(x => x.LastMessageTime)
                .Take(8)
                .ToList();

            var pendingReplies = consultationPatients
                .Where(x => x.UnreadCount > 0)
                .Select(x => new DoctorPendingReplySummary
                {
                    PatientId = x.PatientId,
                    PatientName = x.PatientName,
                    LatestMessagePreview = x.LastMessagePreview,
                    LatestMessageTime = x.LastMessageTime,
                    UnreadCount = x.UnreadCount
                })
                .OrderByDescending(x => x.LatestMessageTime)
                .Take(5)
                .ToList();

            var followUpNotifications = await BuildDueFollowUpNotificationsAsync(doctorId, now);
            var notifications = pendingReplies
                .Select(x => new DoctorNotificationSummary
                {
                    NotificationType = "Consultation",
                    PatientId = x.PatientId,
                    PatientName = x.PatientName,
                    Title = "患者咨询消息",
                    Content = x.LatestMessagePreview,
                    NotificationTime = x.LatestMessageTime,
                    UnreadCount = x.UnreadCount,
                    ShowUnreadCount = x.ShowUnreadCount
                })
                .Concat(followUpNotifications)
                .OrderByDescending(x => x.NotificationTime)
                .Take(6)
                .ToList();

            var model = new DoctorDashboardViewModel
            {
                DoctorName = HttpContext.Session.GetString("FullName") ?? HttpContext.Session.GetString("Username") ?? "医生",
                PatientCount = await _context.Users.CountAsync(u => u.UserType == "Patient"),
                UnreadMessageCount = await _context.ConsultationMessages.CountAsync(m => m.ReceiverId == doctorId && !m.IsRead),
                NotificationCount = notifications.Count,
                TodayConsultationCount = await _context.ConsultationMessages.CountAsync(m =>
                    (m.SenderId == doctorId || m.ReceiverId == doctorId) && m.CreatedDate >= today),
                ActiveReminderCount = await _context.Reminders.CountAsync(r => r.IsActive),
                RecentPatients = recentPatients,
                RecentUnreadMessages = unreadMessages,
                ConsultationPatients = consultationPatients,
                PendingReplies = pendingReplies,
                Notifications = notifications
            };

            // 首页：高危患者预警（按近30天内“血糖异常/足压高风险/伤口异常”汇总，取前10）
            var endDate = DateTime.Today.AddDays(1);
            var startDate = DateTime.Today.AddDays(-30);

            // 趋势分析：近14天异常趋势（按天）
            var trendStart = DateTime.Today.AddDays(-13);
            var trendEnd = DateTime.Today.AddDays(1);

            var bsTrend = await _context.BloodSugarRecords
                .AsNoTracking()
                .Where(r => r.UserId > 0 && r.RecordDate >= trendStart && r.RecordDate < trendEnd)
                .Where(r => r.Status == "High" || r.Status == "Low")
                .GroupBy(r => r.RecordDate.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync();

            var fpTrend = await _context.FootPressureRecords
                .AsNoTracking()
                .Where(r => r.UserId > 0 && r.RecordDate >= trendStart && r.RecordDate < trendEnd)
                .Where(r =>
                    r.LeftFootStatus == "高风险" || r.LeftFootStatus == "极高风险" ||
                    r.RightFootStatus == "高风险" || r.RightFootStatus == "极高风险")
                .GroupBy(r => r.RecordDate.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync();

            var woundTrend = await _context.WoundRecords
                .AsNoTracking()
                .Where(r => r.UserId > 0 && r.RecordDate >= trendStart && r.RecordDate < trendEnd)
                .Where(r => r.HasInfection || r.HasDischarge || r.HasFever || r.HasOdor)
                .GroupBy(r => r.RecordDate.Date)
                .Select(g => new { Day = g.Key, Count = g.Count() })
                .ToListAsync();

            var bsDictTrend = bsTrend.ToDictionary(x => x.Day, x => x.Count);
            var fpDictTrend = fpTrend.ToDictionary(x => x.Day, x => x.Count);
            var woundDictTrend = woundTrend.ToDictionary(x => x.Day, x => x.Count);

            for (var d = trendStart.Date; d < trendEnd.Date; d = d.AddDays(1))
            {
                model.TrendLabels.Add(d.ToString("MM-dd"));
                model.TrendBloodSugarAlerts.Add(bsDictTrend.TryGetValue(d, out var c1) ? c1 : 0);
                model.TrendFootPressureAlerts.Add(fpDictTrend.TryGetValue(d, out var c2) ? c2 : 0);
                model.TrendWoundAlerts.Add(woundDictTrend.TryGetValue(d, out var c3) ? c3 : 0);
            }

            var bloodSugarByPatientHome = await _context.BloodSugarRecords
                .AsNoTracking()
                .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                .Where(r => r.Status == "High" || r.Status == "Low")
                .GroupBy(r => r.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), MaxDate = g.Max(r => r.RecordDate) })
                .ToListAsync();

            var footPressureByPatientHome = await _context.FootPressureRecords
                .AsNoTracking()
                .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                .Where(r =>
                    r.LeftFootStatus == "高风险" || r.LeftFootStatus == "极高风险" ||
                    r.RightFootStatus == "高风险" || r.RightFootStatus == "极高风险")
                .GroupBy(r => r.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), MaxDate = g.Max(r => r.RecordDate) })
                .ToListAsync();

            var woundByPatientHome = await _context.WoundRecords
                .AsNoTracking()
                .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                .Where(r => r.HasInfection || r.HasDischarge || r.HasFever || r.HasOdor)
                .GroupBy(r => r.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), MaxDate = g.Max(r => r.RecordDate) })
                .ToListAsync();

            var patientIdsHome = bloodSugarByPatientHome.Select(x => x.UserId)
                .Concat(footPressureByPatientHome.Select(x => x.UserId))
                .Concat(woundByPatientHome.Select(x => x.UserId))
                .Distinct()
                .ToList();

            if (patientIdsHome.Count > 0)
            {
                var usersHome = await _context.Users
                    .AsNoTracking()
                    .Where(u => patientIdsHome.Contains(u.UserId))
                    .Select(u => new { u.UserId, u.FullName, u.Username, u.PhoneNumber })
                    .ToListAsync();

                var bsDictHome = bloodSugarByPatientHome.ToDictionary(x => x.UserId, x => (x.Count, x.MaxDate));
                var fpDictHome = footPressureByPatientHome.ToDictionary(x => x.UserId, x => (x.Count, x.MaxDate));
                var woundDictHome = woundByPatientHome.ToDictionary(x => x.UserId, x => (x.Count, x.MaxDate));

                var listHome = new List<DoctorPatientAlertSummary>();
                foreach (var u in usersHome)
                {
                    bsDictHome.TryGetValue(u.UserId, out var bs);
                    fpDictHome.TryGetValue(u.UserId, out var fp);
                    woundDictHome.TryGetValue(u.UserId, out var wound);
                    var latest = new[] { bs.MaxDate, fp.MaxDate, wound.MaxDate }.Where(d => d != default).DefaultIfEmpty(default).Max();
                    listHome.Add(new DoctorPatientAlertSummary
                    {
                        PatientId = u.UserId,
                        DisplayName = string.IsNullOrWhiteSpace(u.FullName) ? (u.Username ?? "未命名患者") : u.FullName,
                        PhoneNumber = u.PhoneNumber,
                        BloodSugarAlertCount = bs.Count,
                        FootPressureAlertCount = fp.Count,
                        WoundAlertCount = wound.Count,
                        LatestAlertDate = latest == default ? (DateTime?)null : latest
                    });
                }

                model.HomeHighRiskPatients = listHome
                    .OrderByDescending(x => x.LatestAlertDate ?? DateTime.MinValue)
                    .Take(10)
                    .ToList();
            }
            

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var today = DateTime.Today;

            var consultationMessages = await _context.ConsultationMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m =>
                    (m.SenderId == doctorId && m.Receiver != null && m.Receiver.UserType == "Patient") ||
                    (m.ReceiverId == doctorId && m.Sender != null && m.Sender.UserType == "Patient"))
                .OrderByDescending(m => m.CreatedDate)
                .ToListAsync();

            var pendingReplies = consultationMessages
                .Select(m =>
                {
                    var patient = m.SenderId == doctorId ? m.Receiver : m.Sender;
                    return new
                    {
                        PatientId = patient?.UserId ?? 0,
                        PatientName = string.IsNullOrWhiteSpace(patient?.FullName) ? (patient?.Username ?? "未知患者") : patient.FullName!,
                        LastMessageTime = m.CreatedDate,
                        LastMessagePreview = string.IsNullOrWhiteSpace(m.MessageContent) ? $"[{m.MessageType ?? "消息"}]" : m.MessageContent!,
                        IsUnread = m.ReceiverId == doctorId && !m.IsRead,
                        IsPatientMessage = m.ReceiverId == doctorId
                    };
                })
                .Where(x => x.PatientId > 0)
                .GroupBy(x => new { x.PatientId, x.PatientName })
                .Select(g =>
                {
                    var latest = g.OrderByDescending(x => x.LastMessageTime).First();
                    return new DoctorPendingReplySummary
                    {
                        PatientId = g.Key.PatientId,
                        PatientName = g.Key.PatientName,
                        LatestMessagePreview = latest.LastMessagePreview,
                        LatestMessageTime = latest.LastMessageTime,
                        UnreadCount = g.Count(x => x.IsUnread),
                        ShowUnreadCount = latest.IsPatientMessage && latest.IsUnread
                    };
                })
                .Where(x => x.UnreadCount > 0)
                .ToList();

            var notifications = pendingReplies
                .Select(x => new DoctorNotificationSummary
                {
                    NotificationType = "Consultation",
                    PatientId = x.PatientId,
                    PatientName = x.PatientName,
                    Title = "患者咨询消息",
                    Content = x.LatestMessagePreview,
                    NotificationTime = x.LatestMessageTime,
                    UnreadCount = x.UnreadCount,
                    ShowUnreadCount = x.ShowUnreadCount
                })
                .Concat(await BuildDueFollowUpNotificationsAsync(doctorId, DateTime.Now))
                .OrderByDescending(x => x.NotificationTime)
                .ToList();

            var model = new DoctorNotificationPageViewModel
            {
                NotificationCount = notifications.Count,
                Notifications = notifications
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MultidisciplinaryConsultation()
        {
            var doctors = await _context.OtherDepartmentDoctors
                .AsNoTracking()
                .OrderBy(d => d.Department)
                .ThenBy(d => d.Name)
                .ToListAsync();

            return View(doctors);
        }

        [HttpGet]
        public async Task<IActionResult> MultidisciplinaryConsultationChat(int doctorId)
        {
            if (doctorId <= 0)
            {
                TempData["Error"] = "请选择要会诊的医生。";
                return RedirectToAction(nameof(MultidisciplinaryConsultation));
            }

            var target = await _context.OtherDepartmentDoctors
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (target == null)
            {
                TempData["Error"] = "未找到对应科室医生。";
                return RedirectToAction(nameof(MultidisciplinaryConsultation));
            }

            return View(target);
        }

        [HttpGet]
        public async Task<IActionResult> PatientArchive(int patientId)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var patient = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == patientId && u.UserType == "Patient");

            if (patient == null)
            {
                TempData["Error"] = "未找到对应患者档案。";
                return RedirectToAction(nameof(Index));
            }

            var hiddenHealthKeys = await _context.DoctorHiddenHealthItems
                .AsNoTracking()
                .Where(x => x.DoctorId == doctorId && x.PatientId == patientId)
                .Select(x => x.ItemKey)
                .ToListAsync();
            var hiddenHealthSet = new HashSet<string>(hiddenHealthKeys, StringComparer.OrdinalIgnoreCase);

            var hiddenMessageIds = await _context.DoctorHiddenConsultationMessages
                .AsNoTracking()
                .Where(x => x.DoctorId == doctorId && x.PatientId == patientId)
                .Select(x => x.MessageId)
                .ToListAsync();
            var hiddenMessageSet = new HashSet<int>(hiddenMessageIds);

            var bloodSugarRecords = (await _context.BloodSugarRecords
                .Where(r => r.UserId == patientId)
                .ToListAsync())
                .OrderByDescending(r => r.RecordDate)
                .ThenByDescending(r => r.RecordTime)
                .ToList();

            var latestBloodSugar = bloodSugarRecords.FirstOrDefault(r => !hiddenHealthSet.Contains($"bloodsugar:{r.RecordId}"));
            var latestMedication = bloodSugarRecords.FirstOrDefault(r =>
                ContainsHealthNoteSection(r.Notes, "用药记录") && !hiddenHealthSet.Contains($"medication:{r.RecordId}"));
            var latestDiet = bloodSugarRecords.FirstOrDefault(r =>
                ContainsHealthNoteSection(r.Notes, "进食记录") && !hiddenHealthSet.Contains($"diet:{r.RecordId}"));
            var latestExercise = bloodSugarRecords.FirstOrDefault(r =>
                ContainsHealthNoteSection(r.Notes, "运动记录") && !hiddenHealthSet.Contains($"exercise:{r.RecordId}"));

            var latestWound = (await _context.WoundRecords
                .Where(r => r.UserId == patientId)
                .ToListAsync())
                .OrderByDescending(r => r.RecordDate)
                .ThenByDescending(r => r.RecordTime)
                .FirstOrDefault(r => !hiddenHealthSet.Contains($"wound:{r.WoundId}"));

            var latestFootPressure = (await _context.FootPressureRecords
                .Where(r => r.UserId == patientId)
                .ToListAsync())
                .OrderByDescending(r => r.RecordDate)
                .ThenByDescending(r => r.RecordTime)
                .FirstOrDefault(r => !hiddenHealthSet.Contains($"footpressure:{r.FootPressureId}"));

            var recentMessages = await _context.ConsultationMessages
                .Where(m =>
                    (m.SenderId == doctorId && m.ReceiverId == patientId) ||
                    (m.SenderId == patientId && m.ReceiverId == doctorId))
                .OrderByDescending(m => m.CreatedDate)
                .Take(6)
                .ToListAsync();

            recentMessages = recentMessages
                .Where(m => !hiddenMessageSet.Contains(m.MessageId))
                .Take(6)
                .ToList();

            var model = new DoctorPatientArchiveViewModel
            {
                PatientId = patient.UserId,
                PatientName = string.IsNullOrWhiteSpace(patient.FullName) ? (patient.Username ?? "未命名患者") : patient.FullName,
                Username = patient.Username ?? string.Empty,
                Email = patient.Email,
                Gender = patient.Gender,
                PhoneNumber = patient.PhoneNumber,
                Age = patient.Age,
                ResidenceStatus = patient.ResidenceStatus,
                DiabeticFootType = patient.DiabeticFootType,
                DiseaseCourse = patient.DiseaseCourse,
                DiagnosisDate = patient.DiagnosisDate,
                HadUlcerBeforeVisit = patient.HadUlcerBeforeVisit,
                IsPostFootSurgeryPatient = patient.IsPostFootSurgeryPatient,
                IsActive = patient.IsActive,
                CreatedDate = patient.CreatedDate,
                LastLoginDate = patient.LastLoginDate,
                BloodSugarRecordCount = await _context.BloodSugarRecords.CountAsync(r => r.UserId == patientId),
                WoundRecordCount = await _context.WoundRecords.CountAsync(r => r.UserId == patientId),
                FootPressureRecordCount = await _context.FootPressureRecords.CountAsync(r => r.UserId == patientId),
                ConsultationCount = await _context.ConsultationMessages.CountAsync(m =>
                    (m.SenderId == doctorId && m.ReceiverId == patientId) ||
                    (m.SenderId == patientId && m.ReceiverId == doctorId)),
                LatestBloodSugarRecord = latestBloodSugar == null ? null : new DoctorArchiveRecordSummary
                {
                    RecordId = latestBloodSugar.RecordId,
                    ItemKey = $"bloodsugar:{latestBloodSugar.RecordId}",
                    Title = "最近血糖记录",
                    Summary = $"{GetMealTypeText(latestBloodSugar.MealType)} {latestBloodSugar.BloodSugarValueMmol:F2} mmol/L，状态：{GetStatusText(latestBloodSugar.Status)}",
                    RecordDateTime = latestBloodSugar.RecordDate.Date.Add(latestBloodSugar.RecordTime)
                },
                LatestWoundRecord = latestWound == null ? null : new DoctorArchiveRecordSummary
                {
                    RecordId = latestWound.WoundId,
                    ItemKey = $"wound:{latestWound.WoundId}",
                    Title = "最近伤口记录",
                    Summary = latestWound.GetStatusSummary(),
                    RecordDateTime = latestWound.RecordDate.Date.Add(latestWound.RecordTime)
                },
                LatestFootPressureRecord = latestFootPressure == null ? null : new DoctorArchiveRecordSummary
                {
                    RecordId = latestFootPressure.FootPressureId,
                    ItemKey = $"footpressure:{latestFootPressure.FootPressureId}",
                    Title = "最近足压记录",
                    Summary = latestFootPressure.GetStatusSummary(),
                    RecordDateTime = latestFootPressure.RecordDate.Date.Add(latestFootPressure.RecordTime)
                },
                LatestMedicationRecord = BuildHealthNoteRecordSummary(latestMedication, "最近用药记录", "用药记录"),
                LatestDietRecord = BuildHealthNoteRecordSummary(latestDiet, "最近饮食记录", "进食记录"),
                LatestExerciseRecord = BuildHealthNoteRecordSummary(latestExercise, "最近运动记录", "运动记录"),
                RecentMessages = recentMessages
                    .Select(m => new DoctorArchiveMessageSummary
                    {
                        MessageId = m.MessageId,
                        SenderName = m.SenderId == doctorId ? "医生" : "患者",
                        Content = string.IsNullOrWhiteSpace(m.MessageContent) ? $"[{m.MessageType ?? "消息"}]" : m.MessageContent!,
                        CreatedDate = m.CreatedDate
                    })
                    .ToList()
            };

            return View(model);
        }

        /// <summary>
        /// 患者档案版本号：用于前端轮询判断是否需要局部刷新。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PatientArchiveVersion(int patientId)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (doctorId == 0 || patientId <= 0)
            {
                return Json(new { success = false, message = "参数错误" });
            }

            var patientExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserId == patientId && u.UserType == "Patient");
            if (!patientExists)
            {
                return Json(new { success = false, message = "未找到对应患者" });
            }

            var version = await ComputePatientArchiveVersionAsync(doctorId, patientId);
            return Json(new { success = true, version });
        }

        private async Task<string> ComputePatientArchiveVersionAsync(int doctorId, int patientId)
        {
            var patient = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == patientId && u.UserType == "Patient")
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.FullName,
                    u.Email,
                    u.PhoneNumber,
                    u.Gender,
                    u.Age,
                    u.ResidenceStatus,
                    u.DiabeticFootType,
                    u.DiseaseCourse,
                    u.DiagnosisDate,
                    u.HadUlcerBeforeVisit,
                    u.IsPostFootSurgeryPatient,
                    u.IsActive,
                    u.LastLoginDate
                })
                .FirstOrDefaultAsync();

            // 基本信息变化：直接拼接字段（无需新增 UpdatedAt）
            var patientKey = patient == null
                ? "patient:null"
                : string.Join("|", new[]
                {
                    $"id:{patient.UserId}",
                    $"u:{patient.Username ?? ""}",
                    $"n:{patient.FullName ?? ""}",
                    $"e:{patient.Email ?? ""}",
                    $"p:{patient.PhoneNumber ?? ""}",
                    $"g:{patient.Gender ?? ""}",
                    $"age:{patient.Age?.ToString() ?? ""}",
                    $"res:{patient.ResidenceStatus ?? ""}",
                    $"type:{patient.DiabeticFootType ?? ""}",
                    $"course:{patient.DiseaseCourse ?? ""}",
                    $"dx:{patient.DiagnosisDate?.ToString("O") ?? ""}",
                    $"ulcer:{patient.HadUlcerBeforeVisit ?? ""}",
                    $"surgery:{patient.IsPostFootSurgeryPatient ?? ""}",
                    $"active:{patient.IsActive}",
                    $"last:{patient.LastLoginDate?.ToString("O") ?? ""}"
                });

            var bloodSugarCountTask = _context.BloodSugarRecords.CountAsync(r => r.UserId == patientId);
            var woundCountTask = _context.WoundRecords.CountAsync(r => r.UserId == patientId);
            var footPressureCountTask = _context.FootPressureRecords.CountAsync(r => r.UserId == patientId);
            var consultationCountTask = _context.ConsultationMessages.CountAsync(m =>
                (m.SenderId == doctorId && m.ReceiverId == patientId) ||
                (m.SenderId == patientId && m.ReceiverId == doctorId));

            // 使用 CreatedDate 的 Max 作为变化提示（新增/删除/新消息可感知）
            var bloodSugarLastTask = _context.BloodSugarRecords
                .Where(r => r.UserId == patientId)
                .Select(r => r.CreatedDate)
                .DefaultIfEmpty(DateTime.MinValue)
                .MaxAsync();
            var woundLastTask = _context.WoundRecords
                .Where(r => r.UserId == patientId)
                .Select(r => r.CreatedDate)
                .DefaultIfEmpty(DateTime.MinValue)
                .MaxAsync();
            var footPressureLastTask = _context.FootPressureRecords
                .Where(r => r.UserId == patientId)
                .Select(r => r.CreatedDate)
                .DefaultIfEmpty(DateTime.MinValue)
                .MaxAsync();
            var consultationLastTask = _context.ConsultationMessages
                .Where(m =>
                    (m.SenderId == doctorId && m.ReceiverId == patientId) ||
                    (m.SenderId == patientId && m.ReceiverId == doctorId))
                .Select(m => m.CreatedDate)
                .DefaultIfEmpty(DateTime.MinValue)
                .MaxAsync();

            await Task.WhenAll(
                bloodSugarCountTask, woundCountTask, footPressureCountTask, consultationCountTask,
                bloodSugarLastTask, woundLastTask, footPressureLastTask, consultationLastTask);

            var versionSeed = string.Join("|", new[]
            {
                patientKey,
                $"bsCount:{bloodSugarCountTask.Result}",
                $"wCount:{woundCountTask.Result}",
                $"fpCount:{footPressureCountTask.Result}",
                $"cCount:{consultationCountTask.Result}",
                $"bsLast:{bloodSugarLastTask.Result:O}",
                $"wLast:{woundLastTask.Result:O}",
                $"fpLast:{footPressureLastTask.Result:O}",
                $"cLast:{consultationLastTask.Result:O}"
            });

            return ComputeSha256Base64(versionSeed);
        }

        private static string ComputeSha256Base64(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        [HttpGet]
        public async Task<IActionResult> PatientArchiveBasicInfoPartial(int patientId)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var model = await BuildPatientArchiveViewModelAsync(doctorId, patientId);
            if (model == null) return NotFound();
            return PartialView("~/Views/Doctor/Partials/_PatientArchiveBasicInfoPanel.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> PatientArchiveLatestHealthPartial(int patientId)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var model = await BuildPatientArchiveViewModelAsync(doctorId, patientId);
            if (model == null) return NotFound();
            return PartialView("~/Views/Doctor/Partials/_PatientArchiveLatestHealthPanel.cshtml", model);
        }

        [HttpGet]
        public async Task<IActionResult> PatientArchiveRecentMessagesPartial(int patientId)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var model = await BuildPatientArchiveViewModelAsync(doctorId, patientId);
            if (model == null) return NotFound();
            return PartialView("~/Views/Doctor/Partials/_PatientArchiveRecentMessagesPanel.cshtml", model);
        }

        private async Task<DoctorPatientArchiveViewModel?> BuildPatientArchiveViewModelAsync(int doctorId, int patientId)
        {
            if (doctorId == 0 || patientId <= 0) return null;

            var patient = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == patientId && u.UserType == "Patient");
            if (patient == null) return null;

            var hiddenHealthKeys = await _context.DoctorHiddenHealthItems
                .AsNoTracking()
                .Where(x => x.DoctorId == doctorId && x.PatientId == patientId)
                .Select(x => x.ItemKey)
                .ToListAsync();
            var hiddenHealthSet = new HashSet<string>(hiddenHealthKeys, StringComparer.OrdinalIgnoreCase);

            var hiddenMessageIds = await _context.DoctorHiddenConsultationMessages
                .AsNoTracking()
                .Where(x => x.DoctorId == doctorId && x.PatientId == patientId)
                .Select(x => x.MessageId)
                .ToListAsync();
            var hiddenMessageSet = new HashSet<int>(hiddenMessageIds);

            var bloodSugarRecords = (await _context.BloodSugarRecords
                .Where(r => r.UserId == patientId)
                .ToListAsync())
                .OrderByDescending(r => r.RecordDate)
                .ThenByDescending(r => r.RecordTime)
                .ToList();

            var latestBloodSugar = bloodSugarRecords.FirstOrDefault(r => !hiddenHealthSet.Contains($"bloodsugar:{r.RecordId}"));
            var latestMedication = bloodSugarRecords.FirstOrDefault(r =>
                ContainsHealthNoteSection(r.Notes, "用药记录") && !hiddenHealthSet.Contains($"medication:{r.RecordId}"));
            var latestDiet = bloodSugarRecords.FirstOrDefault(r =>
                ContainsHealthNoteSection(r.Notes, "进食记录") && !hiddenHealthSet.Contains($"diet:{r.RecordId}"));
            var latestExercise = bloodSugarRecords.FirstOrDefault(r =>
                ContainsHealthNoteSection(r.Notes, "运动记录") && !hiddenHealthSet.Contains($"exercise:{r.RecordId}"));

            var latestWound = (await _context.WoundRecords
                .Where(r => r.UserId == patientId)
                .ToListAsync())
                .OrderByDescending(r => r.RecordDate)
                .ThenByDescending(r => r.RecordTime)
                .FirstOrDefault(r => !hiddenHealthSet.Contains($"wound:{r.WoundId}"));

            var latestFootPressure = (await _context.FootPressureRecords
                .Where(r => r.UserId == patientId)
                .ToListAsync())
                .OrderByDescending(r => r.RecordDate)
                .ThenByDescending(r => r.RecordTime)
                .FirstOrDefault(r => !hiddenHealthSet.Contains($"footpressure:{r.FootPressureId}"));

            var recentMessages = await _context.ConsultationMessages
                .Where(m =>
                    (m.SenderId == doctorId && m.ReceiverId == patientId) ||
                    (m.SenderId == patientId && m.ReceiverId == doctorId))
                .OrderByDescending(m => m.CreatedDate)
                .Take(6)
                .ToListAsync();

            recentMessages = recentMessages
                .Where(m => !hiddenMessageSet.Contains(m.MessageId))
                .Take(6)
                .ToList();

            return new DoctorPatientArchiveViewModel
            {
                PatientId = patient.UserId,
                PatientName = string.IsNullOrWhiteSpace(patient.FullName) ? (patient.Username ?? "未命名患者") : patient.FullName,
                Username = patient.Username ?? string.Empty,
                Email = patient.Email,
                Gender = patient.Gender,
                PhoneNumber = patient.PhoneNumber,
                Age = patient.Age,
                ResidenceStatus = patient.ResidenceStatus,
                DiabeticFootType = patient.DiabeticFootType,
                DiseaseCourse = patient.DiseaseCourse,
                DiagnosisDate = patient.DiagnosisDate,
                HadUlcerBeforeVisit = patient.HadUlcerBeforeVisit,
                IsPostFootSurgeryPatient = patient.IsPostFootSurgeryPatient,
                IsActive = patient.IsActive,
                CreatedDate = patient.CreatedDate,
                LastLoginDate = patient.LastLoginDate,
                BloodSugarRecordCount = await _context.BloodSugarRecords.CountAsync(r => r.UserId == patientId),
                WoundRecordCount = await _context.WoundRecords.CountAsync(r => r.UserId == patientId),
                FootPressureRecordCount = await _context.FootPressureRecords.CountAsync(r => r.UserId == patientId),
                ConsultationCount = await _context.ConsultationMessages.CountAsync(m =>
                    (m.SenderId == doctorId && m.ReceiverId == patientId) ||
                    (m.SenderId == patientId && m.ReceiverId == doctorId)),
                LatestBloodSugarRecord = latestBloodSugar == null ? null : new DoctorArchiveRecordSummary
                {
                    RecordId = latestBloodSugar.RecordId,
                    ItemKey = $"bloodsugar:{latestBloodSugar.RecordId}",
                    Title = "最近血糖记录",
                    Summary = $"{GetMealTypeText(latestBloodSugar.MealType)} {latestBloodSugar.BloodSugarValueMmol:F2} mmol/L，状态：{GetStatusText(latestBloodSugar.Status)}",
                    RecordDateTime = latestBloodSugar.RecordDate.Date.Add(latestBloodSugar.RecordTime)
                },
                LatestWoundRecord = latestWound == null ? null : new DoctorArchiveRecordSummary
                {
                    RecordId = latestWound.WoundId,
                    ItemKey = $"wound:{latestWound.WoundId}",
                    Title = "最近伤口记录",
                    Summary = latestWound.GetStatusSummary(),
                    RecordDateTime = latestWound.RecordDate.Date.Add(latestWound.RecordTime)
                },
                LatestFootPressureRecord = latestFootPressure == null ? null : new DoctorArchiveRecordSummary
                {
                    RecordId = latestFootPressure.FootPressureId,
                    ItemKey = $"footpressure:{latestFootPressure.FootPressureId}",
                    Title = "最近足压记录",
                    Summary = latestFootPressure.GetStatusSummary(),
                    RecordDateTime = latestFootPressure.RecordDate.Date.Add(latestFootPressure.RecordTime)
                },
                LatestMedicationRecord = BuildHealthNoteRecordSummary(latestMedication, "最近用药记录", "用药记录"),
                LatestDietRecord = BuildHealthNoteRecordSummary(latestDiet, "最近饮食记录", "进食记录"),
                LatestExerciseRecord = BuildHealthNoteRecordSummary(latestExercise, "最近运动记录", "运动记录"),
                RecentMessages = recentMessages
                    .Select(m => new DoctorArchiveMessageSummary
                    {
                        MessageId = m.MessageId,
                        SenderName = m.SenderId == doctorId ? "医生" : "患者",
                        Content = string.IsNullOrWhiteSpace(m.MessageContent) ? $"[{m.MessageType ?? "消息"}]" : m.MessageContent!,
                        CreatedDate = m.CreatedDate
                    })
                    .ToList()
            };
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelectedHealthRecords(int patientId, List<string>? selectedHealthItems)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var patientExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserId == patientId && u.UserType == "Patient");

            if (!patientExists)
            {
                TempData["Error"] = "未找到对应患者档案。";
                return RedirectToAction(nameof(PatientArchives));
            }

            var selectedItems = (selectedHealthItems ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (selectedItems.Count == 0)
            {
                TempData["Error"] = "请先勾选要删除的健康记录。";
                return RedirectToAction(nameof(PatientArchive), new { patientId });
            }

            var existingKeys = await _context.DoctorHiddenHealthItems
                .AsNoTracking()
                .Where(x => x.DoctorId == doctorId && x.PatientId == patientId && selectedItems.Contains(x.ItemKey))
                .Select(x => x.ItemKey)
                .ToListAsync();
            var existingSet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);

            var toAdd = selectedItems
                .Where(k => !existingSet.Contains(k))
                .Select(k => new DoctorHiddenHealthItem
                {
                    DoctorId = doctorId,
                    PatientId = patientId,
                    ItemKey = k,
                    HiddenAt = DateTime.Now
                })
                .ToList();

            if (toAdd.Count > 0)
            {
                await _context.DoctorHiddenHealthItems.AddRangeAsync(toAdd);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"已在医生端隐藏 {selectedItems.Count} 项健康记录（患者端数据不会被删除）。";

            return RedirectToAction(nameof(PatientArchive), new { patientId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelectedConsultationMessages(int patientId, List<int>? selectedMessageIds)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var patientExists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.UserId == patientId && u.UserType == "Patient");

            if (!patientExists)
            {
                TempData["Error"] = "未找到对应患者档案。";
                return RedirectToAction(nameof(PatientArchives));
            }

            var messageIds = (selectedMessageIds ?? new List<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (messageIds.Count == 0)
            {
                TempData["Error"] = "请先勾选要删除的咨询内容。";
                return RedirectToAction(nameof(PatientArchive), new { patientId });
            }

            var messages = await _context.ConsultationMessages
                .Where(m => messageIds.Contains(m.MessageId))
                .Where(m =>
                    (m.SenderId == doctorId && m.ReceiverId == patientId) ||
                    (m.SenderId == patientId && m.ReceiverId == doctorId))
                .ToListAsync();

            if (messages.Count == 0)
            {
                TempData["Error"] = "未找到可删除的咨询内容。";
                return RedirectToAction(nameof(PatientArchive), new { patientId });
            }

            var existing = await _context.DoctorHiddenConsultationMessages
                .AsNoTracking()
                .Where(x => x.DoctorId == doctorId && x.PatientId == patientId && messageIds.Contains(x.MessageId))
                .Select(x => x.MessageId)
                .ToListAsync();
            var existingSet = existing.ToHashSet();

            var toAdd = messages
                .Where(m => !existingSet.Contains(m.MessageId))
                .Select(m => new DoctorHiddenConsultationMessage
                {
                    DoctorId = doctorId,
                    PatientId = patientId,
                    MessageId = m.MessageId,
                    HiddenAt = DateTime.Now
                })
                .ToList();

            if (toAdd.Count > 0)
            {
                await _context.DoctorHiddenConsultationMessages.AddRangeAsync(toAdd);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"已在医生端隐藏 {messages.Count} 条咨询内容（患者端数据不会被删除）。";
            return RedirectToAction(nameof(PatientArchive), new { patientId });
        }

        [HttpGet]
        public async Task<IActionResult> PatientArchives()
        {
            var patients = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserType == "Patient")
                .OrderByDescending(u => u.CreatedDate)
                .Select(u => new DoctorPatientSummary
                {
                    UserId = u.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(u.FullName) ? (u.Username ?? "未命名患者") : u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    CreatedDate = u.CreatedDate,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            return View(patients);
        }

        [HttpGet]
        public async Task<IActionResult> CustomGroups()
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            List<DoctorCustomGroup> groups;
            List<DoctorPatientGroupMap> maps;
            try
            {
                await EnsureDefaultDoctorGroupsAsync(doctorId);
                await SyncAutoGroupsAsync(doctorId);

                groups = await _context.DoctorCustomGroups
                    .AsNoTracking()
                    .Where(g => g.DoctorId == doctorId)
                    .OrderBy(g => g.GroupName)
                    .ToListAsync();

                maps = await _context.DoctorPatientGroupMaps
                    .AsNoTracking()
                    .Where(m => m.DoctorId == doctorId)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // 典型原因：服务未重启导致建表逻辑未执行，或旧数据库缺少表
                TempData["Error"] = $"自定义分组暂不可用：{ex.Message}。请重启应用后重试。";
                groups = new List<DoctorCustomGroup>();
                maps = new List<DoctorPatientGroupMap>();
            }

            var patients = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserType == "Patient")
                .OrderByDescending(u => u.CreatedDate)
                .Select(u => new DoctorPatientSummary
                {
                    UserId = u.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(u.FullName) ? (u.Username ?? "未命名患者") : u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    CreatedDate = u.CreatedDate,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            // 风险分组（自动）：近30天异常数据聚合
            try
            {
                var endDate = DateTime.Today.AddDays(1);
                var startDate = DateTime.Today.AddDays(-30);

                // 血糖异常：Low / High / ExtremeHigh(>=300mg/dL≈16.7mmol/L)
                var lowDict = await _context.BloodSugarRecords
                    .AsNoTracking()
                    .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                    .Where(r => r.Status == "Low")
                    .GroupBy(r => r.UserId)
                    .Select(g => new { PatientId = g.Key, Latest = g.Max(x => x.RecordDate) })
                    .ToDictionaryAsync(x => x.PatientId, x => x.Latest);

                var extremeHighDict = await _context.BloodSugarRecords
                    .AsNoTracking()
                    .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                    .Where(r => r.Status == "High" && r.BloodSugarValue >= 300m)
                    .GroupBy(r => r.UserId)
                    .Select(g => new { PatientId = g.Key, Latest = g.Max(x => x.RecordDate) })
                    .ToDictionaryAsync(x => x.PatientId, x => x.Latest);

                var highDict = await _context.BloodSugarRecords
                    .AsNoTracking()
                    .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                    .Where(r => r.Status == "High" && r.BloodSugarValue < 300m)
                    .GroupBy(r => r.UserId)
                    .Select(g => new { PatientId = g.Key, Latest = g.Max(x => x.RecordDate) })
                    .ToDictionaryAsync(x => x.PatientId, x => x.Latest);

                // 足压风险：极高风险 / 高风险（不含极高）
                var fpExtremeDict = await _context.FootPressureRecords
                    .AsNoTracking()
                    .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                    .Where(r => r.LeftFootStatus == "极高风险" || r.RightFootStatus == "极高风险")
                    .GroupBy(r => r.UserId)
                    .Select(g => new { PatientId = g.Key, Latest = g.Max(x => x.RecordDate) })
                    .ToDictionaryAsync(x => x.PatientId, x => x.Latest);

                var fpHighDict = await _context.FootPressureRecords
                    .AsNoTracking()
                    .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                    .Where(r =>
                        (r.LeftFootStatus == "高风险" || r.RightFootStatus == "高风险") &&
                        r.LeftFootStatus != "极高风险" && r.RightFootStatus != "极高风险")
                    .GroupBy(r => r.UserId)
                    .Select(g => new { PatientId = g.Key, Latest = g.Max(x => x.RecordDate) })
                    .ToDictionaryAsync(x => x.PatientId, x => x.Latest);

                // 伤口异常：感染/渗出/发热/异味
                var woundDict = await _context.WoundRecords
                    .AsNoTracking()
                    .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                    .Where(r => r.HasInfection || r.HasDischarge || r.HasFever || r.HasOdor)
                    .GroupBy(r => r.UserId)
                    .Select(g => new { PatientId = g.Key, Latest = g.Max(x => x.RecordDate) })
                    .ToDictionaryAsync(x => x.PatientId, x => x.Latest);

                var riskGroups = new List<DoctorRiskGroupSummary>
                {
                    new DoctorRiskGroupSummary
                    {
                        Key = "blood_extreme_high",
                        Title = "极高血糖（近30天）",
                        BadgeClass = "bg-danger",
                        PatientIds = extremeHighDict.Keys.OrderBy(x => x).ToList(),
                        LatestDates = extremeHighDict.ToDictionary(kv => kv.Key, kv => kv.Value)
                    },
                    new DoctorRiskGroupSummary
                    {
                        Key = "blood_high",
                        Title = "高血糖（近30天）",
                        BadgeClass = "bg-danger",
                        PatientIds = highDict.Keys.OrderBy(x => x).ToList(),
                        LatestDates = highDict.ToDictionary(kv => kv.Key, kv => kv.Value)
                    },
                    new DoctorRiskGroupSummary
                    {
                        Key = "blood_low",
                        Title = "低血糖（近30天）",
                        BadgeClass = "bg-warning text-dark",
                        PatientIds = lowDict.Keys.OrderBy(x => x).ToList(),
                        LatestDates = lowDict.ToDictionary(kv => kv.Key, kv => kv.Value)
                    },
                    new DoctorRiskGroupSummary
                    {
                        Key = "footpressure_extreme",
                        Title = "足压极高风险（近30天）",
                        BadgeClass = "bg-danger",
                        PatientIds = fpExtremeDict.Keys.OrderBy(x => x).ToList(),
                        LatestDates = fpExtremeDict.ToDictionary(kv => kv.Key, kv => kv.Value)
                    },
                    new DoctorRiskGroupSummary
                    {
                        Key = "footpressure_high",
                        Title = "足压高风险（近30天）",
                        BadgeClass = "bg-warning text-dark",
                        PatientIds = fpHighDict.Keys.OrderBy(x => x).ToList(),
                        LatestDates = fpHighDict.ToDictionary(kv => kv.Key, kv => kv.Value)
                    },
                    new DoctorRiskGroupSummary
                    {
                        Key = "wound_abnormal",
                        Title = "伤口异常（近30天）",
                        BadgeClass = "bg-danger",
                        PatientIds = woundDict.Keys.OrderBy(x => x).ToList(),
                        LatestDates = woundDict.ToDictionary(kv => kv.Key, kv => kv.Value)
                    }
                };

                ViewBag.RiskGroups = riskGroups;
            }
            catch
            {
                ViewBag.RiskGroups = new List<DoctorRiskGroupSummary>();
            }

            ViewBag.Groups = groups;
            ViewBag.GroupMaps = maps;
            return View(patients);
        }

        private async Task SyncAutoGroupsAsync(int doctorId)
        {
            if (doctorId == 0) return;

            // 两个自动分组：按患者端信息完善字段自动归类（是/否）
            var groupNames = new[]
            {
                "足部术后患者",
                "就诊前已有溃疡患者"
            };

            var autoGroups = await _context.DoctorCustomGroups
                .AsNoTracking()
                .Where(g => g.DoctorId == doctorId && groupNames.Contains(g.GroupName))
                .Select(g => new { g.GroupId, g.GroupName })
                .ToListAsync();

            if (autoGroups.Count == 0) return;

            var patients = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserType == "Patient")
                .Select(u => new
                {
                    u.UserId,
                    u.HadUlcerBeforeVisit,
                    u.IsPostFootSurgeryPatient
                })
                .ToListAsync();

            var postSurgeryIds = patients
                .Where(p => IsYes(p.IsPostFootSurgeryPatient))
                .Select(p => p.UserId)
                .Where(id => id > 0)
                .ToHashSet();

            var ulcerBeforeVisitIds = patients
                .Where(p => IsYes(p.HadUlcerBeforeVisit))
                .Select(p => p.UserId)
                .Where(id => id > 0)
                .ToHashSet();

            foreach (var g in autoGroups)
            {
                var desiredIds = string.Equals(g.GroupName, "足部术后患者", StringComparison.OrdinalIgnoreCase)
                    ? postSurgeryIds
                    : ulcerBeforeVisitIds;

                var existingMaps = await _context.DoctorPatientGroupMaps
                    .Where(m => m.DoctorId == doctorId && m.GroupId == g.GroupId)
                    .ToListAsync();

                var existingIds = existingMaps.Select(m => m.PatientId).ToHashSet();

                var toAdd = desiredIds
                    .Where(id => !existingIds.Contains(id))
                    .Select(id => new DoctorPatientGroupMap
                    {
                        DoctorId = doctorId,
                        GroupId = g.GroupId,
                        PatientId = id,
                        CreatedAt = DateTime.Now
                    })
                    .ToList();

                var toRemove = existingMaps
                    .Where(m => !desiredIds.Contains(m.PatientId))
                    .ToList();

                if (toAdd.Count > 0)
                {
                    await _context.DoctorPatientGroupMaps.AddRangeAsync(toAdd);
                }

                if (toRemove.Count > 0)
                {
                    _context.DoctorPatientGroupMaps.RemoveRange(toRemove);
                }

                if (toAdd.Count > 0 || toRemove.Count > 0)
                {
                    await _context.SaveChangesAsync();
                }
            }
        }

        private static bool IsYes(string? value)
        {
            var v = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(v)) return false;
            return string.Equals(v, "是", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(v, "1", StringComparison.OrdinalIgnoreCase);
        }

        private async Task EnsureDefaultDoctorGroupsAsync(int doctorId)
        {
            if (doctorId == 0) return;

            var defaultNames = new[]
            {
                "重点关注对象",
                "足部术后患者",
                "就诊前已有溃疡患者"
            };

            var existing = await _context.DoctorCustomGroups
                .Where(g => g.DoctorId == doctorId)
                .Select(g => g.GroupName)
                .ToListAsync();

            var existingSet = new HashSet<string>(
                existing.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var toAdd = defaultNames
                .Where(n => !existingSet.Contains(n))
                .Select(n => new DoctorCustomGroup
                {
                    DoctorId = doctorId,
                    GroupName = n,
                    CreatedAt = DateTime.Now
                })
                .ToList();

            if (toAdd.Count == 0) return;

            await _context.DoctorCustomGroups.AddRangeAsync(toAdd);
            await _context.SaveChangesAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustomGroup(string groupName)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            groupName = (groupName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(groupName))
            {
                TempData["Error"] = "请输入分组名称。";
                return RedirectToAction(nameof(CustomGroups));
            }

            try
            {
                _context.DoctorCustomGroups.Add(new DoctorCustomGroup
                {
                    DoctorId = doctorId,
                    GroupName = groupName,
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
                TempData["Success"] = "分组已创建。";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"创建失败：{ex.Message}";
            }
            return RedirectToAction(nameof(CustomGroups));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenameCustomGroup(int groupId, string groupName)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            groupName = (groupName ?? "").Trim();
            if (groupId <= 0 || string.IsNullOrWhiteSpace(groupName))
            {
                TempData["Error"] = "参数错误。";
                return RedirectToAction(nameof(CustomGroups));
            }

            var group = await _context.DoctorCustomGroups.FirstOrDefaultAsync(g => g.GroupId == groupId && g.DoctorId == doctorId);
            if (group == null)
            {
                TempData["Error"] = "分组不存在。";
                return RedirectToAction(nameof(CustomGroups));
            }

            group.GroupName = groupName;
            await _context.SaveChangesAsync();
            TempData["Success"] = "分组已重命名。";
            return RedirectToAction(nameof(CustomGroups));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCustomGroup(int groupId)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var group = await _context.DoctorCustomGroups.FirstOrDefaultAsync(g => g.GroupId == groupId && g.DoctorId == doctorId);
            if (group == null)
            {
                TempData["Error"] = "分组不存在。";
                return RedirectToAction(nameof(CustomGroups));
            }

            _context.DoctorCustomGroups.Remove(group);
            await _context.SaveChangesAsync();
            TempData["Success"] = "分组已删除。";
            return RedirectToAction(nameof(CustomGroups));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignPatientsToGroup(int groupId, List<int>? patientIds)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (groupId <= 0)
            {
                TempData["Error"] = "请选择分组。";
                return RedirectToAction(nameof(CustomGroups));
            }

            var groupExists = await _context.DoctorCustomGroups
                .AsNoTracking()
                .AnyAsync(g => g.GroupId == groupId && g.DoctorId == doctorId);
            if (!groupExists)
            {
                TempData["Error"] = "分组不存在。";
                return RedirectToAction(nameof(CustomGroups));
            }

            var ids = (patientIds ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                TempData["Error"] = "请先勾选患者。";
                return RedirectToAction(nameof(CustomGroups));
            }

            var existing = await _context.DoctorPatientGroupMaps
                .AsNoTracking()
                .Where(m => m.DoctorId == doctorId && m.GroupId == groupId && ids.Contains(m.PatientId))
                .Select(m => m.PatientId)
                .ToListAsync();
            var existingSet = new HashSet<int>(existing);

            var toAdd = ids
                .Where(id => !existingSet.Contains(id))
                .Select(id => new DoctorPatientGroupMap
                {
                    DoctorId = doctorId,
                    PatientId = id,
                    GroupId = groupId,
                    CreatedAt = DateTime.Now
                })
                .ToList();

            if (toAdd.Count > 0)
            {
                await _context.DoctorPatientGroupMaps.AddRangeAsync(toAdd);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"已加入分组（新增 {toAdd.Count} 人）。";
            return RedirectToAction(nameof(CustomGroups));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePatientsFromGroup(int groupId, List<int>? patientIds)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (groupId <= 0)
            {
                TempData["Error"] = "请选择分组。";
                return RedirectToAction(nameof(CustomGroups));
            }

            var groupExists = await _context.DoctorCustomGroups
                .AsNoTracking()
                .AnyAsync(g => g.GroupId == groupId && g.DoctorId == doctorId);
            if (!groupExists)
            {
                TempData["Error"] = "分组不存在。";
                return RedirectToAction(nameof(CustomGroups));
            }

            var ids = (patientIds ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                TempData["Error"] = "请先勾选患者。";
                return RedirectToAction(nameof(CustomGroups));
            }

            var toRemove = await _context.DoctorPatientGroupMaps
                .Where(m => m.DoctorId == doctorId && m.GroupId == groupId && ids.Contains(m.PatientId))
                .ToListAsync();

            if (toRemove.Count > 0)
            {
                _context.DoctorPatientGroupMaps.RemoveRange(toRemove);
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"已移出分组（移除 {toRemove.Count} 人）。";
            return RedirectToAction(nameof(CustomGroups));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPatientToFocusGroup(int patientId, string? returnUrl)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (doctorId == 0 || patientId <= 0)
            {
                TempData["Error"] = "参数错误。";
                return RedirectToAction(nameof(PatientAlerts));
            }

            // 确保默认分组存在
            await EnsureDefaultDoctorGroupsAsync(doctorId);

            var focusGroup = await _context.DoctorCustomGroups
                .AsNoTracking()
                .Where(g => g.DoctorId == doctorId && g.GroupName == "重点关注对象")
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync();

            if (focusGroup == null)
            {
                TempData["Error"] = "重点关注分组初始化失败，请重试。";
                return RedirectToAction(nameof(PatientAlerts));
            }

            var exists = await _context.DoctorPatientGroupMaps
                .AsNoTracking()
                .AnyAsync(m => m.DoctorId == doctorId && m.GroupId == focusGroup.GroupId && m.PatientId == patientId);

            if (!exists)
            {
                _context.DoctorPatientGroupMaps.Add(new DoctorPatientGroupMap
                {
                    DoctorId = doctorId,
                    GroupId = focusGroup.GroupId,
                    PatientId = patientId,
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = exists ? "该患者已在重点关注对象分组中。" : "已列为重点关注对象。";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(PatientAlerts));
        }

        /// <summary>
        /// 高危患者预警：展示患者端触发的预警（血糖异常、足压高风险、伤口异常），按患者汇总，供医生接收与跟进。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PatientAlerts()
        {
            var endDate = DateTime.Today.AddDays(1);
            var startDate = DateTime.Today.AddDays(-30);

            // 血糖异常（高/低血糖）按患者汇总：UserId -> (Count, MaxRecordDate)
            var bloodSugarByPatient = await _context.BloodSugarRecords
                .AsNoTracking()
                .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                .Where(r => r.Status == "High" || r.Status == "Low")
                .GroupBy(r => r.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), MaxDate = g.Max(r => r.RecordDate) })
                .ToListAsync();

            // 足压高风险/极高风险按患者汇总
            var footPressureByPatient = await _context.FootPressureRecords
                .AsNoTracking()
                .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                .Where(r =>
                    r.LeftFootStatus == "高风险" || r.LeftFootStatus == "极高风险" ||
                    r.RightFootStatus == "高风险" || r.RightFootStatus == "极高风险")
                .GroupBy(r => r.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), MaxDate = g.Max(r => r.RecordDate) })
                .ToListAsync();

            // 伤口异常按患者汇总
            var woundByPatient = await _context.WoundRecords
                .AsNoTracking()
                .Where(r => r.UserId > 0 && r.RecordDate >= startDate && r.RecordDate < endDate)
                .Where(r => r.HasInfection || r.HasDischarge || r.HasFever || r.HasOdor)
                .GroupBy(r => r.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count(), MaxDate = g.Max(r => r.RecordDate) })
                .ToListAsync();

            var patientIds = bloodSugarByPatient.Select(x => x.UserId)
                .Concat(footPressureByPatient.Select(x => x.UserId))
                .Concat(woundByPatient.Select(x => x.UserId))
                .Distinct()
                .ToList();

            if (patientIds.Count == 0)
            {
                var emptyNotifications = await _highRiskAlertService.GetRecentNotificationsAsync(30, 80);
                var emptyNotificationItems = emptyNotifications.Select(n => new HighRiskAlertNotificationItem
                {
                    NotificationId = n.NotificationId,
                    PatientId = n.PatientId,
                    PatientName = string.IsNullOrWhiteSpace(n.Patient?.FullName) ? (n.Patient?.Username ?? "未命名患者") : n.Patient!.FullName!,
                    AlertType = n.AlertType,
                    AlertTypeDisplay = n.AlertType switch { "BloodSugarHigh" => "血糖异常(高)", "BloodSugarLow" => "血糖异常(低)", "FootPressureHigh" => "足压高风险", "WoundAbnormal" => "伤口异常", _ => n.AlertType },
                    Summary = n.Summary,
                    CreatedAt = n.CreatedAt
                }).ToList();
                return View(new DoctorPatientAlertsPageViewModel
                {
                    DateRangeText = $"{startDate:yyyy-MM-dd} 至 {endDate.AddDays(-1):yyyy-MM-dd}",
                    Patients = Array.Empty<DoctorPatientAlertSummary>(),
                    LatestNotifications = emptyNotificationItems
                });
            }

            var users = await _context.Users
                .AsNoTracking()
                .Where(u => patientIds.Contains(u.UserId))
                .Select(u => new { u.UserId, u.FullName, u.Username, u.PhoneNumber })
                .ToListAsync();

            var bsDict = bloodSugarByPatient.ToDictionary(x => x.UserId, x => (x.Count, x.MaxDate));
            var fpDict = footPressureByPatient.ToDictionary(x => x.UserId, x => (x.Count, x.MaxDate));
            var woundDict = woundByPatient.ToDictionary(x => x.UserId, x => (x.Count, x.MaxDate));

            var list = new List<DoctorPatientAlertSummary>();
            foreach (var u in users)
            {
                bsDict.TryGetValue(u.UserId, out var bs);
                fpDict.TryGetValue(u.UserId, out var fp);
                woundDict.TryGetValue(u.UserId, out var wound);
                var latest = new[] { bs.MaxDate, fp.MaxDate, wound.MaxDate }.Where(d => d != default).DefaultIfEmpty(default).Max();
                list.Add(new DoctorPatientAlertSummary
                {
                    PatientId = u.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(u.FullName) ? (u.Username ?? "未命名患者") : u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    BloodSugarAlertCount = bs.Count,
                    FootPressureAlertCount = fp.Count,
                    WoundAlertCount = wound.Count,
                    LatestAlertDate = latest == default ? (DateTime?)null : latest
                });
            }

            list = list.OrderByDescending(x => x.LatestAlertDate ?? DateTime.MinValue).ToList();

            var notifications = await _highRiskAlertService.GetRecentNotificationsAsync(30, 80);
            var latestNotificationItems = notifications.Select(n => new HighRiskAlertNotificationItem
            {
                NotificationId = n.NotificationId,
                PatientId = n.PatientId,
                PatientName = string.IsNullOrWhiteSpace(n.Patient?.FullName) ? (n.Patient?.Username ?? "未命名患者") : n.Patient.FullName!,
                AlertType = n.AlertType,
                AlertTypeDisplay = n.AlertType switch
                {
                    "BloodSugarHigh" => "血糖异常(高)",
                    "BloodSugarLow" => "血糖异常(低)",
                    "FootPressureHigh" => "足压高风险",
                    "WoundAbnormal" => "伤口异常",
                    _ => n.AlertType
                },
                Summary = n.Summary,
                CreatedAt = n.CreatedAt
            }).ToList();

            var latestByPatient = latestNotificationItems
                .GroupBy(n => n.PatientId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());
            foreach (var p in list)
            {
                p.LatestPush = latestByPatient.TryGetValue(p.PatientId, out var n) ? n : null;
            }

            var model = new DoctorPatientAlertsPageViewModel
            {
                DateRangeText = $"{startDate:yyyy-MM-dd} 至 {endDate.AddDays(-1):yyyy-MM-dd}",
                Patients = list,
                LatestNotifications = latestNotificationItems
            };
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> FollowUps()
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var model = await BuildFollowUpPageViewModelAsync(doctorId, new DoctorFollowUpCreateRequest
            {
                FollowUpDate = DateTime.Today,
                FollowUpMethod = "电话回访"
            });
            model.StatusMessage = TempData["FollowUpMessage"]?.ToString();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Orders(int patientId = 0, int days = 7)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var patients = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserType == "Patient")
                .OrderByDescending(u => u.CreatedDate)
                .Select(u => new DoctorPatientSummary
                {
                    UserId = u.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(u.FullName) ? (u.Username ?? "未命名患者") : u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    CreatedDate = u.CreatedDate,
                    IsActive = u.IsActive
                })
                .ToListAsync();

            if (patientId == 0 && patients.Count > 0) patientId = patients[0].UserId;

            var today = DateTime.Today;
            days = days is 30 or 7 ? days : 7;
            var rangeStart = today.AddDays(-(days - 1));
            var rangeEndExclusive = today.AddDays(1);

            var orders = await _context.DoctorOrders
                .AsNoTracking()
                .Where(o => o.DoctorId == doctorId && (patientId <= 0 || o.PatientId == patientId))
                .OrderByDescending(o => o.CreatedAt)
                .Take(50)
                .ToListAsync();

            var patientNameDict = patients.ToDictionary(p => p.UserId, p => p.DisplayName);

            // 生成并读取“今日任务完成情况”（基于有效医嘱）
            var todayTasks = new List<DoctorDailyTaskItem>();
            if (patientId > 0)
            {
                var activeOrders = await _context.DoctorOrders
                    .AsNoTracking()
                    .Where(o => o.PatientId == patientId && o.IsActive)
                    .Where(o => o.StartDate.Date <= today && (!o.EndDate.HasValue || o.EndDate.Value.Date >= today))
                    .ToListAsync();

                if (activeOrders.Count > 0)
                {
                    var orderIds = activeOrders.Select(o => o.DoctorOrderId).ToList();
                    var existing = await _context.PatientDailyTasks
                        .AsNoTracking()
                        .Where(t => t.PatientId == patientId && t.TaskDate == today && orderIds.Contains(t.DoctorOrderId))
                        .Select(t => t.DoctorOrderId)
                        .ToListAsync();

                    var existingSet = existing.ToHashSet();
                    var toAdd = activeOrders
                        .Where(o => !existingSet.Contains(o.DoctorOrderId))
                        .Select(o => new PatientDailyTask
                        {
                            PatientId = patientId,
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

                todayTasks = await _context.PatientDailyTasks
                    .AsNoTracking()
                    .Join(_context.DoctorOrders.AsNoTracking(),
                        t => t.DoctorOrderId,
                        o => o.DoctorOrderId,
                        (t, o) => new { t, o })
                    .Where(x => x.t.PatientId == patientId && x.t.TaskDate == today)
                    .OrderBy(x => x.o.Category)
                    .ThenByDescending(x => x.o.CreatedAt)
                    .Select(x => new DoctorDailyTaskItem
                    {
                        PatientDailyTaskId = x.t.PatientDailyTaskId,
                        Category = x.o.Category,
                        Content = x.o.Content,
                        IsCompleted = x.t.IsCompleted,
                        CompletedAt = x.t.CompletedAt
                    })
                    .ToListAsync();
            }

            // 近 N 天完成率：确保任务存在并聚合
            var analyticsLabels = new List<string>();
            var analyticsTotal = new List<int>();
            var analyticsDone = new List<int>();
            var analyticsByCategory = new List<DoctorTaskCategorySummary>();

            if (patientId > 0)
            {
                var activeOrdersRange = await _context.DoctorOrders
                    .AsNoTracking()
                    .Where(o => o.PatientId == patientId && o.IsActive)
                    .Where(o => o.StartDate.Date <= today && (!o.EndDate.HasValue || o.EndDate.Value.Date >= rangeStart))
                    .ToListAsync();

                if (activeOrdersRange.Count > 0)
                {
                    var orderIds = activeOrdersRange.Select(o => o.DoctorOrderId).ToList();
                    var existingTasks = await _context.PatientDailyTasks
                        .AsNoTracking()
                        .Where(t => t.PatientId == patientId && t.TaskDate >= rangeStart && t.TaskDate < rangeEndExclusive)
                        .Where(t => orderIds.Contains(t.DoctorOrderId))
                        .Select(t => new { t.DoctorOrderId, t.TaskDate })
                        .ToListAsync();

                    var existingKey = existingTasks
                        .Select(x => $"{x.DoctorOrderId}|{x.TaskDate:yyyyMMdd}")
                        .ToHashSet(StringComparer.Ordinal);

                    var toAdd = new List<PatientDailyTask>();
                    foreach (var o in activeOrdersRange)
                    {
                        var start = o.StartDate.Date < rangeStart ? rangeStart : o.StartDate.Date;
                        var end = o.EndDate.HasValue && o.EndDate.Value.Date < today ? o.EndDate.Value.Date : today;
                        for (var d = start; d <= end; d = d.AddDays(1))
                        {
                            var key = $"{o.DoctorOrderId}|{d:yyyyMMdd}";
                            if (existingKey.Contains(key)) continue;
                            existingKey.Add(key);
                            toAdd.Add(new PatientDailyTask
                            {
                                PatientId = patientId,
                                DoctorOrderId = o.DoctorOrderId,
                                TaskDate = d,
                                IsCompleted = false,
                                CompletedAt = null,
                                CreatedAt = DateTime.Now
                            });
                        }
                    }

                    if (toAdd.Count > 0)
                    {
                        await _context.PatientDailyTasks.AddRangeAsync(toAdd);
                        await _context.SaveChangesAsync();
                    }
                }

                var tasksRange = await _context.PatientDailyTasks
                    .AsNoTracking()
                    .Join(_context.DoctorOrders.AsNoTracking(),
                        t => t.DoctorOrderId,
                        o => o.DoctorOrderId,
                        (t, o) => new { t, o })
                    .Where(x => x.t.PatientId == patientId && x.t.TaskDate >= rangeStart && x.t.TaskDate < rangeEndExclusive)
                    .Select(x => new
                    {
                        Day = x.t.TaskDate.Date,
                        x.o.Category,
                        x.t.IsCompleted
                    })
                    .ToListAsync();

                for (var d = rangeStart.Date; d <= today; d = d.AddDays(1))
                {
                    var dayItems = tasksRange.Where(x => x.Day == d).ToList();
                    analyticsLabels.Add(d.ToString("MM-dd"));
                    analyticsTotal.Add(dayItems.Count);
                    analyticsDone.Add(dayItems.Count(x => x.IsCompleted));
                }

                analyticsByCategory = tasksRange
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "其他" : x.Category.Trim())
                    .Select(g => new DoctorTaskCategorySummary
                    {
                        Category = g.Key,
                        Total = g.Count(),
                        Completed = g.Count(x => x.IsCompleted)
                    })
                    .OrderByDescending(x => x.Total)
                    .ToList();
            }

            var model = new DoctorOrdersPageViewModel
            {
                SelectedPatientId = patientId,
                Patients = patients,
                Orders = orders.Select(o => new DoctorOrderItem
                {
                    DoctorOrderId = o.DoctorOrderId,
                    PatientId = o.PatientId,
                    PatientName = patientNameDict.TryGetValue(o.PatientId, out var n) ? n : $"患者#{o.PatientId}",
                    Category = o.Category,
                    Content = o.Content,
                    StartDate = o.StartDate,
                    EndDate = o.EndDate,
                    IsActive = o.IsActive,
                    CreatedAt = o.CreatedAt
                }).ToList(),
                Today = today,
                TodayTasks = todayTasks,
                AnalyticsDays = days,
                AnalyticsLabels = analyticsLabels,
                AnalyticsTotalTasks = analyticsTotal,
                AnalyticsCompletedTasks = analyticsDone,
                AnalyticsByCategory = analyticsByCategory,
                StatusMessage = TempData["OrderMessage"]?.ToString()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> OrdersTasksExport(int patientId, int days = 7)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (doctorId == 0 || patientId <= 0) return RedirectToAction(nameof(Orders));
            days = days is 30 or 7 ? days : 7;

            var today = DateTime.Today;
            var rangeStart = today.AddDays(-(days - 1));
            var rangeEndExclusive = today.AddDays(1);

            var patient = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == patientId && u.UserType == "Patient");
            var patientName = patient == null ? $"patient_{patientId}" : (string.IsNullOrWhiteSpace(patient.FullName) ? (patient.Username ?? $"patient_{patientId}") : patient.FullName);

            var rows = await _context.PatientDailyTasks
                .AsNoTracking()
                .Join(_context.DoctorOrders.AsNoTracking(),
                    t => t.DoctorOrderId,
                    o => o.DoctorOrderId,
                    (t, o) => new { t, o })
                .Where(x => x.t.PatientId == patientId && x.t.TaskDate >= rangeStart && x.t.TaskDate < rangeEndExclusive)
                .OrderByDescending(x => x.t.TaskDate)
                .ThenBy(x => x.o.Category)
                .Select(x => new
                {
                    x.t.TaskDate,
                    Category = x.o.Category,
                    x.o.Content,
                    x.t.IsCompleted,
                    x.t.CompletedAt
                })
                .ToListAsync();

            string Escape(string s)
            {
                s ??= string.Empty;
                if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                {
                    return "\"" + s.Replace("\"", "\"\"") + "\"";
                }
                return s;
            }

            var sb = new StringBuilder();
            sb.AppendLine("日期,类型,任务内容,是否完成,完成时间");
            foreach (var r in rows)
            {
                sb.Append(Escape(r.TaskDate.ToString("yyyy-MM-dd"))).Append(",");
                sb.Append(Escape(r.Category ?? "")).Append(",");
                sb.Append(Escape(r.Content ?? "")).Append(",");
                sb.Append(Escape(r.IsCompleted ? "是" : "否")).Append(",");
                sb.AppendLine(Escape(r.CompletedAt.HasValue ? r.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm") : ""));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"任务打卡_{patientName}_{rangeStart:yyyyMMdd}-{today:yyyyMMdd}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrder(int patientId, string category, string content, DateTime startDate, DateTime? endDate)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (doctorId == 0 || patientId <= 0 || string.IsNullOrWhiteSpace(content))
            {
                TempData["OrderMessage"] = "参数错误，请完整填写医嘱内容。";
                return RedirectToAction(nameof(Orders), new { patientId });
            }

            var order = new DoctorOrder
            {
                DoctorId = doctorId,
                PatientId = patientId,
                Category = string.IsNullOrWhiteSpace(category) ? "其他" : category.Trim(),
                Content = content.Trim(),
                StartDate = startDate.Date,
                EndDate = endDate?.Date,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.DoctorOrders.Add(order);
            await _context.SaveChangesAsync();

            TempData["OrderMessage"] = "医嘱已下达，患者端将自动生成每日打卡任务。";
            return RedirectToAction(nameof(Orders), new { patientId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FollowUps(DoctorFollowUpCreateRequest request)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildFollowUpPageViewModelAsync(doctorId, request);
                invalidModel.StatusMessage = "请完整填写回访登记信息。";
                return View(invalidModel);
            }

            var patient = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == request.PatientId && u.UserType == "Patient");

            if (patient == null)
            {
                var invalidModel = await BuildFollowUpPageViewModelAsync(doctorId, request);
                invalidModel.StatusMessage = "未找到对应患者，无法保存回访记录。";
                return View(invalidModel);
            }

            var record = new FollowUpRecord
            {
                DoctorId = doctorId,
                PatientId = request.PatientId,
                FollowUpDate = request.FollowUpDate.Date,
                FollowUpMethod = string.IsNullOrWhiteSpace(request.FollowUpMethod) ? "电话回访" : request.FollowUpMethod.Trim(),
                Summary = request.Summary.Trim(),
                Advice = string.IsNullOrWhiteSpace(request.Advice) ? null : request.Advice.Trim(),
                NextFollowUpDate = request.NextFollowUpDate?.Date,
                CreatedDate = DateTime.Now
            };

            _context.FollowUpRecords.Add(record);
            await _context.SaveChangesAsync();

            // 生成回访登记后：将该患者“已到期”的回访提醒标记为已处理（避免首页继续显示）
            try
            {
                var now = DateTime.Now;
                var toProcess = await _context.FollowUpReminderNotifications
                    .Where(n => n.DoctorId == doctorId && n.PatientId == request.PatientId && n.ProcessedAt == null)
                    .Where(n => n.NextFollowUpDate <= now)
                    .ToListAsync();

                if (toProcess.Count > 0)
                {
                    foreach (var n in toProcess)
                    {
                        n.ProcessedAt = now;
                    }
                    await _context.SaveChangesAsync();
                }
            }
            catch
            {
                // 忽略：不影响回访登记保存
            }

            var patientName = string.IsNullOrWhiteSpace(patient.FullName) ? (patient.Username ?? "患者") : patient.FullName;
            TempData["FollowUpMessage"] = $"已保存 {patientName} 的回访登记。";
            return RedirectToAction(nameof(FollowUps));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelectedFollowUps(List<int>? selectedFollowUpIds)
        {
            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var followUpIds = (selectedFollowUpIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (followUpIds.Count == 0)
            {
                TempData["FollowUpMessage"] = "请先勾选要删除的回访记录。";
                return RedirectToAction(nameof(FollowUps));
            }

            var records = await _context.FollowUpRecords
                .Where(f => f.DoctorId == doctorId && followUpIds.Contains(f.FollowUpRecordId))
                .ToListAsync();

            if (records.Count == 0)
            {
                TempData["FollowUpMessage"] = "未找到可删除的回访记录。";
                return RedirectToAction(nameof(FollowUps));
            }

            var existing = await _context.DoctorHiddenFollowUpRecords
                .AsNoTracking()
                .Where(x => x.DoctorId == doctorId && followUpIds.Contains(x.FollowUpRecordId))
                .Select(x => x.FollowUpRecordId)
                .ToListAsync();
            var existingSet = existing.ToHashSet();

            var toAdd = records
                .Where(r => !existingSet.Contains(r.FollowUpRecordId))
                .Select(r => new DoctorHiddenFollowUpRecord
                {
                    DoctorId = doctorId,
                    FollowUpRecordId = r.FollowUpRecordId,
                    HiddenAt = DateTime.Now
                })
                .ToList();

            if (toAdd.Count > 0)
            {
                await _context.DoctorHiddenFollowUpRecords.AddRangeAsync(toAdd);
                await _context.SaveChangesAsync();
            }

            TempData["FollowUpMessage"] = $"已在医生端隐藏 {records.Count} 条回访记录（患者端数据不会被删除）。";
            return RedirectToAction(nameof(FollowUps));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePatientArchive(int patientId)
        {
            var patient = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == patientId && u.UserType == "Patient");

            if (patient == null)
            {
                TempData["Error"] = "未找到要删除的患者。";
                return RedirectToAction(nameof(PatientArchives));
            }

            try
            {
                await DeletePatientRelatedDataAsync(patientId);
                var patientName = string.IsNullOrWhiteSpace(patient.FullName) ? (patient.Username ?? $"患者#{patientId}") : patient.FullName;
                TempData["Success"] = $"患者“{patientName}”及其相关数据已删除。";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"删除患者失败：{ex.Message}";
            }

            return RedirectToAction(nameof(PatientArchives));
        }

        private IActionResult RedirectToPortalByUserType(string? userType)
        {
            if (string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Dashboard");
            }

            if (string.Equals(userType, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Admin");
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        private static string GetMealTypeText(string? mealType)
        {
            return mealType switch
            {
                "Fasting" => "空腹",
                "AfterMeal" => "餐后",
                "BeforeMeal" => "餐前",
                "AfterMeal1" => "第一次餐后",
                "AfterMeal2" => "第二次餐后",
                "AfterMeal3" => "第三次餐后",
                _ => mealType ?? "未知"
            };
        }

        private static string GetStatusText(string? status)
        {
            return status switch
            {
                "Normal" => "正常",
                "High" => "偏高",
                "Low" => "偏低",
                _ => status ?? "未知"
            };
        }

        private async Task DeletePatientRelatedDataAsync(int patientId)
        {
            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            var patient = await _context.Users.FirstOrDefaultAsync(u => u.UserId == patientId && u.UserType == "Patient");
            if (patient == null)
            {
                throw new InvalidOperationException("患者不存在。");
            }

            var patientPostIds = await _context.Posts
                .Where(p => p.UserId == patientId)
                .Select(p => p.PostId)
                .ToListAsync();

            if (patientPostIds.Count > 0)
            {
                var postComments = await _context.Comments
                    .Where(c => patientPostIds.Contains(c.PostId))
                    .ToListAsync();
                if (postComments.Count > 0)
                {
                    _context.Comments.RemoveRange(postComments);
                }
            }

            var ownComments = await _context.Comments
                .Where(c => c.UserId == patientId)
                .ToListAsync();
            if (ownComments.Count > 0)
            {
                _context.Comments.RemoveRange(ownComments);
            }

            var ownPosts = await _context.Posts
                .Where(p => p.UserId == patientId)
                .ToListAsync();
            if (ownPosts.Count > 0)
            {
                _context.Posts.RemoveRange(ownPosts);
            }

            var consultationMessages = await _context.ConsultationMessages
                .Where(m => m.SenderId == patientId || m.ReceiverId == patientId)
                .ToListAsync();
            if (consultationMessages.Count > 0)
            {
                _context.ConsultationMessages.RemoveRange(consultationMessages);
            }

            var reminders = await _context.Reminders
                .Where(r => r.UserId == patientId)
                .ToListAsync();
            if (reminders.Count > 0)
            {
                _context.Reminders.RemoveRange(reminders);
            }

            var bloodSugarRecords = await _context.BloodSugarRecords
                .Where(r => r.UserId == patientId)
                .ToListAsync();
            if (bloodSugarRecords.Count > 0)
            {
                _context.BloodSugarRecords.RemoveRange(bloodSugarRecords);
            }

            var woundRecords = await _context.WoundRecords
                .Where(r => r.UserId == patientId)
                .ToListAsync();
            if (woundRecords.Count > 0)
            {
                _context.WoundRecords.RemoveRange(woundRecords);
            }

            var footPressureRecords = await _context.FootPressureRecords
                .Where(r => r.UserId == patientId)
                .ToListAsync();
            if (footPressureRecords.Count > 0)
            {
                _context.FootPressureRecords.RemoveRange(footPressureRecords);
            }

            _context.Users.Remove(patient);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        private async Task<DoctorFollowUpPageViewModel> BuildFollowUpPageViewModelAsync(int doctorId, DoctorFollowUpCreateRequest form)
        {
            var patients = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserType == "Patient")
                .OrderByDescending(u => u.CreatedDate)
                .Select(u => new DoctorFollowUpPatientOption
                {
                    PatientId = u.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(u.FullName) ? (u.Username ?? $"患者#{u.UserId}") : u.FullName
                })
                .ToListAsync();

            var hiddenFollowUpIds = await _context.DoctorHiddenFollowUpRecords
                .AsNoTracking()
                .Where(x => x.DoctorId == doctorId)
                .Select(x => x.FollowUpRecordId)
                .ToListAsync();
            var hiddenFollowUpSet = new HashSet<int>(hiddenFollowUpIds);

            var records = await _context.FollowUpRecords
                .AsNoTracking()
                .Include(f => f.Patient)
                .Where(f => f.DoctorId == doctorId)
                .Where(f => !hiddenFollowUpSet.Contains(f.FollowUpRecordId))
                .OrderByDescending(f => f.FollowUpDate)
                .ThenByDescending(f => f.CreatedDate)
                .Take(20)
                .Select(f => new DoctorFollowUpRecordItem
                {
                    FollowUpRecordId = f.FollowUpRecordId,
                    PatientId = f.PatientId,
                    PatientName = string.IsNullOrWhiteSpace(f.Patient!.FullName) ? (f.Patient.Username ?? $"患者#{f.PatientId}") : f.Patient.FullName!,
                    FollowUpDate = f.FollowUpDate,
                    FollowUpMethod = f.FollowUpMethod,
                    Summary = f.Summary,
                    Advice = f.Advice,
                    NextFollowUpDate = f.NextFollowUpDate,
                    CreatedDate = f.CreatedDate
                })
                .ToListAsync();

            return new DoctorFollowUpPageViewModel
            {
                Form = form,
                Patients = patients,
                Records = records
            };
        }

        private static bool ContainsHealthNoteSection(string? notes, string sectionTitle)
        {
            return !string.IsNullOrWhiteSpace(notes) && notes.Contains(sectionTitle, StringComparison.OrdinalIgnoreCase);
        }

        private static DoctorArchiveRecordSummary? BuildHealthNoteRecordSummary(BloodSugarRecord? record, string title, string sectionTitle)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Notes))
            {
                return null;
            }

            var summary = ExtractHealthNoteSection(record.Notes, sectionTitle);
            if (string.IsNullOrWhiteSpace(summary))
            {
                return null;
            }

            return new DoctorArchiveRecordSummary
            {
                RecordId = record.RecordId,
                ItemKey = $"{GetHealthSectionKey(sectionTitle)}:{record.RecordId}",
                Title = title,
                Summary = summary,
                RecordDateTime = record.RecordDate.Date.Add(record.RecordTime)
            };
        }

        private static string ExtractHealthNoteSection(string notes, string sectionTitle)
        {
            var match = Regex.Match(notes, $"{Regex.Escape(sectionTitle)}；(?<content>.*?)(?:。|$)");
            return match.Success ? match.Groups["content"].Value.Trim() : string.Empty;
        }

        private async Task<List<DoctorNotificationSummary>> BuildDueFollowUpNotificationsAsync(int doctorId, DateTime now)
        {
            if (doctorId == 0) return new List<DoctorNotificationSummary>();

            // 1) 找到已到期的回访记录
            var dueFollowUps = await _context.FollowUpRecords
                .AsNoTracking()
                .Where(f => f.DoctorId == doctorId && f.NextFollowUpDate.HasValue && f.NextFollowUpDate.Value <= now)
                .Select(f => new
                {
                    f.FollowUpRecordId,
                    f.PatientId,
                    NextFollowUpDate = f.NextFollowUpDate!.Value
                })
                .ToListAsync();

            if (dueFollowUps.Count > 0)
            {
                // 2) 去重：已生成过提醒的不再插入
                var dueIds = dueFollowUps.Select(x => x.FollowUpRecordId).Distinct().ToList();
                var existingIds = await _context.FollowUpReminderNotifications
                    .AsNoTracking()
                    .Where(n => n.DoctorId == doctorId && dueIds.Contains(n.FollowUpRecordId))
                    .Select(n => n.FollowUpRecordId)
                    .ToListAsync();

                var existingSet = new HashSet<int>(existingIds);
                var toAdd = dueFollowUps
                    .Where(x => !existingSet.Contains(x.FollowUpRecordId))
                    .Select(x => new FollowUpReminderNotification
                    {
                        DoctorId = doctorId,
                        PatientId = x.PatientId,
                        FollowUpRecordId = x.FollowUpRecordId,
                        NextFollowUpDate = x.NextFollowUpDate,
                        CreatedAt = now,
                        ProcessedAt = null
                    })
                    .ToList();

                if (toAdd.Count > 0)
                {
                    await _context.FollowUpReminderNotifications.AddRangeAsync(toAdd);
                    await _context.SaveChangesAsync();
                }
            }

            // 3) 从提醒表读取并展示（按患者聚合展示最新一条）
            var notifications = await _context.FollowUpReminderNotifications
                .AsNoTracking()
                .Include(n => n.Patient)
                .Where(n => n.DoctorId == doctorId && n.ProcessedAt == null)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            return notifications
                .GroupBy(n => new
                {
                    n.PatientId,
                    PatientName = n.Patient == null
                        ? $"患者#{n.PatientId}"
                        : (string.IsNullOrWhiteSpace(n.Patient.FullName) ? (n.Patient.Username ?? $"患者#{n.PatientId}") : n.Patient.FullName!)
                })
                .Select(g =>
                {
                    var latest = g.OrderByDescending(x => x.CreatedAt).First();
                    return new DoctorNotificationSummary
                    {
                        NotificationType = "FollowUp",
                        PatientId = latest.PatientId,
                        PatientName = g.Key.PatientName,
                        Title = "患者回访登记提醒",
                        Content = $"患者 {g.Key.PatientName} 已到下次回访时间，请及时处理回访登记。",
                        NotificationTime = latest.CreatedAt,
                        NextFollowUpDate = latest.NextFollowUpDate
                    };
                })
                .OrderByDescending(x => x.NotificationTime)
                .ToList();
        }

        private static (string RecordType, int RecordId) ParseArchiveSelection(string itemKey)
        {
            var parts = itemKey.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var recordId))
            {
                return (string.Empty, 0);
            }

            return (parts[0], recordId);
        }

        private static string GetHealthSectionKey(string sectionTitle)
        {
            return sectionTitle switch
            {
                "用药记录" => "medication",
                "进食记录" => "diet",
                "运动记录" => "exercise",
                _ => string.Empty
            };
        }

        private static string GetHealthSectionTitle(string recordType)
        {
            return recordType.ToLowerInvariant() switch
            {
                "medication" => "用药记录",
                "diet" => "进食记录",
                "exercise" => "运动记录",
                _ => string.Empty
            };
        }

        private static string RemoveHealthNoteSection(string notes, string sectionTitle)
        {
            if (string.IsNullOrWhiteSpace(notes) || string.IsNullOrWhiteSpace(sectionTitle))
            {
                return notes;
            }

            var sections = notes
                .Split('。', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(section => !section.StartsWith(sectionTitle, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return sections.Count == 0 ? string.Empty : string.Join("。", sections) + "。";
        }
    }
}
