using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.Services
{
    public interface IReminderService
    {
        Task<Reminder> CreateReminderAsync(int userId, string mealType, TimeSpan reminderTime);
        Task<List<Reminder>> GetUserRemindersAsync(int userId);
        Task<List<Reminder>> GetActiveRemindersAsync(int userId);
        Task UpdateReminderAsync(int reminderId, TimeSpan reminderTime, bool isActive);
        Task DeleteReminderAsync(int reminderId);
        Task<List<Reminder>> GetDueRemindersAsync();
        Task<bool> HasRecordedTodayAsync(int userId, string mealType);
        Task<List<BloodSugarRecord>> GetTodayRecordsAsync(int userId);
        Task<List<BloodSugarSlotStatus>> GetTodayBloodSugarCompletionAsync(int userId);
    }

    public class ReminderService : IReminderService
    {
        private readonly DiabetesDbContext _context;

        public ReminderService(DiabetesDbContext context)
        {
            _context = context;
        }

        // 创建提醒
        public async Task<Reminder> CreateReminderAsync(int userId, string mealType, TimeSpan reminderTime)
        {
            var reminder = new Reminder
            {
                UserId = userId,
                MealType = mealType,
                ReminderTime = reminderTime,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            _context.Reminders.Add(reminder);
            await _context.SaveChangesAsync();
            return reminder;
        }

        // 获取用户所有提醒
        public async Task<List<Reminder>> GetUserRemindersAsync(int userId)
        {
            var reminders = await _context.Reminders
                .Where(r => r.UserId == userId)
                .ToListAsync();

            return reminders
                .OrderBy(r => r.ReminderTime)
                .ToList();
        }

        // 获取用户活跃提醒
        public async Task<List<Reminder>> GetActiveRemindersAsync(int userId)
        {
            var reminders = await _context.Reminders
                .Where(r => r.UserId == userId && r.IsActive)
                .ToListAsync();

            return reminders
                .OrderBy(r => r.ReminderTime)
                .ToList();
        }

        // 更新提醒
        public async Task UpdateReminderAsync(int reminderId, TimeSpan reminderTime, bool isActive)
        {
            var reminder = await _context.Reminders.FindAsync(reminderId);
            if (reminder != null)
            {
                reminder.ReminderTime = reminderTime;
                reminder.IsActive = isActive;
                await _context.SaveChangesAsync();
            }
        }

        // 删除提醒
        public async Task DeleteReminderAsync(int reminderId)
        {
            var reminder = await _context.Reminders.FindAsync(reminderId);
            if (reminder != null)
            {
                _context.Reminders.Remove(reminder);
                await _context.SaveChangesAsync();
            }
        }

        // 获取应该触发的提醒（当前时间前后5分钟内）
        public async Task<List<Reminder>> GetDueRemindersAsync()
        {
            var now = GetBeijingNow();
            var currentTime = now.TimeOfDay;
            var fiveMinutesAgo = currentTime.Add(TimeSpan.FromMinutes(-5));
            var fiveMinutesLater = currentTime.Add(TimeSpan.FromMinutes(5));

            return await _context.Reminders
                .Where(r => r.IsActive && 
                    r.ReminderTime >= fiveMinutesAgo && 
                    r.ReminderTime <= fiveMinutesLater)
                .Include(r => r.User)
                .ToListAsync();
        }

        // 检查今天是否已记录该餐次
        public async Task<bool> HasRecordedTodayAsync(int userId, string mealType)
        {
            var today = GetBeijingNow().Date;
            var tomorrow = today.AddDays(1);
            // 兼容“第一次/第二次/第三次餐后提醒”等扩展类型：都视为餐后记录
            var normalizedMealType = NormalizeMealTypeForRecord(mealType);
            return await _context.BloodSugarRecords
                .AnyAsync(r => r.UserId == userId && 
                    r.RecordDate >= today &&
                    r.RecordDate < tomorrow &&
                    r.MealType == normalizedMealType);
        }

        // 获取今天的记录
        public async Task<List<BloodSugarRecord>> GetTodayRecordsAsync(int userId)
        {
            var today = GetBeijingNow().Date;
            var tomorrow = today.AddDays(1);
            var records = await _context.BloodSugarRecords
                .Where(r => r.UserId == userId && r.RecordDate >= today && r.RecordDate < tomorrow)
                .ToListAsync();

            return records
                .OrderBy(r => r.RecordTime)
                .ToList();
        }

        /// <summary>
        /// 获取今日各血糖记录时间段的完成状态（用于消息通知列）
        /// 规定时间段：空腹 06:00-08:00；第一次餐后 10:00-11:00；第二次 13:00-14:00；第三次 20:00-22:00
        /// </summary>
        public async Task<List<BloodSugarSlotStatus>> GetTodayBloodSugarCompletionAsync(int userId)
        {
            var now = GetBeijingNow();
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var records = await _context.BloodSugarRecords
                .Where(r => r.UserId == userId && r.RecordDate >= today && r.RecordDate < tomorrow)
                .ToListAsync();

            var slots = new[]
            {
                (Label: "空腹", Range: "06:00-08:00", MealType: "Fasting", Start: TimeSpan.FromHours(6), End: TimeSpan.FromHours(8)),
                (Label: "第一次餐后", Range: "10:00-11:00", MealType: "AfterMeal", Start: TimeSpan.FromHours(10), End: TimeSpan.FromHours(11)),
                (Label: "第二次餐后", Range: "13:00-14:00", MealType: "AfterMeal", Start: TimeSpan.FromHours(13), End: TimeSpan.FromHours(14)),
                (Label: "第三次餐后", Range: "20:00-22:00", MealType: "AfterMeal", Start: TimeSpan.FromHours(20), End: TimeSpan.FromHours(22))
            };

            var result = new List<BloodSugarSlotStatus>();
            foreach (var slot in slots)
            {
                var completed = records.Any(r =>
                    string.Equals(r.MealType, slot.MealType, StringComparison.OrdinalIgnoreCase) &&
                    r.RecordTime >= slot.Start && r.RecordTime <= slot.End);

                var statusCode = "Upcoming";
                var statusText = "未开始";

                if (completed)
                {
                    statusCode = "Completed";
                    statusText = "已完成";
                }
                else if (now.TimeOfDay > slot.End)
                {
                    statusCode = "Incomplete";
                    statusText = "未完成";
                }
                else if (now.TimeOfDay >= slot.Start && now.TimeOfDay <= slot.End)
                {
                    statusCode = "Pending";
                    statusText = "待完成";
                }

                result.Add(new BloodSugarSlotStatus
                {
                    Label = slot.Label,
                    Range = slot.Range,
                    Completed = completed,
                    StatusCode = statusCode,
                    StatusText = statusText
                });
            }
            return result;
        }

        private static string NormalizeMealTypeForRecord(string? mealType)
        {
            if (string.IsNullOrWhiteSpace(mealType)) return string.Empty;
            mealType = mealType.Trim();
            if (mealType.StartsWith("AfterMeal", StringComparison.OrdinalIgnoreCase))
                return "AfterMeal";
            return mealType;
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

