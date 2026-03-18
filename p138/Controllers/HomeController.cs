using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace DiabetesPatientApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _environment;

        public HomeController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var carouselDir = Path.Combine(webRoot, "uploads", "carousel");
            var articleDir = Path.Combine(webRoot, "uploads", "articles");

            var carouselImages = new List<string>();
            if (Directory.Exists(carouselDir))
            {
                carouselImages = Directory.GetFiles(carouselDir)
                    .OrderByDescending(System.IO.File.GetLastWriteTime)
                    .Select(path => "/" + Path.GetRelativePath(webRoot, path).Replace("\\", "/"))
                    .ToList();
            }

            string? articleContent = null;
            List<string>? articleParagraphs = null;
            if (Directory.Exists(articleDir))
            {
                var latestArticle = Directory.GetFiles(articleDir)
                    .OrderByDescending(System.IO.File.GetLastWriteTime)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(latestArticle))
                {
                    articleContent = System.IO.File.ReadAllText(latestArticle, Encoding.UTF8);
                    articleParagraphs = articleContent
                        .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                }
            }

            ViewBag.CarouselImages = carouselImages;
            ViewBag.ArticleContent = articleContent;
            ViewBag.ArticleParagraphs = articleParagraphs;

            return View();
        }
    }
}


