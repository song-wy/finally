using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.Services
{
    public interface IConsultationService
    {
        Task<ConsultationMessage> SendTextMessageAsync(int senderId, int? receiverId, string? content);
        Task<ConsultationMessage> SendVoiceMessageAsync(int senderId, int? receiverId, IFormFile voiceFile);
        Task<ConsultationMessage> SendAttachmentMessageAsync(int senderId, int? receiverId, IFormFile file, string attachmentType, string? caption = null);
        Task<ConsultationMessage> SendImageFromBytesAsync(int senderId, int? receiverId, byte[] data, string fileName, string? caption = null);
        Task<List<ConsultationMessage>> GetConversationAsync(int userId, int? otherUserId = null);
        Task<List<ConsultationMessage>> GetUnreadMessagesAsync(int userId);
        Task MarkAsReadAsync(int messageId);
        Task<bool> DeleteMessageAsync(int messageId, int userId);
        Task<List<User>> GetDoctorsAndNursesAsync();
    }

    public class ConsultationService : IConsultationService
    {
        private readonly DiabetesDbContext _context;
        private readonly string _voiceUploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "voices");
        private readonly string _attachmentUploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "consultation");

        public ConsultationService(DiabetesDbContext context)
        {
            _context = context;
            if (!Directory.Exists(_voiceUploadPath))
                Directory.CreateDirectory(_voiceUploadPath);
            if (!Directory.Exists(_attachmentUploadPath))
                Directory.CreateDirectory(_attachmentUploadPath);
        }

        public async Task<ConsultationMessage> SendTextMessageAsync(int senderId, int? receiverId, string? content)
        {
            var message = new ConsultationMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                MessageType = "Text",
                MessageContent = content ?? "",
                IsRead = false,
                CreatedDate = DateTime.Now
            };

            _context.ConsultationMessages.Add(message);
            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<ConsultationMessage> SendVoiceMessageAsync(int senderId, int? receiverId, IFormFile voiceFile)
        {
            string? voicePath = null;
            if (voiceFile != null && voiceFile.Length > 0)
            {
                var fileName = $"{senderId}_{DateTime.Now.Ticks}.wav";
                var filePath = Path.Combine(_voiceUploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await voiceFile.CopyToAsync(stream);
                }
                voicePath = $"/uploads/voices/{fileName}";
            }

            var message = new ConsultationMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                MessageType = "Voice",
                VoiceFilePath = voicePath,
                IsRead = false,
                CreatedDate = DateTime.Now
            };

            _context.ConsultationMessages.Add(message);
            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<ConsultationMessage> SendAttachmentMessageAsync(int senderId, int? receiverId, IFormFile file, string attachmentType, string? caption = null)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("请选择要上传的文件");

            var ext = Path.GetExtension(file.FileName) ?? "";
            var safeName = $"{senderId}_{DateTime.Now.Ticks}{ext}";
            var filePath = Path.Combine(_attachmentUploadPath, safeName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            var urlPath = $"/uploads/consultation/{safeName}";
            var displayContent = caption ?? file.FileName ?? "附件";

            var message = new ConsultationMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                MessageType = attachmentType,
                MessageContent = displayContent,
                VoiceFilePath = urlPath,
                IsRead = false,
                CreatedDate = DateTime.Now
            };

            _context.ConsultationMessages.Add(message);
            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<ConsultationMessage> SendImageFromBytesAsync(int senderId, int? receiverId, byte[] data, string fileName, string? caption = null)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("图片数据为空", nameof(data));

            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".png";
            }

            var safeName = $"{senderId}_{DateTime.Now.Ticks}{ext}";
            var filePath = Path.Combine(_attachmentUploadPath, safeName);
            await File.WriteAllBytesAsync(filePath, data);

            var urlPath = $"/uploads/consultation/{safeName}";
            var displayContent = caption ?? "二维码";

            var message = new ConsultationMessage
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                MessageType = "Image",
                MessageContent = displayContent,
                VoiceFilePath = urlPath,
                IsRead = false,
                CreatedDate = DateTime.Now
            };

            _context.ConsultationMessages.Add(message);
            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<List<ConsultationMessage>> GetConversationAsync(int userId, int? otherUserId = null)
        {
            IQueryable<ConsultationMessage> query = _context.ConsultationMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver);

            if (otherUserId.HasValue)
            {
                query = query.Where(m => 
                    (m.SenderId == userId && m.ReceiverId == otherUserId) ||
                    (m.SenderId == otherUserId && m.ReceiverId == userId));
            }
            else
            {
                query = query.Where(m => m.SenderId == userId || m.ReceiverId == userId);
            }

            return await query.OrderByDescending(m => m.CreatedDate).ToListAsync();
        }

        public async Task<List<ConsultationMessage>> GetUnreadMessagesAsync(int userId)
        {
            return await _context.ConsultationMessages
                .Include(m => m.Sender)
                .Where(m => m.ReceiverId == userId && !m.IsRead)
                .OrderByDescending(m => m.CreatedDate)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int messageId)
        {
            var message = await _context.ConsultationMessages.FindAsync(messageId);
            if (message != null)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// 删除消息：仅当当前用户为该消息的发送方或接收方时可删除。返回是否已删除。
        /// </summary>
        public async Task<bool> DeleteMessageAsync(int messageId, int userId)
        {
            var message = await _context.ConsultationMessages.FindAsync(messageId);
            if (message == null) return false;
            if (message.SenderId != userId && message.ReceiverId != userId)
                return false;
            _context.ConsultationMessages.Remove(message);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<User>> GetDoctorsAndNursesAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.UserType == "Doctor" || u.UserType == "Nurse")
                .OrderBy(u => u.UserId)
                .ToListAsync();
        }
    }
}

