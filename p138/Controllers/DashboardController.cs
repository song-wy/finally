using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Services;
using DiabetesPatientApp.ViewModels;

namespace DiabetesPatientApp.Controllers
{
    public class DashboardController : Controller
    {
        private readonly IBloodSugarService _bloodSugarService;
        private readonly IWoundService _woundService;
        private readonly IFootPressureService _footPressureService;
        private readonly IConsultationService _consultationService;
        private readonly DiabetesDbContext _context;

        public DashboardController(
            IBloodSugarService bloodSugarService,
            IWoundService woundService,
            IFootPressureService footPressureService,
            IConsultationService consultationService,
            DiabetesDbContext context)
        {
            _bloodSugarService = bloodSugarService;
            _woundService = woundService;
            _footPressureService = footPressureService;
            _consultationService = consultationService;
            _context = context;
        }

        private int GetUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userId = GetUserId();
            if (userId == 0)
            {
                context.Result = RedirectToAction("Login", "Auth");
                return;
            }

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = RedirectToPortalByUserType(userType);
                return;
            }

            base.OnActionExecuting(context);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var now = GetBeijingNow();
            var today = now.Date;
            var weekStart = GetWeekStart(today);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var todayBloodSugar = await _bloodSugarService.GetRecordsByDateAsync(userId, today);
            var weekBloodSugar = await _bloodSugarService.GetRecordsByDateRangeAsync(userId, weekStart, today);
            var monthBloodSugar = await _bloodSugarService.GetRecordsByDateRangeAsync(userId, monthStart, today);

            var todayWounds = await _woundService.GetRecordsByDateRangeAsync(userId, today, today);
            var weekWounds = await _woundService.GetRecordsByDateRangeAsync(userId, weekStart, today);
            var monthWounds = await _woundService.GetRecordsByDateRangeAsync(userId, monthStart, today);

            var todayFootPressure = await _footPressureService.GetRecordsByDateRangeAsync(userId, today, today);
            var weekFootPressure = await _footPressureService.GetRecordsByDateRangeAsync(userId, weekStart, today);
            var monthFootPressure = await _footPressureService.GetRecordsByDateRangeAsync(userId, monthStart, today);

            ViewBag.TodayRecordsCount = todayBloodSugar.Count + todayWounds.Count + todayFootPressure.Count;
            ViewBag.RecentWoundsCount = weekBloodSugar.Count + weekWounds.Count + weekFootPressure.Count;
            ViewBag.UnreadMessagesCount = monthBloodSugar.Count + monthWounds.Count + monthFootPressure.Count;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Statistics()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var now = GetBeijingNow();
            var today = now.Date;
            var weekStart = GetWeekStart(today);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var todayBloodSugar = await _bloodSugarService.GetRecordsByDateAsync(userId, today);
            var weekBloodSugar = await _bloodSugarService.GetRecordsByDateRangeAsync(userId, weekStart, today);
            var monthBloodSugar = await _bloodSugarService.GetRecordsByDateRangeAsync(userId, monthStart, today);

            var todayWounds = await _woundService.GetRecordsByDateRangeAsync(userId, today, today);
            var weekWounds = await _woundService.GetRecordsByDateRangeAsync(userId, weekStart, today);
            var monthWounds = await _woundService.GetRecordsByDateRangeAsync(userId, monthStart, today);

            var todayFootPressure = await _footPressureService.GetRecordsByDateRangeAsync(userId, today, today);
            var weekFootPressure = await _footPressureService.GetRecordsByDateRangeAsync(userId, weekStart, today);
            var monthFootPressure = await _footPressureService.GetRecordsByDateRangeAsync(userId, monthStart, today);

            var unreadMessages = await _consultationService.GetUnreadMessagesAsync(userId);
            var todayUnreadMessages = unreadMessages.Count(m => m.CreatedDate.Date == today);
            var weekUnreadMessages = unreadMessages.Count(m => m.CreatedDate.Date >= weekStart && m.CreatedDate.Date <= today);
            var monthUnreadMessages = unreadMessages.Count(m => m.CreatedDate.Date >= monthStart && m.CreatedDate.Date <= today);

