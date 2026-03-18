using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using DiabetesPatientApp.Services;
using DiabetesPatientApp.Models;
using DiabetesPatientApp.ViewModels;

namespace DiabetesPatientApp.Controllers
{
    public class BloodSugarController : Controller
    {
        private readonly IBloodSugarService _bloodSugarService;
        private readonly IConfiguration _configuration;
        private readonly IReportAnalysisService _reportAnalysisService;
        private readonly IHighRiskAlertService _highRiskAlertService;

        public BloodSugarController(IBloodSugarService bloodSugarService, IConfiguration configuration, IReportAnalysisService reportAnalysisService, IHighRiskAlertService highRiskAlertService)
        {
            _bloodSugarService = bloodSugarService;
            _configuration = configuration;
            _reportAnalysisService = reportAnalysisService;
            _highRiskAlertService = highRiskAlertService;
        }

        private int GetUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? date)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            // 默认显示最近 14 天内的记录，方便查看补记的往日数据
            var endDate = DateTime.Now.Date;
            var startDate = endDate.AddDays(-13);
            if (date.HasValue)
            {
                // 若传入 date 参数则只显示该日
                startDate = date.Value.Date;
                endDate = date.Value.Date;
            }
            var records = await _bloodSugarService.GetRecordsByDateRangeAsync(userId, startDate, endDate);
            // 按日期降序、同日内时间降序，最新的在前
            records = records.OrderByDescending(r => r.RecordDate).ThenByDescending(r => r.RecordTime).ToList();
            return View(records);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            DateTime recordDate,
            string recordTime,
            string mealType,
            decimal bloodSugarValue,
            string? stapleFood,
            string? proteinFood,
            string? vegetableFood,
            string? intakeAmount,
            string[]? specialConditions,
            string? medicationDose,
            string? medicationRecord,
            string? exerciseType,
            int? exerciseDuration,
            string? exerciseNote)
        {
            try
            {
                var userId = GetUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                if (!TimeSpan.TryParse(recordTime, out var time))
                {
                    ViewBag.Error = "时间格式错误";
                    return View();
                }

                stapleFood = stapleFood?.Trim();
                proteinFood = proteinFood?.Trim();
                vegetableFood = vegetableFood?.Trim();
                intakeAmount = intakeAmount?.Trim();
                medicationDose = medicationDose?.Trim();
                medicationRecord = medicationRecord?.Trim();
                exerciseType = exerciseType?.Trim();
                exerciseNote = exerciseNote?.Trim();

                var special = (specialConditions ?? Array.Empty<string>())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim())
                    .Distinct()
                    .ToList();

                // “无”表示没有特殊情况（并与其它项互斥）
                if (special.Any(s => string.Equals(s, "无", StringComparison.OrdinalIgnoreCase)))
                {
                    special.Clear();
                }

                var hasMealRecord =
                    !string.IsNullOrWhiteSpace(stapleFood) ||
                    !string.IsNullOrWhiteSpace(proteinFood) ||
                    !string.IsNullOrWhiteSpace(vegetableFood) ||
                    !string.IsNullOrWhiteSpace(intakeAmount) ||
                    special.Count > 0;

                var finalNotes = string.Empty;
                var hasMedication = !string.IsNullOrWhiteSpace(medicationDose) || !string.IsNullOrWhiteSpace(medicationRecord);
                var hasExercise = !string.IsNullOrWhiteSpace(exerciseType) || (exerciseDuration.HasValue && exerciseDuration.Value > 0) || !string.IsNullOrWhiteSpace(exerciseNote);

                if (hasMealRecord || hasMedication || hasExercise)
                {
                    var parts = new List<string>();

                    if (hasMealRecord)
                    {
                        var foodPart = $"食物种类：主食（{stapleFood ?? ""}），蛋白质（{proteinFood ?? ""}），蔬菜（{vegetableFood ?? ""}）";
                        var intakePart = string.IsNullOrWhiteSpace(intakeAmount) ? "" : $"进食量：{intakeAmount}";
                        var specialPart = special.Count == 0 ? "" : $"特殊情况：{string.Join("、", special)}";

                        var mealParts = new List<string> { "进食记录", foodPart };
                        if (!string.IsNullOrEmpty(intakePart)) mealParts.Add(intakePart);
                        if (!string.IsNullOrEmpty(specialPart)) mealParts.Add(specialPart);
                        parts.Add(string.Join("；", mealParts));
                    }

                    if (hasMedication)
                    {
                        var medParts = new List<string> { "用药记录" };
                        if (!string.IsNullOrWhiteSpace(medicationDose)) medParts.Add($"用药剂量：{medicationDose}");
                        if (!string.IsNullOrWhiteSpace(medicationRecord)) medParts.Add($"用药说明：{medicationRecord}");
                        parts.Add(string.Join("；", medParts));
                    }

                    if (hasExercise)
                    {
                        var exParts = new List<string> { "运动记录" };
                        if (!string.IsNullOrWhiteSpace(exerciseType)) exParts.Add($"运动类型：{exerciseType}");
                        if (exerciseDuration.HasValue && exerciseDuration.Value > 0) exParts.Add($"运动时长：{exerciseDuration}分钟");
                        if (!string.IsNullOrWhiteSpace(exerciseNote)) exParts.Add($"运动说明：{exerciseNote}");
                        parts.Add(string.Join("；", exParts));
                    }

                    finalNotes = string.Join("。", parts) + "。";
                }

                var bloodSugarMgDl = BloodSugarRecord.MmolToMgDl(bloodSugarValue);
                var record = await _bloodSugarService.AddRecordAsync(userId, recordDate, time, mealType, bloodSugarMgDl, finalNotes);
                if (record.Status == "High" || record.Status == "Low")
                {
                    var alertType = record.Status == "High" ? "BloodSugarHigh" : "BloodSugarLow";
                    var summary = record.Status == "High" ? $"高血糖 {record.BloodSugarValueMmol} mmol/L" : $"低血糖 {record.BloodSugarValueMmol} mmol/L";
                    await _highRiskAlertService.NotifyAsync(userId, alertType, summary, record.RecordId, "BloodSugar");
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var record = await _bloodSugarService.GetRecordByIdAsync(userId, id);
            if (record == null) return NotFound();

            ParseNotesToViewBag(record.Notes);
            return View(record);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(
            int recordId,
            DateTime recordDate,
            string recordTime,
            string mealType,
            decimal bloodSugarValue,
            string? stapleFood,
            string? proteinFood,
            string? vegetableFood,
            string? intakeAmount,
            string[]? specialConditions,
            string? medicationDose,
            string? medicationRecord,
            string? exerciseType,
            int? exerciseDuration,
            string? exerciseNote)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var existing = await _bloodSugarService.GetRecordByIdAsync(userId, recordId);
            if (existing == null) return NotFound();

            if (!TimeSpan.TryParse(recordTime, out var time))
            {
                ViewBag.Error = "时间格式错误";
                ParseNotesToViewBag(existing.Notes);
                return View(existing);
            }

            mealType = mealType?.Trim() ?? "";
            if (string.IsNullOrEmpty(mealType))
            {
                ViewBag.Error = "请选择餐次";
                ParseNotesToViewBag(existing.Notes);
                return View(existing);
            }

            stapleFood = stapleFood?.Trim();
            proteinFood = proteinFood?.Trim();
            vegetableFood = vegetableFood?.Trim();
            intakeAmount = intakeAmount?.Trim();
            medicationDose = medicationDose?.Trim();
            medicationRecord = medicationRecord?.Trim();
            exerciseType = exerciseType?.Trim();
            exerciseNote = exerciseNote?.Trim();
            var special = (specialConditions ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct()
                .ToList();
            if (special.Any(s => string.Equals(s, "无", StringComparison.OrdinalIgnoreCase)))
                special.Clear();

            var hasMealRecord =
                !string.IsNullOrWhiteSpace(stapleFood) ||
                !string.IsNullOrWhiteSpace(proteinFood) ||
                !string.IsNullOrWhiteSpace(vegetableFood) ||
                !string.IsNullOrWhiteSpace(intakeAmount) ||
                special.Count > 0;
            var hasMedication = !string.IsNullOrWhiteSpace(medicationDose) || !string.IsNullOrWhiteSpace(medicationRecord);
            var hasExercise = !string.IsNullOrWhiteSpace(exerciseType) || (exerciseDuration.HasValue && exerciseDuration.Value > 0) || !string.IsNullOrWhiteSpace(exerciseNote);
            var finalNotes = string.Empty;
            if (hasMealRecord || hasMedication || hasExercise)
            {
                var parts = new List<string>();
                if (hasMealRecord)
                {
                    var foodPart = $"食物种类：主食（{stapleFood ?? ""}），蛋白质（{proteinFood ?? ""}），蔬菜（{vegetableFood ?? ""}）";
                    var intakePart = string.IsNullOrWhiteSpace(intakeAmount) ? "" : $"进食量：{intakeAmount}";
                    var specialPart = special.Count == 0 ? "" : $"特殊情况：{string.Join("、", special)}";
                    var mealParts = new List<string> { "进食记录", foodPart };
                    if (!string.IsNullOrEmpty(intakePart)) mealParts.Add(intakePart);
                    if (!string.IsNullOrEmpty(specialPart)) mealParts.Add(specialPart);
                    parts.Add(string.Join("；", mealParts));
                }
                if (hasMedication)
                {
                    var medParts = new List<string> { "用药记录" };
                    if (!string.IsNullOrWhiteSpace(medicationDose)) medParts.Add($"用药剂量：{medicationDose}");
                    if (!string.IsNullOrWhiteSpace(medicationRecord)) medParts.Add($"用药说明：{medicationRecord}");
                    parts.Add(string.Join("；", medParts));
                }
                if (hasExercise)
                {
                    var exParts = new List<string> { "运动记录" };
                    if (!string.IsNullOrWhiteSpace(exerciseType)) exParts.Add($"运动类型：{exerciseType}");
                    if (exerciseDuration.HasValue && exerciseDuration.Value > 0) exParts.Add($"运动时长：{exerciseDuration}分钟");
                    if (!string.IsNullOrWhiteSpace(exerciseNote)) exParts.Add($"运动说明：{exerciseNote}");
                    parts.Add(string.Join("；", exParts));
                }
                finalNotes = string.Join("。", parts) + "。";
            }

            try
            {
                var bloodSugarMgDl = BloodSugarRecord.MmolToMgDl(bloodSugarValue);
                var status = BloodSugarRecord.DetermineStatus(bloodSugarMgDl, mealType);
                await _bloodSugarService.UpdateRecordAsync(recordId, recordDate.Date, time, mealType, bloodSugarMgDl, finalNotes);
                if (status == "High" || status == "Low")
                {
                    var alertType = status == "High" ? "BloodSugarHigh" : "BloodSugarLow";
                    var mmol = Math.Round(bloodSugarValue, 2, MidpointRounding.AwayFromZero);
                    var summary = status == "High" ? $"高血糖 {mmol} mmol/L" : $"低血糖 {mmol} mmol/L";
                    await _highRiskAlertService.NotifyAsync(userId, alertType, summary, recordId, "BloodSugar");
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                existing.RecordDate = recordDate.Date;
                existing.RecordTime = time;
                existing.MealType = mealType;
                existing.Notes = finalNotes;
                existing.BloodSugarValue = BloodSugarRecord.MmolToMgDl(bloodSugarValue);
                ViewBag.StapleFood = stapleFood;
                ViewBag.ProteinFood = proteinFood;
                ViewBag.VegetableFood = vegetableFood;
                ViewBag.IntakeAmount = intakeAmount;
                ViewBag.SpecialConditions = special;
                ViewBag.MedicationDose = medicationDose;
                ViewBag.MedicationRecord = medicationRecord;
                ViewBag.ExerciseType = exerciseType;
                ViewBag.ExerciseDuration = exerciseDuration?.ToString() ?? "";
                ViewBag.ExerciseNote = exerciseNote;
                return View(existing);
            }
        }

        private void ParseNotesToViewBag(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return;
            var n = notes;
            ViewBag.StapleFood = Regex.Match(n, @"主食（([^）]*)）").Groups[1].Value.Trim();
            ViewBag.ProteinFood = Regex.Match(n, @"蛋白质（([^）]*)）").Groups[1].Value.Trim();
            ViewBag.VegetableFood = Regex.Match(n, @"蔬菜（([^）]*)）").Groups[1].Value.Trim();
            ViewBag.IntakeAmount = Regex.Match(n, @"进食量：([^；。]*)").Groups[1].Value.Trim();
            var specialStr = Regex.Match(n, @"特殊情况：([^。]*)").Groups[1].Value.Trim();
            ViewBag.SpecialConditions = string.IsNullOrWhiteSpace(specialStr)
                ? new List<string>()
                : specialStr.Split('、', '，').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s) && !string.Equals(s, "无", StringComparison.OrdinalIgnoreCase)).ToList();
            ViewBag.MedicationDose = Regex.Match(n, @"用药剂量：([^；]*)").Groups[1].Value.Trim();
            ViewBag.MedicationRecord = Regex.Match(n, @"用药说明：([^。]*)").Groups[1].Value.Trim();
            ViewBag.ExerciseType = Regex.Match(n, @"运动类型：([^；]*)").Groups[1].Value.Trim();
            var durationStr = Regex.Match(n, @"运动时长：(\d+)分钟").Groups[1].Value.Trim();
            ViewBag.ExerciseDuration = string.IsNullOrEmpty(durationStr) ? "" : durationStr;
            ViewBag.ExerciseNote = Regex.Match(n, @"运动说明：([^。]*)").Groups[1].Value.Trim();
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int recordId)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            await _bloodSugarService.DeleteRecordAsync(recordId);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Trend(int days = 30)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var records = await _bloodSugarService.GetTrendDataAsync(userId, days);
            ViewBag.Days = days;
            return View(records);
        }

        [HttpGet]
        public async Task<IActionResult> Report(int days = 30)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");
            var vm = await BuildReportViewModelAsync(userId, days);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> SaveReportToFolder(int days = 30)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var vm = await BuildReportViewModelAsync(userId, days);
            var folderPath = _configuration["ReportSaveFolder"];
            if (string.IsNullOrWhiteSpace(folderPath))
                folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "血糖分析报告");

            try
            {
                Directory.CreateDirectory(folderPath);
                var fileName = $"血糖分析报告_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                var fullPath = Path.Combine(folderPath, fileName);
                var html = BuildReportHtml(vm);
                await System.IO.File.WriteAllTextAsync(fullPath, html, new UTF8Encoding(true));
                TempData["ReportSavePath"] = fullPath;
                TempData["ReportSaveSuccess"] = true;
            }
            catch (Exception ex)
            {
                TempData["ReportSaveError"] = ex.Message;
            }
            return RedirectToAction("Report", new { days });
        }

        private static string H(string? s) => string.IsNullOrEmpty(s) ? "—" : WebUtility.HtmlEncode(s);

        private static string BuildReportHtml(BloodSugarReportViewModel vm)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>血糖分析报告</title></head><body style=\"font-family:Microsoft YaHei,sans-serif;padding:20px;\">");
            sb.Append($"<h2 style=\"text-align:center;\">血糖分析报告</h2>");
            sb.Append($"<p style=\"text-align:center;color:#666;\">报告生成时间：{vm.ReportDate:yyyy-MM-dd HH:mm}</p>");
            sb.Append("<table border=\"1\" cellpadding=\"8\" cellspacing=\"0\" style=\"border-collapse:collapse;width:100%;max-width:210mm;\">");
            sb.Append("<tr><td colspan=\"4\" style=\"background:#f0f0f0;font-weight:bold;\">一、患者基本信息</td></tr>");
            sb.Append($"<tr><td style=\"width:100px;background:#f8f9fa;\">姓名</td><td>{H(vm.PatientName)}</td><td style=\"width:90px;background:#f8f9fa;\">年龄</td><td>{H(vm.Age?.ToString())}</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">性别</td><td>{H(vm.Gender)}</td><td style=\"background:#f8f9fa;\">电话</td><td>{H(vm.PhoneNumber)}</td></tr>");
            if (!string.IsNullOrWhiteSpace(vm.Email))
                sb.Append($"<tr><td style=\"background:#f8f9fa;\">邮箱</td><td colspan=\"3\">{H(vm.Email)}</td></tr>");
            sb.Append("<tr><td colspan=\"4\" style=\"background:#f0f0f0;font-weight:bold;\">二、血糖监测概况</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">监测时段</td><td>{H(vm.MonitoringPeriodText)}</td><td style=\"background:#f8f9fa;\">总次数</td><td>{vm.TotalCount} 次</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">频率</td><td colspan=\"3\">{H(vm.FrequencyText)}</td></tr>");
            sb.Append("<tr><td colspan=\"4\" style=\"background:#f0f0f0;font-weight:bold;\">三、血糖控制达标率</td></tr>");
            if (!string.IsNullOrWhiteSpace(vm.TrendAnalysis))
                sb.Append($"<tr><td colspan=\"4\" style=\"background:#fff;\">{H(vm.TrendAnalysis)}</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">空腹血糖达标率</td><td colspan=\"3\">{H(vm.FastingComplianceText)}</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">餐后血糖达标率</td><td colspan=\"3\">{H(vm.AfterMealComplianceText)}</td></tr>");
            sb.Append("<tr><td colspan=\"4\" style=\"background:#f0f0f0;font-weight:bold;\">四、相关风险</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">高血糖感染风险</td><td colspan=\"3\">{H(vm.HyperglycemiaRiskText)}</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">低血糖跌倒风险</td><td colspan=\"3\">{H(vm.HypoglycemiaRiskText)}</td></tr>");
            sb.Append("<tr><td colspan=\"4\" style=\"background:#f0f0f0;font-weight:bold;\">五、建议</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">饮食建议</td><td colspan=\"3\">{H(vm.DietSuggestion)}</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">药物建议</td><td colspan=\"3\">{H(vm.MedicationSuggestion)}</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">运动建议</td><td colspan=\"3\">{H(vm.ExerciseSuggestion)}</td></tr>");
            sb.Append($"<tr><td style=\"background:#f8f9fa;\">监测次数建议</td><td colspan=\"3\">{H(vm.MonitoringSuggestion)}</td></tr>");
            sb.Append("</table></body></html>");
            return sb.ToString();
        }

        private async Task<BloodSugarReportViewModel> BuildReportViewModelAsync(int userId, int days)
        {
            var user = await _bloodSugarService.GetUserByIdAsync(userId);
            var records = await _bloodSugarService.GetTrendDataAsync(userId, days);

            var vm = new BloodSugarReportViewModel
            {
                ReportDate = DateTime.Now,
                Days = days,
                PatientName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : user?.Username,
                Gender = user?.Gender,
                Age = user?.DateOfBirth.HasValue == true ? (int?)((DateTime.Now - user.DateOfBirth!.Value).TotalDays / 365.25) : null,
                PhoneNumber = user?.PhoneNumber,
                Email = user?.Email,
                MonitoringPeriodText = $"最近{days}天",
                TotalCount = records.Count,
                FrequencyText = days > 0 && records.Count > 0
                    ? $"日均 {(decimal)records.Count / days:F1} 次"
                    : (records.Count == 0 ? "0 次" : "—")
            };

            var fasting = records.Where(r => string.Equals(r.MealType, "Fasting", StringComparison.OrdinalIgnoreCase)).ToList();
            var afterMeal = records.Where(r => string.Equals(r.MealType, "AfterMeal", StringComparison.OrdinalIgnoreCase)).ToList();
            const decimal fastingLow = 4.4m, fastingHigh = 7.0m, afterMealLow = 6.0m, afterMealHigh = 10.0m;

            if (fasting.Count > 0)
            {
                var inRange = fasting.Count(r => r.BloodSugarValueMmol >= fastingLow && r.BloodSugarValueMmol <= fastingHigh);
                vm.FastingComplianceRate = (decimal)inRange / fasting.Count * 100;
                vm.FastingComplianceText = $"{vm.FastingComplianceRate:F0}%（达标 {inRange}/{fasting.Count} 次，正常范围 {fastingLow}～{fastingHigh} mmol/L）";
            }
            else
                vm.FastingComplianceText = "暂无空腹血糖记录";

            if (afterMeal.Count > 0)
            {
                var inRange = afterMeal.Count(r => r.BloodSugarValueMmol >= afterMealLow && r.BloodSugarValueMmol <= afterMealHigh);
                vm.AfterMealComplianceRate = (decimal)inRange / afterMeal.Count * 100;
                vm.AfterMealComplianceText = $"{vm.AfterMealComplianceRate:F0}%（达标 {inRange}/{afterMeal.Count} 次，正常范围 {afterMealLow}～{afterMealHigh} mmol/L）";
            }
            else
                vm.AfterMealComplianceText = "暂无餐后血糖记录";

            var totalHigh = fasting.Count(r => r.BloodSugarValueMmol > fastingHigh) + afterMeal.Count(r => r.BloodSugarValueMmol > afterMealHigh);
            var totalLow = fasting.Count(r => r.BloodSugarValueMmol < fastingLow) + afterMeal.Count(r => r.BloodSugarValueMmol < afterMealLow);

            var context = new ReportAnalysisContext
            {
                PatientName = vm.PatientName,
                Age = vm.Age,
                Gender = vm.Gender,
                Days = days,
                TotalCount = records.Count,
                FrequencyText = vm.FrequencyText,
                FastingCount = fasting.Count,
                FastingAvg = fasting.Count > 0 ? fasting.Average(r => r.BloodSugarValueMmol) : 0,
                FastingMin = fasting.Count > 0 ? fasting.Min(r => r.BloodSugarValueMmol) : 0,
                FastingMax = fasting.Count > 0 ? fasting.Max(r => r.BloodSugarValueMmol) : 0,
                FastingComplianceRate = vm.FastingComplianceRate,
                FastingHighCount = fasting.Count(r => r.BloodSugarValueMmol > fastingHigh),
                FastingLowCount = fasting.Count(r => r.BloodSugarValueMmol < fastingLow),
                AfterMealCount = afterMeal.Count,
                AfterMealAvg = afterMeal.Count > 0 ? afterMeal.Average(r => r.BloodSugarValueMmol) : 0,
                AfterMealMin = afterMeal.Count > 0 ? afterMeal.Min(r => r.BloodSugarValueMmol) : 0,
                AfterMealMax = afterMeal.Count > 0 ? afterMeal.Max(r => r.BloodSugarValueMmol) : 0,
                AfterMealComplianceRate = vm.AfterMealComplianceRate,
                AfterMealHighCount = afterMeal.Count(r => r.BloodSugarValueMmol > afterMealHigh),
                AfterMealLowCount = afterMeal.Count(r => r.BloodSugarValueMmol < afterMealLow),
                TotalHighCount = totalHigh,
                TotalLowCount = totalLow,
                SampleNotes = records.Where(r => !string.IsNullOrWhiteSpace(r.Notes)).Select(r => r.Notes!).Take(10).ToList(),
                SampleRecordLines = records
                    .OrderByDescending(r => r.RecordDate)
                    .ThenByDescending(r => r.RecordTime)
                    .Take(20)
                    .Select(r => $"{r.RecordDate:yyyy-MM-dd} {r.RecordTime.Hours:D2}:{r.RecordTime.Minutes:D2}｜{GetBloodSugarMealTypeText(r.MealType)}｜{r.BloodSugarValueMmol:F2} mmol/L｜状态：{GetBloodSugarStatusText(r.Status)}｜备注：{(string.IsNullOrWhiteSpace(r.Notes) ? "无" : r.Notes!.Trim())}")
                    .ToList()
            };

            var aiResult = await _reportAnalysisService.GenerateAnalysisAsync(context);
            if (aiResult != null)
            {
                vm.TrendAnalysis = aiResult.TrendAnalysis;
                vm.HyperglycemiaRiskText = aiResult.HyperglycemiaRiskText;
                vm.HypoglycemiaRiskText = aiResult.HypoglycemiaRiskText;
                vm.DietSuggestion = aiResult.DietSuggestion;
                vm.MedicationSuggestion = aiResult.MedicationSuggestion;
                vm.ExerciseSuggestion = aiResult.ExerciseSuggestion;
                vm.MonitoringSuggestion = aiResult.MonitoringSuggestion;
            }
            else
            {
                if (records.Count == 0)
                {
                    vm.HyperglycemiaRiskText = "暂无监测数据，无法评估。建议开始规律监测后再次生成报告。";
                    vm.HypoglycemiaRiskText = "暂无监测数据，无法评估。建议开始规律监测后再次生成报告。";
                }
                else
                {
                    if (totalHigh >= records.Count * 0.3m) vm.HyperglycemiaRiskText = "偏高。近期高血糖比例较高，感染及并发症风险增加，建议严格控制饮食、规律用药并复诊评估。";
                    else if (totalHigh > 0) vm.HyperglycemiaRiskText = "需关注。存在高血糖记录，建议加强监测、调整饮食与用药，必要时就医。";
                    else vm.HyperglycemiaRiskText = "较低。本周期内未出现明显高血糖，请继续保持。";
                    if (totalLow >= 3) vm.HypoglycemiaRiskText = "偏高。低血糖次数较多，跌倒与晕厥风险增加，建议避免空腹运动、随身携带糖果、与医生沟通调整用药。";
                    else if (totalLow > 0) vm.HypoglycemiaRiskText = "需关注。偶有低血糖，注意规律进餐、避免空腹过久，运动前适当加餐。";
                    else vm.HypoglycemiaRiskText = "较低。本周期内未出现低血糖记录，请继续保持。";
                }

                var hasMealNotes = records.Any(r => !string.IsNullOrEmpty(r.Notes) && r.Notes.Contains("进食记录"));
                vm.DietSuggestion = hasMealNotes
                    ? "根据您的进食记录，建议保持主食（米饭/面条/馒头）适量，每餐搭配足量蛋白质与蔬菜，进食量尽量规律；避免暴饮暴食或长期偏少，若有拒食、偏少或空腹过久情况请与医生沟通。"
                    : "建议在测血糖时记录主食、蛋白质与蔬菜种类及进食量，便于分析饮食对血糖的影响并获个性化饮食建议。";
                var hasMedNotes = records.Any(r => !string.IsNullOrEmpty(r.Notes) && r.Notes.Contains("用药记录"));
                vm.MedicationSuggestion = hasMedNotes
                    ? "请继续按医嘱规律用药，注意剂量与时间；若出现低血糖或持续偏高请及时复诊。本报告不能替代医嘱，具体用药以医生指导为准。"
                    : "建议记录每次用药剂量与时间，便于复诊时医生评估疗效与安全性并调整方案。";
                if (vm.FastingComplianceRate >= 80 && vm.AfterMealComplianceRate >= 80)
                    vm.ExerciseSuggestion = "血糖控制较平稳，可维持当前运动习惯，建议每周至少150分钟中等强度有氧运动，运动前测血糖、避免空腹剧烈运动。";
                else if (totalLow > 0)
                    vm.ExerciseSuggestion = "存在低血糖风险，运动前请测血糖并适当加餐，避免空腹或长时间运动，随身携带糖果。";
                else
                    vm.ExerciseSuggestion = "建议规律进行中等强度有氧运动（如快走、骑车），有助于改善血糖；运动前后可测血糖以便掌握规律。";
                if (records.Count == 0)
                    vm.MonitoringSuggestion = "本周期内无监测记录。建议至少每日监测空腹及餐后血糖，共约每日2次以上，以便评估控制情况。";
                else if ((decimal)records.Count / Math.Max(1, days) < 1.5m)
                    vm.MonitoringSuggestion = $"当前监测频率偏低（{vm.FrequencyText}）。建议增加至每日至少空腹1次、餐后1～2次，便于更好评估血糖控制与调整治疗。";
                else
                    vm.MonitoringSuggestion = $"当前监测频率（{vm.FrequencyText}）较合理，建议继续保持并记录完整，便于复诊时医生评估。";
            }

            return vm;
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
    }
}

