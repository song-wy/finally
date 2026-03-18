using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using DiabetesPatientApp.Data;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.Services
{
    public interface IFootPressureService
    {
        Task<FootPressureRecord> AddRecordAsync(int userId, DateTime recordDate, TimeSpan recordTime, 
            decimal? leftPressure, string leftStatus, decimal? rightPressure, string rightStatus, 
            string notes);
        Task<List<FootPressureRecord>> GetRecordsByDateAsync(int userId, DateTime date);
        Task<List<FootPressureRecord>> GetRecordsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate);
        Task<FootPressureRecord> UpdateRecordAsync(int recordId, DateTime recordDate, TimeSpan recordTime, decimal? leftPressure, string leftStatus, 
            decimal? rightPressure, string rightStatus, string notes);
        Task DeleteRecordAsync(int recordId);
        Task<List<FootPressureRecord>> GetLatestRecordsAsync(int userId, int count = 10);
        Task<FootPressureRecord?> GetRecordByIdAsync(int recordId);
        Task<FootPressureRecord?> GetRecordWithUserByIdAsync(int recordId);
    }

    public class FootPressureService : IFootPressureService
    {
        private readonly DiabetesDbContext _context;

        public FootPressureService(DiabetesDbContext context)
        {
            _context = context;
        }

        public async Task<FootPressureRecord> AddRecordAsync(int userId, DateTime recordDate, TimeSpan recordTime,
            decimal? leftPressure, string leftStatus, decimal? rightPressure, string rightStatus,
            string notes)
        {
            var record = new FootPressureRecord
            {
                UserId = userId,
                RecordDate = recordDate,
                RecordTime = recordTime,
                LeftFootPressure = leftPressure,
                LeftFootStatus = leftStatus ?? FootPressureRecord.DeterminePressureStatus(leftPressure),
                RightFootPressure = rightPressure,
                RightFootStatus = rightStatus ?? FootPressureRecord.DeterminePressureStatus(rightPressure),
                Notes = notes,
                CreatedDate = DateTime.Now
            };

            _context.FootPressureRecords.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<List<FootPressureRecord>> GetRecordsByDateAsync(int userId, DateTime date)
        {
            var records = await _context.FootPressureRecords
                .Where(r => r.UserId == userId && r.RecordDate == date)
                .ToListAsync();

            return records
                .OrderBy(r => r.RecordTime)
                .ToList();
        }

        public async Task<List<FootPressureRecord>> GetRecordsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate)
        {
            var records = await _context.FootPressureRecords
                .Where(r => r.UserId == userId && r.RecordDate >= startDate && r.RecordDate <= endDate)
                .ToListAsync();

            return records
                .OrderByDescending(r => r.RecordDate)
                .ThenBy(r => r.RecordTime)
                .ToList();
        }

        public async Task<FootPressureRecord> UpdateRecordAsync(int recordId, DateTime recordDate, TimeSpan recordTime, decimal? leftPressure, string leftStatus,
            decimal? rightPressure, string rightStatus, string notes)
        {
            var record = await _context.FootPressureRecords.FindAsync(recordId);
            if (record == null)
                throw new Exception("记录不存在");

            record.RecordDate = recordDate.Date;
            record.RecordTime = recordTime;
            record.LeftFootPressure = leftPressure;
            record.LeftFootStatus = leftStatus ?? FootPressureRecord.DeterminePressureStatus(leftPressure);
            record.RightFootPressure = rightPressure;
            record.RightFootStatus = rightStatus ?? FootPressureRecord.DeterminePressureStatus(rightPressure);
            record.Notes = notes;

            await _context.SaveChangesAsync();
            return record;
        }

        public async Task DeleteRecordAsync(int recordId)
        {
            var record = await _context.FootPressureRecords.FindAsync(recordId);
            if (record != null)
            {
                _context.FootPressureRecords.Remove(record);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<FootPressureRecord>> GetLatestRecordsAsync(int userId, int count = 10)
        {
            var records = await _context.FootPressureRecords
                .Where(r => r.UserId == userId)
                .ToListAsync();

            return records
                .OrderByDescending(r => r.RecordDate)
                .ThenByDescending(r => r.RecordTime)
                .Take(count)
                .ToList();
        }

        public async Task<FootPressureRecord?> GetRecordByIdAsync(int recordId)
        {
            return await _context.FootPressureRecords.FindAsync(recordId);
        }

        public async Task<FootPressureRecord?> GetRecordWithUserByIdAsync(int recordId)
        {
            return await _context.FootPressureRecords
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.FootPressureId == recordId);
        }
    }
}

