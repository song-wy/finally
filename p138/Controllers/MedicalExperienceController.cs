using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.Controllers
{
    /// <summary>
    /// 患者端就医体验（对医生、线上就医、系统进行 emoji 评价）
    /// </summary>
    public class MedicalExperienceController : Controller
    {
        private readonly DiabetesDbContext _context;

        public MedicalExperienceController(DiabetesDbContext context)
        {
            _context = context;
        }

        private int GetUserId()
        {
            return HttpContext.Session.GetInt32("UserId") ?? 0;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Login", "Auth");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(int doctorRating, int onlineConsultRating, int systemRating)
        {
            var userId = GetUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Auth");

            var userType = HttpContext.Session.GetString("UserType");
            if (!string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Login", "Auth");

            if (doctorRating < 1 || doctorRating > 5) doctorRating = 3;
            if (onlineConsultRating < 1 || onlineConsultRating > 5) onlineConsultRating = 3;
            if (systemRating < 1 || systemRating > 5) systemRating = 3;

            var feedback = new MedicalExperienceFeedback
            {
                UserId = userId,
                DoctorRating = doctorRating,
                OnlineConsultRating = onlineConsultRating,
                SystemRating = systemRating,
                CreatedAt = DateTime.Now
            };
            _context.MedicalExperienceFeedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            TempData["Success"] = "感谢您的评价，我们会持续改进服务。";
            return RedirectToAction(nameof(Index));
        }
    }
}
