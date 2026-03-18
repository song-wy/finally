using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.Services
{
    public interface IHighRiskAlertService
    {
        /// <summary>
        /// 患者端发生异常时调用，向医生端高危患者预警发送一条通知。
        /// </summary>
        Task NotifyAsync(int patientId, string alertType, string summary, int? relatedRecordId = null, string? relatedTable = null);

        /// <summary>
        /// 获取最近一段时间内的预警通知（供医生端展示）。
        /// </summary>
        Task<List<HighRiskAlertNotification>> GetRecentNotificationsAsync(int days = 30, int maxCount = 100);
    }

    public class HighRiskAlertService : IHighRiskAlertService
    {
        private readonly DiabetesDbContext _context;

        public HighRiskAlertService(DiabetesDbContext context)
        {
            _context = context;
        }

        public async Task NotifyAsync(int patientId, string alertType, string summary, int? relatedRecordId = null, string? relatedTable = null)
        {
            var notification = new HighRiskAlertNotification
            {
                PatientId = patientId,
                AlertType = alertType,
                Summary = summary ?? string.Empty,
                RelatedRecordId = relatedRecordId,
                RelatedTable = relatedTable,
                CreatedAt = DateTime.Now
            };
            _context.HighRiskAlertNotifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task<List<HighRiskAlertNotification>> GetRecentNotificationsAsync(int days = 30, int maxCount = 100)
        {
            var since = DateTime.Today.AddDays(-days);
            var list = await _context.HighRiskAlertNotifications
                .AsNoTracking()
                .Include(n => n.Patient)
                .Where(n => n.CreatedAt >= since)
                .OrderByDescending(n => n.CreatedAt)
                .Take(maxCount)
                .ToListAsync();
            return list;
        }
    }
}
