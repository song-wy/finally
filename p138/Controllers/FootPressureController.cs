using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using DiabetesPatientApp.Models;
using DiabetesPatientApp.Services;

namespace DiabetesPatientApp.Controllers
{
    public class FootPressureController : Controller
    {
        private readonly IFootPressureService _footPressureService;
        private readonly IFootPressureSuggestionService _suggestionService;
        private readonly IHighRiskAlertService _highRiskAlertService;

        public FootPressureController(IFootPressureService footPressureService, IFootPressureSuggestionService suggestionService, IHighRiskAlertService highRiskAlertService)
        {
            _footPressureService = footPressureService;
            _suggestionService = suggestionService;
            _highRiskAlertService = highRiskAlertService;
        }

        private int GetUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        // 足部压力记录列表
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var records = await _footPressureService.GetLatestRecordsAsync(userId, 50);
            return View(records);
        }

        // 新增足部压力记录
        public IActionResult Create()
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var now = GetBeijingNow();
            ViewBag.DefaultRecordDate = now.ToString("yyyy-MM-dd");
            ViewBag.DefaultRecordTime = now.ToString("HH:mm");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(DateTime recordDate, string recordTime, 
            decimal? leftFootPressure, string leftFootStatus, 
            decimal? rightFootPressure, string rightFootStatus,
            string walkingDuration, string notes)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            try
            {
                if (!TimeSpan.TryParse(recordTime, out var time))
                {
                    ModelState.AddModelError("", "时间格式错误");
                    return View();
                }

                walkingDuration = (walkingDuration ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(walkingDuration))
                {
                    var prefix = $"行走时长：{walkingDuration}分钟";
                    notes = string.IsNullOrWhiteSpace(notes) ? prefix : $"{prefix}；{notes}";
                }

                var record = await _footPressureService.AddRecordAsync(userId, recordDate, time,
                    leftFootPressure, leftFootStatus,
                    rightFootPressure, rightFootStatus,
                    notes);

                if (leftFootStatus == "高风险" || leftFootStatus == "极高风险" || rightFootStatus == "高风险" || rightFootStatus == "极高风险")
                {
                    var summary = $"左脚{leftFootStatus}，右脚{rightFootStatus}";
                    await _highRiskAlertService.NotifyAsync(userId, "FootPressureHigh", summary, record.FootPressureId, "FootPressure");
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"保存失败: {ex.Message}");
                return View();
            }
        }

        // 编辑足部压力记录
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var record = await _footPressureService.GetRecordByIdAsync(id);
            if (record == null || record.UserId != userId)
                return NotFound();

            ViewBag.WalkingDuration = ExtractWalkingDuration(record.Notes);
            return View(record);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, DateTime recordDate, string recordTime,
            decimal? leftFootPressure, string leftFootStatus,
            decimal? rightFootPressure, string rightFootStatus,
            string walkingDuration, string notes)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            try
            {
                var record = await _footPressureService.GetRecordByIdAsync(id);
                if (record == null || record.UserId != userId)
                    return NotFound();

                if (!TimeSpan.TryParse(recordTime, out var time))
                {
                    ModelState.AddModelError("", "时间格式错误");
                    ViewBag.WalkingDuration = walkingDuration;
                    return View(record);
                }

                notes = UpsertWalkingDuration(notes, walkingDuration);

                await _footPressureService.UpdateRecordAsync(id, recordDate, time,
                    leftFootPressure, leftFootStatus,
                    rightFootPressure, rightFootStatus,
                    notes);

                if (leftFootStatus == "高风险" || leftFootStatus == "极高风险" || rightFootStatus == "高风险" || rightFootStatus == "极高风险")
                {
                    var summary = $"左脚{leftFootStatus}，右脚{rightFootStatus}";
                    await _highRiskAlertService.NotifyAsync(record.UserId, "FootPressureHigh", summary, id, "FootPressure");
                }

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"更新失败: {ex.Message}");
                return View();
            }
        }

        private static string ExtractWalkingDuration(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return "";
            var m = Regex.Match(notes, @"行走时长：\s*(\d+)\s*分钟");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static string UpsertWalkingDuration(string? notes, string? walkingDuration)
        {
            var cleanNotes = (notes ?? "").Trim();
            cleanNotes = Regex.Replace(cleanNotes, @"(^|[；;]\s*)行走时长：\s*\d+\s*分钟\s*", "$1").Trim();
            cleanNotes = cleanNotes.Trim('；', ';', ' ');

            walkingDuration = (walkingDuration ?? "").Trim();
            if (string.IsNullOrWhiteSpace(walkingDuration)) return cleanNotes;

            var prefix = $"行走时长：{walkingDuration}分钟";
            return string.IsNullOrWhiteSpace(cleanNotes) ? prefix : $"{prefix}；{cleanNotes}";
        }

        // 删除足部压力记录
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            try
            {
                var record = await _footPressureService.GetRecordByIdAsync(id);
                if (record == null || record.UserId != userId)
                    return NotFound();

                await _footPressureService.DeleteRecordAsync(id);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"删除失败: {ex.Message}");
                return RedirectToAction("Index");
            }
        }

        // 查看详情（报告单，需加载患者信息）
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var record = await _footPressureService.GetRecordWithUserByIdAsync(id);
            if (record == null || record.UserId != userId)
                return NotFound();

            return View(record);
        }

        /// <summary>
        /// 获取建议：AI 分析该条足部压力数据，生成日常行为、随访、减压鞋垫建议单
        /// </summary>
        public async Task<IActionResult> Suggestion(int id)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var record = await _footPressureService.GetRecordWithUserByIdAsync(id);
            if (record == null || record.UserId != userId)
                return NotFound();

            var recent = await _footPressureService.GetLatestRecordsAsync(userId, 10);
            var suggestion = await _suggestionService.GenerateSuggestionAsync(record, recent);
            return View(suggestion);
        }

        /// <summary>
        /// 保存建议单至桌面“足压检测报告单”文件夹
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveSuggestionToFolder(int recordId)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            try
            {
                var record = await _footPressureService.GetRecordWithUserByIdAsync(recordId);
                if (record == null || record.UserId != userId)
                    return NotFound();

                var recent = await _footPressureService.GetLatestRecordsAsync(userId, 10);
                var model = await _suggestionService.GenerateSuggestionAsync(record, recent);

                string folderPath;
                string folderDescription;
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (!string.IsNullOrWhiteSpace(desktop))
                {
                    folderPath = Path.Combine(desktop, "足压检测报告单");
                    folderDescription = "桌面\\足压检测报告单";
                }
                else
                {
                    folderPath = Path.Combine(Directory.GetCurrentDirectory(), "足压检测报告单");
                    folderDescription = "程序目录\\足压检测报告单";
                }

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string BadgeStyle(string? status)
                {
                    if (string.IsNullOrEmpty(status)) return "background:#6c757d;color:#fff;";
                    return status switch
                    {
                        "低风险" => "background:#198754;color:#fff;",
                        "中风险" => "background:#ffc107;color:#000;",
                        "高风险" => "background:#fd7e14;color:#fff;",
                        "极高风险" => "background:#dc3545;color:#fff;",
                        _ => "background:#6c757d;color:#fff;"
                    };
                }

                string leftBadge = string.IsNullOrEmpty(model.LeftStatus) ? "" : $"<span style=\"padding:2px 8px;border-radius:4px;font-size:12px;{BadgeStyle(model.LeftStatus)}\">{WebUtility.HtmlEncode(model.LeftStatus)}</span>";
                string rightBadge = string.IsNullOrEmpty(model.RightStatus) ? "" : $"<span style=\"padding:2px 8px;border-radius:4px;font-size:12px;{BadgeStyle(model.RightStatus)}\">{WebUtility.HtmlEncode(model.RightStatus)}</span>";

                string fileName = $"足压检测报告单_{recordId}_{model.GeneratedAt:yyyyMMdd_HHmm}.html";
                string filePath = Path.Combine(folderPath, fileName);

                string html = $@"<!DOCTYPE html>
<html><head><meta charset=""utf-8""><title>足部压力检测报告单</title>
<style>
.report-card {{ max-width: 720px; margin: 0 auto; padding: 24px; }}
.report-title {{ font-size: 20px; font-weight: 600; text-align: center; margin-bottom: 20px; }}
.report-section {{ margin-bottom: 22px; }}
.report-section-title {{ font-size: 15px; font-weight: 600; color: #0b3a66; margin-bottom: 10px; padding-bottom: 6px; border-bottom: 2px solid #0b3a66; }}
.suggestion-content {{ font-size: 14px; line-height: 1.85; color: #333; white-space: pre-line; }}
.summary-row {{ margin-bottom: 16px; }}
.summary-item {{ font-size: 14px; margin-right: 24px; }}
.generated-at {{ font-size: 12px; color: #6c757d; margin-top: 8px; }}
</style></head><body class=""report-card"">
<div class=""report-title"">患者足部压力检测报告单</div>
<div class=""report-section""><div class=""report-section-title"">患者与检测概要</div>
<div class=""summary-row"">
<span class=""summary-item""><strong>姓名：</strong>{WebUtility.HtmlEncode(model.PatientName ?? "—")}</span>
<span class=""summary-item""><strong>年龄：</strong>{model.Age?.ToString() ?? "—"} 岁</span>
<span class=""summary-item""><strong>左脚：</strong>{model.LeftPressure?.ToString("F2") ?? "—"} kPa {leftBadge}</span>
<span class=""summary-item""><strong>右脚：</strong>{model.RightPressure?.ToString("F2") ?? "—"} kPa {rightBadge}</span>
</div>
<p class=""generated-at"">建议生成时间：{model.GeneratedAt:yyyy-MM-dd HH:mm}</p>
</div>
<div class=""report-section""><div class=""report-section-title"">一、日常行为建议</div>
<div class=""suggestion-content"">{WebUtility.HtmlEncode(model.DailyBehaviorAdvice)}</div></div>
<div class=""report-section""><div class=""report-section-title"">二、随访建议</div>
<div class=""suggestion-content"">{WebUtility.HtmlEncode(model.FollowUpAdvice)}</div></div>
<div class=""report-section""><div class=""report-section-title"">三、减压鞋垫选择建议</div>
<div class=""suggestion-content"">{WebUtility.HtmlEncode(model.InsoleAdvice)}</div></div>
</body></html>";

                await System.IO.File.WriteAllTextAsync(filePath, html);

                TempData["SaveToFolderMessage"] = $"已保存至{folderDescription}\\{fileName}";
                return RedirectToAction("Suggestion", new { id = recordId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "保存至文件夹失败：" + ex.Message;
                return RedirectToAction("Suggestion", new { id = recordId });
            }
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
    }
}