            var vm = new DashboardStatisticsViewModel
            {
                ReportTime = now,
                TodayLabel = today.ToString("yyyy-MM-dd"),
                WeekLabel = $"{weekStart:yyyy-MM-dd} 至 {today:yyyy-MM-dd}",
                MonthLabel = $"{monthStart:yyyy-MM-dd} 至 {today:yyyy-MM-dd}",
                Rows =
                {
                    new DashboardStatisticRowViewModel
                    {
                        Name = "血糖记录",
                        TodayCount = todayBloodSugar.Count,
                        WeekCount = weekBloodSugar.Count,
                        MonthCount = monthBloodSugar.Count
                    },
                    new DashboardStatisticRowViewModel
                    {
                        Name = "伤口记录",
                        TodayCount = todayWounds.Count,
                        WeekCount = weekWounds.Count,
                        MonthCount = monthWounds.Count
                    },
                    new DashboardStatisticRowViewModel
                    {
                        Name = "足压记录",
                        TodayCount = todayFootPressure.Count,
                        WeekCount = weekFootPressure.Count,
                        MonthCount = monthFootPressure.Count
                    },
                    new DashboardStatisticRowViewModel
                    {
                        Name = "未读消息",
                        TodayCount = todayUnreadMessages,
                        WeekCount = weekUnreadMessages,
                        MonthCount = monthUnreadMessages
                    }
                }
            };

