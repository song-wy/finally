using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using DiabetesPatientApp.Services;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.Controllers
{
    public class CommunityController : Controller
    {
        private readonly ICommunityService _communityService;

        public CommunityController(ICommunityService communityService)
        {
            _communityService = communityService;
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

            var posts = await _communityService.GetAllPostsAsync();
            return View(posts);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            var post = await _communityService.GetPostByIdAsync(id);
            if (post == null) return NotFound();

            return View(post);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(string title, string content, bool isAnonymous = false)
        {
            try
            {
                var userId = GetUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
                {
                    ViewBag.Error = "标题和内容不能为空";
                    return View();
                }

                await _communityService.CreatePostAsync(userId, title, content, isAnonymous);
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int postId, string content, bool isAnonymous = false)
        {
            try
            {
                var userId = GetUserId();
                if (userId == 0) return RedirectToAction("Login", "Auth");

                if (string.IsNullOrWhiteSpace(content))
                {
                    TempData["Error"] = "评论内容不能为空";
                    return RedirectToAction("Details", new { id = postId });
                }

                await _communityService.AddCommentAsync(postId, userId, content, isAnonymous);
                return RedirectToAction("Details", new { id = postId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", new { id = postId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeletePost(int id)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            await _communityService.DeletePostAsync(id);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteComment(int id, int postId)
        {
            var userId = GetUserId();
            if (userId == 0) return RedirectToAction("Login", "Auth");

            await _communityService.DeleteCommentAsync(id);
            return RedirectToAction("Details", new { id = postId });
        }
    }
}
