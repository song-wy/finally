using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;
using DiabetesPatientApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace DiabetesPatientApp.Controllers
{
    public class EducationController : Controller
    {
        private readonly DiabetesDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ISystemSettingsService _systemSettings;

        public EducationController(DiabetesDbContext context, IWebHostEnvironment env, ISystemSettingsService systemSettings)
        {
            _context = context;
            _env = env;
            _systemSettings = systemSettings;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (userId == 0)
            {
                context.Result = RedirectToAction("Login", "Auth");
                return;
            }
            base.OnActionExecuting(context);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userType = HttpContext.Session.GetString("UserType") ?? "";
            if (!string.Equals(userType, "Patient", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Home");
            }

            var items = await _context.EducationResources
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.CreatedAt)
                .Take(100)
                .ToListAsync();

            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> DoctorManage()
        {
            var userType = HttpContext.Session.GetString("UserType") ?? "";
            if (!string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Home");
            }

            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var items = await _context.EducationResources
                .AsNoTracking()
                .Where(x => x.UploaderDoctorId == doctorId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(200)
                .ToListAsync();

            ViewBag.StatusMessage = TempData["EduMessage"]?.ToString();
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(string title, string resourceType, IFormFile file)
        {
            var userType = HttpContext.Session.GetString("UserType") ?? "";
            if (!string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Home");
            }

            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            if (doctorId == 0 || file == null || file.Length == 0)
            {
                TempData["EduMessage"] = "上传失败：请选择文件。";
                return RedirectToAction(nameof(DoctorManage));
            }

            var maxMb = await _systemSettings.GetIntAsync(SystemSettingKeys.MaxUploadMB, 30);
            if (maxMb > 0 && file.Length > (long)maxMb * 1024L * 1024L)
            {
                TempData["EduMessage"] = $"上传失败：文件过大，单个文件请 ≤ {maxMb}MB。";
                return RedirectToAction(nameof(DoctorManage));
            }

            title = (title ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                TempData["EduMessage"] = "上传失败：请填写标题。";
                return RedirectToAction(nameof(DoctorManage));
            }

            resourceType = string.IsNullOrWhiteSpace(resourceType) ? "文件" : resourceType.Trim();
            if (resourceType != "视频" && resourceType != "文件") resourceType = "文件";

            var ext = Path.GetExtension(file.FileName) ?? "";
            var safeExt = ext.Length > 10 ? "" : ext;
            var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{safeExt}";
            var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "education");
            Directory.CreateDirectory(uploadDir);
            var savePath = Path.Combine(uploadDir, fileName);

            await using (var fs = new FileStream(savePath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            var url = $"/uploads/education/{fileName}";

            _context.EducationResources.Add(new EducationResource
            {
                UploaderDoctorId = doctorId,
                Title = title,
                ResourceType = resourceType,
                FileUrl = url,
                OriginalFileName = file.FileName ?? fileName,
                IsActive = true,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["EduMessage"] = "宣教资料已上传。";
            return RedirectToAction(nameof(DoctorManage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var userType = HttpContext.Session.GetString("UserType") ?? "";
            if (!string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Home");
            }

            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var item = await _context.EducationResources.FirstOrDefaultAsync(x => x.EducationResourceId == id && x.UploaderDoctorId == doctorId);
            if (item == null)
            {
                TempData["EduMessage"] = "未找到该资源。";
                return RedirectToAction(nameof(DoctorManage));
            }

            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();
            TempData["EduMessage"] = item.IsActive ? "已上架（患者端可见）。" : "已下架（患者端不可见）。";
            return RedirectToAction(nameof(DoctorManage));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userType = HttpContext.Session.GetString("UserType") ?? "";
            if (!string.Equals(userType, "Doctor", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Home");
            }

            var doctorId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var item = await _context.EducationResources.FirstOrDefaultAsync(x => x.EducationResourceId == id && x.UploaderDoctorId == doctorId);
            if (item == null)
            {
                TempData["EduMessage"] = "未找到该资源。";
                return RedirectToAction(nameof(DoctorManage));
            }

            _context.EducationResources.Remove(item);
            await _context.SaveChangesAsync();

            try
            {
                if (!string.IsNullOrWhiteSpace(item.FileUrl) && item.FileUrl.StartsWith("/uploads/education/", StringComparison.OrdinalIgnoreCase))
                {
                    var local = Path.Combine(_env.WebRootPath, item.FileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(local))
                    {
                        System.IO.File.Delete(local);
                    }
                }
            }
            catch
            {
                // 忽略：删除数据库记录优先
            }

            TempData["EduMessage"] = "已删除宣教资源。";
            return RedirectToAction(nameof(DoctorManage));
        }
    }
}