            vm.TodayTotalCount = vm.Rows.Sum(r => r.TodayCount);
            vm.WeekTotalCount = vm.Rows.Sum(r => r.WeekCount);
            vm.MonthTotalCount = vm.Rows.Sum(r => r.MonthCount);

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string period = "today")
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var now = GetBeijingNow();
            var today = now.Date;
            var weekStart = GetWeekStart(today);
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var normalizedPeriod = (period ?? "today").Trim().ToLowerInvariant();
            DateTime startDate;
            DateTime endDate = today;
            string title;
            string periodLabel;

            switch (normalizedPeriod)
            {
                case "week":
                    startDate = weekStart;
                    title = "本周数据详情";
                    periodLabel = $"{weekStart:yyyy-MM-dd} 至 {today:yyyy-MM-dd}";
                    break;
                case "month":
                    startDate = monthStart;
                    title = "本月数据详情";
                    periodLabel = $"{monthStart:yyyy-MM-dd} 至 {today:yyyy-MM-dd}";
                    break;
                default:
                    startDate = today;
                    title = "今日数据详情";
                    periodLabel = today.ToString("yyyy-MM-dd");
                    break;
            }

            var bloodSugarRecords = await _bloodSugarService.GetRecordsByDateRangeAsync(userId, startDate, endDate);
            var woundRecords = await _woundService.GetRecordsByDateRangeAsync(userId, startDate, endDate);
            var footPressureRecords = await _footPressureService.GetRecordsByDateRangeAsync(userId, startDate, endDate);

            var vm = new DashboardRecordDetailsViewModel
            {
                Title = title,
                PeriodLabel = periodLabel
            };

            // 时间轴：今日=当日记录数，本周=近一周每日记录数，本月=本月每日记录数
            vm.ShowTimeline = true;
            if (normalizedPeriod == "today")
            {
                vm.TimelineHint = "以下为今日您本人的血糖记录、伤口上传、足压记录数量。";
                vm.TimelineLabels = new List<string> { "今日" };
                vm.TimelineBloodSugarCounts = new List<int> { bloodSugarRecords.Count };
                vm.TimelineWoundCounts = new List<int> { woundRecords.Count };
                vm.TimelineFootPressureCounts = new List<int> { footPressureRecords.Count };
            }
            else if (normalizedPeriod == "week")
            {
                vm.TimelineHint = "以下为近一周内每日血糖记录、伤口上传、足压记录数量。";
                var start7 = today.AddDays(-6);
                var weekDays = Enumerable.Range(0, 7).Select(i => start7.AddDays(i)).ToList();
                var bsWeek = await _bloodSugarService.GetRecordsByDateRangeAsync(userId, start7, today);
                var wWeek = await _woundService.GetRecordsByDateRangeAsync(userId, start7, today);
                var fpWeek = await _footPressureService.GetRecordsByDateRangeAsync(userId, start7, today);
                vm.TimelineLabels = weekDays.Select(d => d.ToString("MM-dd")).ToList();
                vm.TimelineBloodSugarCounts = weekDays.Select(d => bsWeek.Count(r => r.RecordDate.Date == d)).ToList();
                vm.TimelineWoundCounts = weekDays.Select(d => wWeek.Count(r => r.RecordDate.Date == d)).ToList();
                vm.TimelineFootPressureCounts = weekDays.Select(d => fpWeek.Count(r => r.RecordDate.Date == d)).ToList();
            }
            else
            {
                vm.TimelineHint = "以下为本月内每日血糖记录、伤口上传、足压记录数量。";
                var monthDays = Enumerable.Range(0, (today - monthStart).Days + 1).Select(i => monthStart.AddDays(i)).ToList();
                var bsMonth = await _bloodSugarService.GetRecordsByDateRangeAsync(userId, monthStart, today);
                var wMonth = await _woundService.GetRecordsByDateRangeAsync(userId, monthStart, today);
                var fpMonth = await _footPressureService.GetRecordsByDateRangeAsync(userId, monthStart, today);
                vm.TimelineLabels = monthDays.Select(d => d.ToString("MM-dd")).ToList();
                vm.TimelineBloodSugarCounts = monthDays.Select(d => bsMonth.Count(r => r.RecordDate.Date == d)).ToList();
                vm.TimelineWoundCounts = monthDays.Select(d => wMonth.Count(r => r.RecordDate.Date == d)).ToList();
                vm.TimelineFootPressureCounts = monthDays.Select(d => fpMonth.Count(r => r.RecordDate.Date == d)).ToList();
            }

            vm.Rows.AddRange(bloodSugarRecords.Select(r => new DashboardRecordDetailRowViewModel
            {
                DataType = "血糖记录",
                RecordDate = r.RecordDate.Date,
                RecordTimeText = r.RecordTime.ToString(@"hh\:mm"),
                Summary = $"{GetBloodSugarMealTypeText(r.MealType)} {r.BloodSugarValueMmol:F2} mmol/L，状态：{GetBloodSugarStatusText(r.Status)}"
            }));

            vm.Rows.AddRange(woundRecords.Select(r => new DashboardRecordDetailRowViewModel
            {
                DataType = "伤口记录",
                RecordDate = r.RecordDate.Date,
                RecordTimeText = r.RecordTime.ToString(@"hh\:mm"),
                Summary = string.IsNullOrWhiteSpace(r.PhotoPath)
                    ? $"伤口状态：{r.GetStatusSummary()}"
                    : $"已上传伤口照片，伤口状态：{r.GetStatusSummary()}"
            }));

            vm.Rows.AddRange(footPressureRecords.Select(r => new DashboardRecordDetailRowViewModel
            {
                DataType = "足压记录",
                RecordDate = r.RecordDate.Date,
                RecordTimeText = r.RecordTime.ToString(@"hh\:mm"),
                Summary = $"左脚：{GetFootPressureText(r.LeftFootPressure, r.LeftFootStatus)}；右脚：{GetFootPressureText(r.RightFootPressure, r.RightFootStatus)}"
            }));

            vm.Rows = vm.Rows
                .OrderByDescending(r => r.RecordDate)
                .ThenByDescending(r => r.RecordTimeText)
                .ToList();

            vm.TotalCount = vm.Rows.Count;

            // 根据时间轴数据生成图表分析文案
            vm.ChartAnalysisText = BuildChartAnalysisText(
                vm.TimelineBloodSugarCounts,
                vm.TimelineWoundCounts,
                vm.TimelineFootPressureCounts);

            return View(vm);
        }

        private static string BuildChartAnalysisText(List<int> bs, List<int> wound, List<int> fp)
        {
            if (bs == null) bs = new List<int>();
            if (wound == null) wound = new List<int>();
            if (fp == null) fp = new List<int>();

            var parts = new List<string>();
            int bsLast = bs.Count > 0 ? bs[bs.Count - 1] : 0;
            int bsFirst = bs.Count > 0 ? bs[0] : 0;
            int woundLast = wound.Count > 0 ? wound[wound.Count - 1] : 0;
            int woundFirst = wound.Count > 0 ? wound[0] : 0;
            int fpLast = fp.Count > 0 ? fp[fp.Count - 1] : 0;
            int fpFirst = fp.Count > 0 ? fp[0] : 0;
            bool isSingleDay = bs.Count == 1 && wound.Count == 1 && fp.Count == 1;

            if (isSingleDay)
            {
                parts.Add(bsLast > 0 ? $"今日血糖记录 {bsLast} 条。" : "今日暂无血糖记录，建议按医嘱监测并记录。");
                parts.Add(woundLast > 0 ? $"今日伤口上传 {woundLast} 条。" : "今日暂无伤口上传。");
                parts.Add(fpLast > 0 ? $"今日足压记录 {fpLast} 条。" : "今日暂无足压记录。");
                return string.Join(" ", parts);
            }

            if (bsLast > 0)
            {
                if (bsLast > bsFirst) parts.Add($"血糖记录在统计周期内共 {bsLast} 条，呈上升趋势，请继续保持规律监测。");
                else if (bsLast == bsFirst && bsLast > 0) parts.Add($"血糖记录在统计周期内共 {bsLast} 条，保持稳定记录。");
                else parts.Add($"统计周期内血糖记录共 {bsLast} 条。");
            }
            else parts.Add("统计周期内暂无血糖记录，建议按医嘱每日监测空腹及餐后血糖并在此记录。");

            if (woundLast > 0)
            {
                if (woundLast > woundFirst) parts.Add($"伤口上传在统计周期内共 {woundLast} 条，已按要求上传。");
                else parts.Add($"统计周期内伤口上传共 {woundLast} 条。");
            }
            else parts.Add("统计周期内暂无伤口上传记录，若有足部伤口请按医嘱定期上传照片便于随访。");

            if (fpLast > 0)
            {
                if (fpLast > fpFirst) parts.Add($"足压记录在统计周期内共 {fpLast} 条，呈上升趋势。");
                else parts.Add($"统计周期内足压记录共 {fpLast} 条。");
            }
            else parts.Add("统计周期内暂无足压记录，建议按计划进行足部压力监测并在此记录。");

            return string.Join(" ", parts);
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId && u.UserType == "Patient");

            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            var model = new PatientProfileViewModel
            {
                UserId = user.UserId,
                Username = user.Username ?? string.Empty,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Gender = user.Gender,
                Age = user.Age,
                ResidenceStatus = user.ResidenceStatus,
                DiabeticFootType = user.DiabeticFootType,
                DiseaseCourse = user.DiseaseCourse,
                DiagnosisDate = user.DiagnosisDate,
                HadUlcerBeforeVisit = user.HadUlcerBeforeVisit,
                IsPostFootSurgeryPatient = user.IsPostFootSurgeryPatient
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(PatientProfileViewModel model)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.UserType == "Patient");
            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            var normalizedEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedEmail))
            {
                var duplicatedEmail = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.UserId != userId && u.Email != null && u.Email.ToLower() == normalizedEmail.ToLower());
                if (duplicatedEmail)
                {
                    ModelState.AddModelError(nameof(model.Email), "该邮箱已被其他账号使用");
                    return View(model);
                }
            }

            user.FullName = string.IsNullOrWhiteSpace(model.FullName) ? user.Username : model.FullName.Trim();
            user.Email = normalizedEmail;
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            user.Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender.Trim();
            user.Age = model.Age;
            user.ResidenceStatus = string.IsNullOrWhiteSpace(model.ResidenceStatus) ? null : model.ResidenceStatus.Trim();
            user.DiabeticFootType = string.IsNullOrWhiteSpace(model.DiabeticFootType) ? null : model.DiabeticFootType.Trim();
            user.DiseaseCourse = string.IsNullOrWhiteSpace(model.DiseaseCourse) ? null : model.DiseaseCourse.Trim();
            user.DiagnosisDate = model.DiagnosisDate?.Date;
            user.HadUlcerBeforeVisit = string.IsNullOrWhiteSpace(model.HadUlcerBeforeVisit) ? null : model.HadUlcerBeforeVisit.Trim();
            user.IsPostFootSurgeryPatient = string.IsNullOrWhiteSpace(model.IsPostFootSurgeryPatient) ? null : model.IsPostFootSurgeryPatient.Trim();

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("FullName", user.FullName ?? user.Username ?? string.Empty);
            TempData["Success"] = "个人信息已更新。";

            return RedirectToAction(nameof(Profile));
        }

        private static DateTime GetBeijingNow()
        {
            var utcNow = DateTime.UtcNow;
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, GetBeijingTimeZone());
        }

        private static DateTime GetWeekStart(DateTime date)
        {
            int daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return date.AddDays(-daysSinceMonday);
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

        private static string GetBloodSugarMealTypeText(string? mealType)
        {
            return mealType switch
            {
                "Fasting" => "空腹",
                "AfterMeal" => "餐后",
                "BeforeMeal" => "餐前",
                _ => mealType ?? "未知"
            };
        }

        private static string GetBloodSugarStatusText(string? status)
        {
            return status switch
            {
                "Normal" => "正常",
                "High" => "偏高",
                "Low" => "偏低",
                _ => status ?? "未知"
            };
        }

        private static string GetFootPressureText(decimal? pressure, string? status)
        {
            if (!pressure.HasValue)
            {
                return "未测量";
            }

            return $"{pressure.Value:F2} kPa（{status ?? "未标记"}）";
        }

        private IActionResult RedirectToPortalByUserType(string? userType)
        {
            if (string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Doctor");
            }

            if (string.Equals(userType, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Admin");
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }
    }
}

