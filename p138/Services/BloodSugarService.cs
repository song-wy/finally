using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.Services
{
    public interface IBloodSugarService
    {
        Task<BloodSugarRecord> AddRecordAsync(int userId, DateTime recordDate, TimeSpan recordTime, string mealType, decimal value, string notes);
        Task<BloodSugarRecord?> GetRecordByIdAsync(int userId, int recordId);
        Task<List<BloodSugarRecord>> GetRecordsByDateAsync(int userId, DateTime date);
        Task<List<BloodSugarRecord>> GetRecordsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate);
        Task<BloodSugarRecord> UpdateRecordAsync(int recordId, DateTime recordDate, TimeSpan recordTime, string mealType, decimal value, string notes);
        Task DeleteRecordAsync(int recordId);
        Task<List<BloodSugarRecord>> GetTrendDataAsync(int userId, int days = 30);
        Task<Models.User?> GetUserByIdAsync(int userId);
    }

    public class BloodSugarService : IBloodSugarService
    {
        private readonly DiabetesDbContext _context;

        public BloodSugarService(DiabetesDbContext context)
        {
            _context = context;
        }

        public async Task<BloodSugarRecord> AddRecordAsync(int userId, DateTime recordDate, TimeSpan recordTime, string mealType, decimal value, string notes)
        {
            var status = BloodSugarRecord.DetermineStatus(value, mealType);
            var record = new BloodSugarRecord
            {
                UserId = userId,
                RecordDate = recordDate,
                RecordTime = recordTime,
                MealType = mealType,
                BloodSugarValue = value,
                Status = status,
                Notes = notes,
                CreatedDate = DateTime.Now
            };

            _context.BloodSugarRecords.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<BloodSugarRecord?> GetRecordByIdAsync(int userId, int recordId)
        {
            return await _context.BloodSugarRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RecordId == recordId && r.UserId == userId);
        }

        public async Task<List<BloodSugarRecord>> GetRecordsByDateAsync(int userId, DateTime date)
        {
            // Compare by date only to avoid time component mismatches
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);
            var records = await _context.BloodSugarRecords
                .Where(r => r.UserId == userId && r.RecordDate >= startOfDay && r.RecordDate < endOfDay)
                .ToListAsync();

            return records
                .OrderBy(r => r.RecordTime)
                .ToList();
        }

        public async Task<List<BloodSugarRecord>> GetRecordsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate)
        {
            var records = await _context.BloodSugarRecords
                .Where(r => r.UserId == userId && r.RecordDate >= startDate && r.RecordDate <= endDate)
                .ToListAsync();

            return records
                .OrderByDescending(r => r.RecordDate)
                .ThenBy(r => r.RecordTime)
                .ToList();
        }

        public async Task<BloodSugarRecord> UpdateRecordAsync(int recordId, DateTime recordDate, TimeSpan recordTime, string mealType, decimal value, string notes)
        {
            var record = await _context.BloodSugarRecords.FindAsync(recordId);
            if (record == null)
                throw new Exception("记录不存在");

            record.RecordDate = recordDate.Date;
            record.RecordTime = recordTime;
            record.MealType = mealType;
            record.BloodSugarValue = value;
            record.Status = BloodSugarRecord.DetermineStatus(value, mealType);
            record.Notes = notes;
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task DeleteRecordAsync(int recordId)
        {
            var record = await _context.BloodSugarRecords.FindAsync(recordId);
            if (record != null)
            {
                _context.BloodSugarRecords.Remove(record);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<BloodSugarRecord>> GetTrendDataAsync(int userId, int days = 30)
        {
            var startDate = DateTime.Now.AddDays(-days);
            return await _context.BloodSugarRecords
                .Where(r => r.UserId == userId && r.RecordDate >= startDate)
                .OrderBy(r => r.RecordDate)
                .ToListAsync();
        }

        public async Task<Models.User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }
    }
}

