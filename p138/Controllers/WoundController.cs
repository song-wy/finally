using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using DiabetesPatientApp.Services;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.Controllers
{
    public class WoundController : Controller
    {
        private readonly IWoundService _woundService;
        private readonly IWoundImageAnalysisService _woundImageAnalysisService;
        private readonly IHighRiskAlertService _highRiskAlertService;
        private readonly ISystemSettingsService _systemSettings;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;

        public WoundController(IWoundService woundService, IWoundImageAnalysisService woundImageAnalysisService, IHighRiskAlertService highRiskAlertService, ISystemSettingsService systemSettings, IWebHostEnvironment env, IConfiguration configuration)
        {
            _woundService = woundService;
            _woundImageAnalysisService = woundImageAnalysisService;
            _highRiskAlertService = highRiskAlertService;
            _systemSettings = systemSettings;
            _env = env;
            _configuration = configuration;
        }

        private int GetUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var start = startDate ?? DateTime.Now.AddMonths(-1);
            var end = endDate ?? DateTime.Now;
            var records = await _woundService.GetRecordsByDateRangeAsync(userId, start, end);
            
            ViewBag.StartDate = start;
            ViewBag.EndDate = end;
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
        public async Task<IActionResult> Create(IFormFile photoFile)
        {
            try
            {
                var userId = GetUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                if (photoFile == null || photoFile.Length == 0)
                {
                    ViewBag.Error = "请选择要上传的伤口照片";
                    return View();
                }

                var maxMb = await _systemSettings.GetIntAsync(SystemSettingKeys.MaxUploadMB, 30);
                if (maxMb > 0 && photoFile.Length > (long)maxMb * 1024L * 1024L)
                {
                    ViewBag.Error = $"上传失败：文件过大，单个文件请 ≤ {maxMb}MB。";
                    return View();
                }

                var now = GetBeijingNow();
                var record = await _woundService.AddRecordWithPhotoAsync(userId, now.Date, now.TimeOfDay, null,
                    false, false, false, false, photoFile, "");

                TempData["WoundMessage"] = "伤口照片已保存，页面将根据上传的图片自动进行 AI 分析。";
                return RedirectToAction("Edit", new { id = record.WoundId });
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

            var record = await _woundService.GetRecordAsync(id);
            if (record == null || record.UserId != userId)
                return NotFound();

            // 根据患者提交的伤口照片进行 AI 分析，供报告页面显示
            WoundImageAnalysisResult? aiResult = null;
            string? aiError = null;
            if (string.IsNullOrEmpty(record.PhotoPath))
            {
                aiError = "未上传伤口照片，请先在伤口监护中上传照片并保存。";
            }
            else
            {
                // 伤口图片分析使用独立视觉配置（VisionAI），不改变现有 DeepSeek（OpenAI 节点）文本分析配置
                var apiKey = _configuration["VisionAI:ApiKey"]?.Trim();
                var baseUrl = _configuration["VisionAI:BaseUrl"]?.Trim() ?? "";
                var model = _configuration["VisionAI:Model"]?.Trim();

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    aiError = "未配置视觉 AI 密钥（VisionAI:ApiKey 为空），因此无法进行 AI 伤口图片分析。";
                }
                else if (string.IsNullOrWhiteSpace(model))
                {
                    aiError = "未配置视觉模型名（VisionAI:Model 为空），因此无法进行 AI 伤口图片分析。";
                }
                else if (baseUrl.Contains("deepseek", StringComparison.OrdinalIgnoreCase))
                {
                    // 当前伤口分析走“图片输入（image_url）”的 OpenAI 兼容协议；DeepSeek 的 deepseek-chat 通常不支持视觉输入。
                    aiError = "你当前视觉配置（VisionAI:BaseUrl）指向 DeepSeek，但本功能需要“上传图片的视觉分析（image_url）”。DeepSeek 的 deepseek-chat 一般不支持图片输入，所以会分析失败。请把 VisionAI 配置为支持图片的模型/平台（例如 OpenAI 视觉模型或 Moonshot/Kimi 的视觉能力接口）。";
                }

                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var photoFullPath = Path.Combine(webRoot, record.PhotoPath.TrimStart('/', '\\'));
                if (!System.IO.File.Exists(photoFullPath))
                {
                    aiError = "未找到上传的图片文件，请重新上传伤口照片后刷新本页。";
                }
                else if (string.IsNullOrWhiteSpace(aiError))
                {
                    try
                    {
                        aiResult = await _woundImageAnalysisService.AnalyzeImageAsync(photoFullPath);
                        if (aiResult == null)
                            aiError = "AI 分析未返回结果，请确认已在 appsettings.json 中配置 VisionAI:ApiKey 与 VisionAI:Model（以及兼容的 VisionAI:BaseUrl）。";
                    }
                    catch (Exception ex)
                    {
                        aiError = "AI 分析失败：" + ex.Message;
                    }
                }
            }
            ViewBag.WoundAiResult = aiResult;
            ViewBag.WoundAiError = aiError;

            return View(record);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int woundId, decimal? temperature,
            bool hasInfection, bool hasFever, bool hasOdor, bool hasDischarge, string notes)
        {
            try
            {
                var userId = GetUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                await _woundService.UpdateRecordAsync(woundId, temperature, hasInfection, hasFever, hasOdor, hasDischarge, notes);
                if (hasInfection || hasFever || hasOdor || hasDischarge)
                {
                    var parts = new List<string>();
                    if (hasInfection) parts.Add("感染");
                    if (hasFever) parts.Add("发热");
                    if (hasOdor) parts.Add("异味");
                    if (hasDischarge) parts.Add("渗出");
                    var summary = string.Join("、", parts);
                    await _highRiskAlertService.NotifyAsync(userId, "WoundAbnormal", summary, woundId, "Wound");
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                var record = await _woundService.GetRecordAsync(woundId);
                return record != null ? View("Edit", record) : RedirectToAction("Index");
            }
        }

        /// <summary>
        /// 保存至文件夹：先更新数据库，再在桌面（或备用目录）创建“伤口档案”文件夹并导出 HTML 文件。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveToFolder(int woundId, string temperature,
            bool hasInfection, bool hasFever, bool hasOdor, bool hasDischarge, string notes)
        {
            try
            {
                var userId = GetUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                decimal? temperatureValue = null;
                if (!string.IsNullOrWhiteSpace(temperature) && decimal.TryParse(temperature.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t))
                    temperatureValue = t;

                await _woundService.UpdateRecordAsync(woundId, temperatureValue, hasInfection, hasFever, hasOdor, hasDischarge, notes);

                var record = await _woundService.GetRecordAsync(woundId);
                if (record == null || record.UserId != userId)
                    return NotFound();

                // 优先使用桌面，若桌面路径为空或不可用则使用程序目录下的“AI伤口评估报告”文件夹（如 IIS/服务器环境）
                string folderPath;
                string folderDescription;
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (!string.IsNullOrWhiteSpace(desktop))
                {
                    folderPath = Path.Combine(desktop, "AI伤口评估报告");
                    folderDescription = "桌面\\AI伤口评估报告";
                }
                else
                {
                    folderPath = Path.Combine(Directory.GetCurrentDirectory(), "AI伤口评估报告");
                    folderDescription = "程序目录\\AI伤口评估报告";
                }

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string ageStr = "—";
                if (record.User?.DateOfBirth != null)
                {
                    int age = DateTime.Today.Year - record.User.DateOfBirth.Value.Year;
                    if (record.User.DateOfBirth.Value.Date > DateTime.Today.AddYears(-age)) age--;
                    ageStr = age.ToString();
                }
                string identityStr = "—";
                if (record.User != null)
                    identityStr = record.User.UserType == "Patient" ? "患者" : record.User.UserType == "Doctor" ? "医生" : record.User.UserType == "Nurse" ? "护士" : record.User.UserType ?? "—";

                string genderStr = string.IsNullOrWhiteSpace(record.User?.Gender) ? "—" : record.User.Gender;
                string phoneStr = string.IsNullOrWhiteSpace(record.User?.PhoneNumber) ? "—" : record.User.PhoneNumber;

                string imgHtml;
                string? photoFullPath = null;
                if (!string.IsNullOrEmpty(record.PhotoPath))
                {
                    photoFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", record.PhotoPath.TrimStart('/', '\\'));
                    if (System.IO.File.Exists(photoFullPath))
                    {
                        byte[] bytes = await System.IO.File.ReadAllBytesAsync(photoFullPath);
                        string base64 = Convert.ToBase64String(bytes);
                        imgHtml = $"<img src=\"data:image/jpeg;base64,{base64}\" alt=\"伤口照片\" style=\"max-width:100%;max-height:320px;\">";
                    }
                    else
                    {
                        imgHtml = "<span>无</span>";
                        photoFullPath = null;
                    }
                }
                else
                    imgHtml = "<span>无</span>";

                // AI 分析伤口照片（有照片且配置了 API 时）
                WoundImageAnalysisResult? aiResult = null;
                if (!string.IsNullOrEmpty(photoFullPath))
                {
                    try
                    {
                        aiResult = await _woundImageAnalysisService.AnalyzeImageAsync(photoFullPath!);
                    }
                    catch
                    {
                        // 忽略分析失败，档案仍可导出
                    }
                }

                string timeStr = $"{record.RecordTime.Hours:D2}:{record.RecordTime.Minutes:D2}";
                string fileName = $"AI伤口评估报告_{record.WoundId}_{record.RecordDate:yyyyMMdd}_{record.RecordTime.Hours:D2}{record.RecordTime.Minutes:D2}.html";
                string filePath = Path.Combine(folderPath, fileName);

                string Esc(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : WebUtility.HtmlEncode(s.Trim());

                // 患者基本信息表
                string patientTable = $@"<table>
<tr><th>姓名</th><td>{Esc(record.User?.FullName)}</td><th>年龄</th><td>{ageStr}</td></tr>
<tr><th>性别</th><td>{genderStr}</td><th>联系电话</th><td>{Esc(phoneStr)}</td></tr>
<tr><th>身份信息</th><td colspan=""3"">{identityStr}</td></tr>
</table>";

                // 伤口照片表
                string photoTable = $@"<table>
<tr><th>伤口照片</th><td colspan=""3"">{imgHtml}</td></tr>
</table>";

                // AI 伤口分析表（根据患者提交的伤口照片：伤口位置及分级、创面大小、创面外观）
                string aiAssessmentTable = "";
                if (aiResult != null)
                {
                    aiAssessmentTable = $@"<div class=""section""><div class=""section-title"">AI伤口分析</div>
<p class=""section-desc"">以下内容由 AI 根据患者提交的足部伤口图片自动生成</p>
<table>
<tr><th>伤口位置及分级</th><td colspan=""3"">{Esc(aiResult.LocationAndGrade)}</td></tr>
<tr><th>创面大小</th><td colspan=""3"">{Esc(aiResult.WoundSize)}</td></tr>
<tr><th>创面外观</th><td colspan=""3"">{Esc(aiResult.WoundAppearance)}</td></tr>
</table></div>";
                }
                else
                {
                    aiAssessmentTable = @"<div class=""section""><div class=""section-title"">AI伤口分析</div>
<p class=""section-desc"">以下内容由 AI 根据患者提交的足部伤口图片自动生成</p>
<table>
<tr><th>伤口位置及分级</th><td colspan=""3"">—</td></tr>
<tr><th>创面大小</th><td colspan=""3"">—</td></tr>
<tr><th>创面外观</th><td colspan=""3"">—</td></tr>
</table>
<p class=""text-muted"">（未配置 OpenAI 或未上传足部伤口照片时无法自动生成，请上传足部伤口照片并配置 OpenAI 后重新导出）</p></div>";
                }

                // 感染倾向（表格）
                string infectionSection = "";
                if (aiResult != null && !string.IsNullOrWhiteSpace(aiResult.InfectionTendency))
                {
                    infectionSection = $@"<div class=""section""><div class=""section-title"">感染倾向</div>
<table><tr><th>评估</th><td colspan=""3"">{Esc(aiResult.InfectionTendency)}</td></tr></table></div>";
                }
                else
                {
                    infectionSection = @"<div class=""section""><div class=""section-title"">感染倾向</div>
<table><tr><th>评估</th><td colspan=""3"">—</td></tr></table></div>";
                }

                // 建议（表格：清创方式、换药频率、患者自我管理）
                string suggestionsTable = "";
                if (aiResult != null)
                {
                    suggestionsTable = $@"<div class=""section""><div class=""section-title"">提出建议</div>
<p class=""section-desc"">以下内容由 AI 根据患者提交的足部图片自动生成，内容包括：清创方式、换药频率、患者自我管理</p>
<table>
<tr><th>清创方式</th><td colspan=""3"">{Esc(aiResult.DebridementSuggestion)}</td></tr>
<tr><th>换药频率</th><td colspan=""3"">{Esc(aiResult.DressingFrequencySuggestion)}</td></tr>
<tr><th>患者自我管理</th><td colspan=""3"">{Esc(aiResult.SelfManagementSuggestion)}</td></tr>
</table></div>";
                }
                else
                {
                    suggestionsTable = @"<div class=""section""><div class=""section-title"">提出建议</div>
<p class=""section-desc"">以下内容由 AI 根据患者提交的足部图片自动生成，内容包括：清创方式、换药频率、患者自我管理</p>
<table>
<tr><th>清创方式</th><td colspan=""3"">—</td></tr>
<tr><th>换药频率</th><td colspan=""3"">—</td></tr>
<tr><th>患者自我管理</th><td colspan=""3"">—</td></tr>
</table></div>";
                }

                string html = $@"<!DOCTYPE html>
<html><head><meta charset=""utf-8""><title>AI伤口评估报告</title>
<style>
table {{ border-collapse: collapse; width: 100%; }}
th, td {{ border: 1px solid #dee2e6; padding: 0.5rem 0.75rem; text-align: left; }}
th {{ background: #f8f9fa; width: 140px; }}
.title {{ text-align: center; font-weight: bold; font-size: 1.5rem; margin-bottom: 0.25rem; }}
.datetime {{ text-align: center; font-size: 0.9rem; color: #6c757d; margin-bottom: 1.5rem; }}
.section {{ margin-bottom: 1rem; }}
.section-title {{ font-weight: 600; margin-bottom: 0.5rem; padding-bottom: 0.25rem; border-bottom: 1px solid #dee2e6; }}
.section-desc {{ color: #6c757d; font-size: 0.9rem; margin-bottom: 0.5rem; }}
.text-muted {{ color: #6c757d; font-size: 0.9rem; margin-top: 0.5rem; }}
</style></head><body style=""max-width:210mm;margin:0 auto;padding:15mm;"">
<div class=""title"">AI伤口评估报告</div>
<div class=""datetime"">记录时间: {record.RecordDate:yyyy-MM-dd} {timeStr}</div>
<div class=""section""><div class=""section-title"">患者基本信息</div>
{patientTable}</div>
<div class=""section""><div class=""section-title"">伤口照片</div>
{photoTable}</div>
{aiAssessmentTable}
{infectionSection}
{suggestionsTable}
</body></html>";

                await System.IO.File.WriteAllTextAsync(filePath, html);

                TempData["SaveToFolderMessage"] = $"已保存至{folderDescription}\\{fileName}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "保存至文件夹失败：" + ex.Message;
                var record = await _woundService.GetRecordAsync(woundId);
                return record != null ? RedirectToAction("Edit", new { id = woundId }) : RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int woundId)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            await _woundService.DeleteRecordAsync(woundId);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var record = await _woundService.GetRecordAsync(id);
            if (record == null || record.UserId != userId)
                return NotFound();

            return View(record);
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

