using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using DiabetesPatientApp.Services;
using DiabetesPatientApp.Models;
using DiabetesPatientApp.ViewModels;
using DiabetesPatientApp.Data;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace DiabetesPatientApp.Controllers
{
    public class ConsultationController : Controller
    {
        private readonly IConsultationService _consultationService;
        private readonly IQuestionnaireGenerationService _questionnaireGenerationService;
        private readonly DiabetesDbContext _context;
        private const string GeneratedQuestionnaireSessionKey = "GeneratedQuestionnaire";

        public ConsultationController(
            IConsultationService consultationService,
            IQuestionnaireGenerationService questionnaireGenerationService,
            DiabetesDbContext context)
        {
            _consultationService = consultationService;
            _questionnaireGenerationService = questionnaireGenerationService;
            _context = context;
        }

        private int GetUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var model = await BuildIndexViewModelAsync(userId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateQuestionnaire(string keyword, string requirements)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var result = await _questionnaireGenerationService.GenerateAsync(keyword, requirements);
                var generatedQuestionnaire = new AiQuestionnaireViewModel
                {
                    Title = result.Title,
                    Introduction = result.Introduction,
                    Questions = result.Questions,
                    Source = result.Source,
                    GeneratedAt = DateTime.Now
                };

                HttpContext.Session.SetString(
                    GeneratedQuestionnaireSessionKey,
                    JsonSerializer.Serialize(generatedQuestionnaire));

                return RedirectToAction(nameof(QuestionnaireResult));
            }
            catch (Exception ex)
            {
                var model = await BuildIndexViewModelAsync(userId, keyword, requirements, null, ex.Message);
                return View("Index", model);
            }
        }

        [HttpGet]
        public IActionResult QuestionnaireResult()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Index));
            }

            var questionnaireJson = HttpContext.Session.GetString(GeneratedQuestionnaireSessionKey);
            if (string.IsNullOrWhiteSpace(questionnaireJson))
            {
                TempData["Error"] = "暂无已生成的问卷结果，请先生成问卷。";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var questionnaire = JsonSerializer.Deserialize<AiQuestionnaireViewModel>(questionnaireJson);
                if (questionnaire == null)
                {
                    TempData["Error"] = "问卷结果读取失败，请重新生成。";
                    return RedirectToAction(nameof(Index));
                }

                return View(BuildQuestionnaireResultPageModel(questionnaire));
            }
            catch
            {
                TempData["Error"] = "问卷结果读取失败，请重新生成。";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendQuestionnaireToPatient(List<int>? patientIds)
        {
            var doctorId = GetUserId();
            if (doctorId == 0) return RedirectToAction("Login", "Auth");

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Index));
            }

            var selectedPatientIds = (patientIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (selectedPatientIds.Count == 0)
            {
                TempData["Error"] = "请至少选择一位患者。";
                return RedirectToAction(nameof(QuestionnaireResult));
            }

            var questionnaire = TryGetGeneratedQuestionnaireFromSession();
            if (questionnaire == null)
            {
                TempData["Error"] = "暂无可发送的问卷结果，请先生成问卷。";
                return RedirectToAction(nameof(Index));
            }

            var patients = await _context.Users
                .AsNoTracking()
                .Where(u => selectedPatientIds.Contains(u.UserId) && u.UserType == "Patient")
                .ToListAsync();
            if (patients.Count == 0)
            {
                TempData["Error"] = "未找到要发送的患者。";
                return RedirectToAction(nameof(QuestionnaireResult));
            }

            var questionnaireJson = JsonSerializer.Serialize(questionnaire);

            foreach (var patient in patients)
            {
                var token = Guid.NewGuid().ToString("N");
                var assignment = new QuestionnaireAssignment
                {
                    DoctorId = doctorId,
                    PatientId = patient.UserId,
                    QuestionnaireJson = questionnaireJson,
                    AccessToken = token,
                    CreatedAt = DateTime.Now
                };
                _context.QuestionnaireAssignments.Add(assignment);
                await _context.SaveChangesAsync();

                var fillUrl = BuildAbsoluteUrl($"/Consultation/FillQuestionnaire?id={assignment.QuestionnaireAssignmentId}&token={token}");

                var textMessage = BuildQuestionnaireFillMessage(questionnaire, fillUrl);
                await _consultationService.SendTextMessageAsync(doctorId, patient.UserId, textMessage);

                // 发送二维码（内容为填写链接，更短也更稳定）
                try
                {
                    var qrBytes = GenerateQrCode(fillUrl);
                    var caption = "扫码填写健康问卷";
                    var fileName = $"questionnaire_qr_{doctorId}_{patient.UserId}.png";
                    await _consultationService.SendImageFromBytesAsync(doctorId, patient.UserId, qrBytes, fileName, caption);
                }
                catch
                {
                    // 忽略二维码失败，不影响文字链接填写
                }
            }

            TempData["Success"] = $"问卷已发送给 {patients.Count} 位患者。";
            return RedirectToAction(nameof(QuestionnaireResult));
        }

        [HttpGet]
        public async Task<IActionResult> FillQuestionnaire(int id, string token)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "仅患者可填写问卷。";
                return RedirectToAction(nameof(Index));
            }

            var assignment = await _context.QuestionnaireAssignments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.QuestionnaireAssignmentId == id);
            if (assignment == null || assignment.PatientId != userId || !string.Equals(assignment.AccessToken, token, StringComparison.Ordinal))
            {
                TempData["Error"] = "问卷链接无效或已过期。";
                return RedirectToAction("Index", "Dashboard");
            }

            if (assignment.SubmittedAt.HasValue)
            {
                TempData["Success"] = "该问卷已提交，无需重复填写。";
            }

            AiQuestionnaireViewModel? questionnaire;
            try
            {
                questionnaire = JsonSerializer.Deserialize<AiQuestionnaireViewModel>(assignment.QuestionnaireJson);
            }
            catch
            {
                questionnaire = null;
            }

            if (questionnaire == null || questionnaire.Questions.Count == 0)
            {
                TempData["Error"] = "问卷内容读取失败。";
                return RedirectToAction("Index", "Dashboard");
            }

            ViewBag.AssignmentId = id;
            ViewBag.Token = token;
            ViewBag.SubmittedAt = assignment.SubmittedAt;
            return View(questionnaire);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitQuestionnaire(int id, string token, List<string>? answers, List<int>? ratings)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Index));
            }

            var assignment = await _context.QuestionnaireAssignments
                .FirstOrDefaultAsync(x => x.QuestionnaireAssignmentId == id);
            if (assignment == null || assignment.PatientId != userId || !string.Equals(assignment.AccessToken, token, StringComparison.Ordinal))
            {
                TempData["Error"] = "问卷提交失败：链接无效。";
                return RedirectToAction("Index", "Dashboard");
            }

            if (assignment.SubmittedAt.HasValue)
            {
                TempData["Success"] = "该问卷已提交，无需重复提交。";
                return RedirectToAction(nameof(FillQuestionnaire), new { id, token });
            }

            AiQuestionnaireViewModel? questionnaire;
            try
            {
                questionnaire = JsonSerializer.Deserialize<AiQuestionnaireViewModel>(assignment.QuestionnaireJson);
            }
            catch
            {
                questionnaire = null;
            }
            if (questionnaire == null || questionnaire.Questions.Count == 0)
            {
                TempData["Error"] = "问卷内容读取失败。";
                return RedirectToAction("Index", "Dashboard");
            }

            var safeAnswers = (answers ?? new List<string>()).Select(a => (a ?? string.Empty).Trim()).ToList();
            var safeRatings = (ratings ?? new List<int>()).ToList();

            // 补齐长度
            while (safeAnswers.Count < questionnaire.Questions.Count) safeAnswers.Add(string.Empty);
            while (safeRatings.Count < questionnaire.Questions.Count) safeRatings.Add(0);

            var payload = new
            {
                title = questionnaire.Title,
                introduction = questionnaire.Introduction,
                questions = questionnaire.Questions.Select((q, idx) => new
                {
                    index = idx + 1,
                    question = q,
                    answer = safeAnswers[idx],
                    rating = safeRatings[idx]
                }).ToList()
            };

            assignment.AnswerJson = JsonSerializer.Serialize(payload);
            assignment.SubmittedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // 自动发送给医生：发一条咨询消息，提示已提交并给出查看入口
            var doctorViewUrl = BuildAbsoluteUrl($"/Consultation/ViewCollectedQuestionnaire?id={assignment.QuestionnaireAssignmentId}");
            var notify = $"【问卷已提交】\n患者已完成并提交问卷：{questionnaire.Title}\n医生请在健康问卷页面的“问卷收集”中查看，或点击：{doctorViewUrl}";
            await _consultationService.SendTextMessageAsync(userId, assignment.DoctorId, notify);

            TempData["Success"] = "问卷已提交，系统已自动保存并发送给医生。";
            return RedirectToAction(nameof(FillQuestionnaire), new { id, token });
        }

        [HttpGet]
        public async Task<IActionResult> ViewCollectedQuestionnaire(int id)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "仅医生可查看收集的问卷。";
                return RedirectToAction(nameof(Index));
            }

            var assignment = await _context.QuestionnaireAssignments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.QuestionnaireAssignmentId == id && x.DoctorId == userId);
            if (assignment == null)
            {
                TempData["Error"] = "未找到该问卷记录。";
                return RedirectToAction(nameof(Index));
            }

            AiQuestionnaireViewModel? questionnaire = null;
            try { questionnaire = JsonSerializer.Deserialize<AiQuestionnaireViewModel>(assignment.QuestionnaireJson); } catch { }

            JsonDocument? answersDoc = null;
            if (!string.IsNullOrWhiteSpace(assignment.AnswerJson))
            {
                try { answersDoc = JsonDocument.Parse(assignment.AnswerJson); } catch { }
            }

            var patient = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == assignment.PatientId);
            ViewBag.PatientName = patient == null
                ? $"患者#{assignment.PatientId}"
                : (string.IsNullOrWhiteSpace(patient.FullName) ? (patient.Username ?? $"患者#{assignment.PatientId}") : patient.FullName);
            ViewBag.CreatedAt = assignment.CreatedAt;
            ViewBag.SubmittedAt = assignment.SubmittedAt;
            ViewBag.AnswersJson = assignment.AnswerJson ?? string.Empty;

            return View(new Tuple<AiQuestionnaireViewModel?, string>(questionnaire, assignment.AnswerJson ?? string.Empty));
        }

        [HttpGet]
        public async Task<IActionResult> Chat(int? doctorId)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            List<ConsultationMessage> messages = new List<ConsultationMessage>();
            if (doctorId.HasValue)
            {
                messages = await _consultationService.GetConversationAsync(userId, doctorId);
            }

            ViewBag.DoctorId = doctorId;
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(int? receiverId, string messageContent, IFormFile? attachment)
        {
            try
            {
                var userId = GetUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                if (attachment != null && attachment.Length > 0)
                {
                    var contentType = attachment.ContentType?.ToLowerInvariant() ?? "";
                    var attachmentType = contentType.StartsWith("image/") ? "Image" : "File";
                    await _consultationService.SendAttachmentMessageAsync(userId, receiverId, attachment, attachmentType, messageContent);
                }
                else
                {
                    await _consultationService.SendTextMessageAsync(userId, receiverId, messageContent ?? "");
                }
                return RedirectToAction("Chat", new { doctorId = receiverId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Chat", new { doctorId = receiverId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendVoiceMessage(int? receiverId, IFormFile voiceFile)
        {
            try
            {
                var userId = GetUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                if (voiceFile == null || voiceFile.Length == 0)
                {
                    ViewBag.Error = "请选择音频文件";
                    return RedirectToAction("Chat", new { doctorId = receiverId });
                }

                await _consultationService.SendVoiceMessageAsync(userId, receiverId, voiceFile);
                return RedirectToAction("Chat", new { doctorId = receiverId });
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return RedirectToAction("Chat", new { doctorId = receiverId });
            }
        }

        [HttpGet]
        [ResponseCache(NoStore = true, Duration = 0)]
        public async Task<IActionResult> Doctors()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var doctors = await _consultationService.GetDoctorsAndNursesAsync();
            return View(doctors);
        }

        [HttpGet]
        public async Task<IActionResult> Unread()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var messages = await _consultationService.GetUnreadMessagesAsync(userId);
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int messageId)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            await _consultationService.MarkAsReadAsync(messageId);
            return Ok();
        }

        /// <summary>
        /// 删除聊天消息，仅当当前用户为发送方或接收方时可删除。删除后返回当前对话页。
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteMessage(int messageId, int? doctorId)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            bool deleted = await _consultationService.DeleteMessageAsync(messageId, userId);
            if (!deleted)
                TempData["Error"] = "无法删除该消息或消息不存在。";
            return RedirectToAction("Chat", new { doctorId });
        }

        private async Task<ConsultationQuestionnaireViewModel> BuildIndexViewModelAsync(
            int userId,
            string? keyword = null,
            string? requirements = null,
            AiQuestionnaireViewModel? generatedQuestionnaire = null,
            string? generationError = null)
        {
            var messages = await _consultationService.GetConversationAsync(userId);
            var userType = HttpContext.Session.GetString("UserType");
            var isDoctorPortal = string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase);

            var collected = new List<DoctorCollectedQuestionnaireSummary>();
            if (isDoctorPortal)
            {
                var collectedRaw = await _context.QuestionnaireAssignments
                    .AsNoTracking()
                    .Where(x => x.DoctorId == userId)
                    .OrderByDescending(x => x.SubmittedAt ?? x.CreatedAt)
                    .Take(30)
                    .Select(x => new DoctorCollectedQuestionnaireSummary
                    {
                        QuestionnaireAssignmentId = x.QuestionnaireAssignmentId,
                        PatientId = x.PatientId,
                        PatientName = "",
                        Title = x.QuestionnaireJson,
                        CreatedAt = x.CreatedAt,
                        SubmittedAt = x.SubmittedAt
                    })
                    .ToListAsync();

                var patientIds = collectedRaw.Select(x => x.PatientId).Distinct().ToList();
                var patients = await _context.Users
                    .AsNoTracking()
                    .Where(u => patientIds.Contains(u.UserId))
                    .Select(u => new { u.UserId, Name = string.IsNullOrWhiteSpace(u.FullName) ? (u.Username ?? $"患者#{u.UserId}") : u.FullName! })
                    .ToListAsync();
                var patientNameMap = patients.ToDictionary(x => x.UserId, x => x.Name);

                foreach (var item in collectedRaw)
                {
                    item.PatientName = patientNameMap.TryGetValue(item.PatientId, out var name) ? name : $"患者#{item.PatientId}";

                    try
                    {
                        var qvm = JsonSerializer.Deserialize<AiQuestionnaireViewModel>(item.Title);
                        item.Title = qvm?.Title ?? "健康问卷";
                    }
                    catch
                    {
                        item.Title = "健康问卷";
                    }
                }

                collected = collectedRaw;
            }

            return new ConsultationQuestionnaireViewModel
            {
                IsDoctorPortal = isDoctorPortal,
                PageTitle = isDoctorPortal ? "健康问卷" : "在线咨询",
                Keyword = keyword ?? string.Empty,
                Requirements = requirements ?? string.Empty,
                GeneratedQuestionnaire = generatedQuestionnaire,
                GenerationError = generationError,
                Messages = messages,
                CollectedQuestionnaires = collected
            };
        }

        private QuestionnaireResultPageViewModel BuildQuestionnaireResultPageModel(AiQuestionnaireViewModel questionnaire)
        {
            var patients = _context.Users
                .AsNoTracking()
                .Where(u => u.UserType == "Patient")
                .OrderByDescending(u => u.CreatedDate)
                .Select(u => new QuestionnairePatientOption
                {
                    PatientId = u.UserId,
                    DisplayName = string.IsNullOrWhiteSpace(u.FullName) ? (u.Username ?? $"患者#{u.UserId}") : u.FullName!
                })
                .ToList();

            return new QuestionnaireResultPageViewModel
            {
                Questionnaire = questionnaire,
                Patients = patients
            };
        }

        private AiQuestionnaireViewModel? TryGetGeneratedQuestionnaireFromSession()
        {
            var questionnaireJson = HttpContext.Session.GetString(GeneratedQuestionnaireSessionKey);
            if (string.IsNullOrWhiteSpace(questionnaireJson))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<AiQuestionnaireViewModel>(questionnaireJson);
            }
            catch
            {
                return null;
            }
        }

        private static string BuildQuestionnaireMessage(AiQuestionnaireViewModel questionnaire)
        {
            var lines = new List<string>
            {
                "【健康问卷】",
                questionnaire.Title
            };

            if (!string.IsNullOrWhiteSpace(questionnaire.Introduction))
            {
                lines.Add(questionnaire.Introduction);
            }

            for (var i = 0; i < questionnaire.Questions.Count; i++)
            {
                lines.Add($"{i + 1}. {questionnaire.Questions[i]}");
            }

            lines.Add("请根据问卷内容进行填写，并结合自身情况回复。");
            return string.Join(Environment.NewLine, lines);
        }

        private string BuildAbsoluteUrl(string relativePath)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return baseUrl + relativePath;
        }

        private static string BuildQuestionnaireFillMessage(AiQuestionnaireViewModel questionnaire, string fillUrl)
        {
            var lines = new List<string>
            {
                "【健康问卷】",
                questionnaire.Title,
                "请点击链接进入填写页面，填写后系统会自动保存并发送给医生：",
                fillUrl
            };
            return string.Join(Environment.NewLine, lines);
        }

        private static byte[] GenerateQrCode(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("二维码内容不能为空", nameof(content));
            }

            using var qrGenerator = new QRCodeGenerator();
            var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            return qrCode.GetGraphic(20);
        }
    }
}

