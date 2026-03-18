using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.Services
{
    public interface ICommunityService
    {
        Task<List<Post>> GetAllPostsAsync();
        Task<Post?> GetPostByIdAsync(int postId);
        Task<Post> CreatePostAsync(int userId, string title, string content, bool isAnonymous);
        Task<Comment> AddCommentAsync(int postId, int userId, string content, bool isAnonymous);
        Task<List<Comment>> GetCommentsByPostIdAsync(int postId);
        Task DeletePostAsync(int postId);
        Task DeleteCommentAsync(int commentId);
    }

    public class CommunityService : ICommunityService
    {
        private readonly DiabetesDbContext _context;

        public CommunityService(DiabetesDbContext context)
        {
            _context = context;
        }

        public async Task<List<Post>> GetAllPostsAsync()
        {
            return await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();
        }

        public async Task<Post?> GetPostByIdAsync(int postId)
        {
            return await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User)
                .FirstOrDefaultAsync(p => p.PostId == postId);
        }

        public async Task<Post> CreatePostAsync(int userId, string title, string content, bool isAnonymous)
        {
            var post = new Post
            {
                UserId = userId,
                Title = title,
                Content = content,
                IsAnonymous = isAnonymous,
                CreatedDate = DateTime.Now
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();
            return post;
        }

        public async Task<Comment> AddCommentAsync(int postId, int userId, string content, bool isAnonymous)
        {
            var comment = new Comment
            {
                PostId = postId,
                UserId = userId,
                Content = content,
                IsAnonymous = isAnonymous,
                CreatedDate = DateTime.Now
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();
            return comment;
        }

        public async Task<List<Comment>> GetCommentsByPostIdAsync(int postId)
        {
            return await _context.Comments
                .Include(c => c.User)
                .Where(c => c.PostId == postId)
                .OrderBy(c => c.CreatedDate)
                .ToListAsync();
        }

        public async Task DeletePostAsync(int postId)
        {
            var post = await _context.Posts.FindAsync(postId);
            if (post != null)
            {
                _context.Posts.Remove(post);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteCommentAsync(int commentId)
        {
            var comment = await _context.Comments.FindAsync(commentId);
            if (comment != null)
            {
                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
            }
        }
    }
}
