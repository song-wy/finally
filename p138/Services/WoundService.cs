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
    public interface IWoundService
    {
        Task<WoundRecord> AddRecordAsync(int userId, DateTime recordDate, TimeSpan recordTime, decimal? temperature, 
            bool hasInfection, bool hasFever, bool hasOdor, bool hasDischarge, string notes);
        Task<WoundRecord> AddRecordWithPhotoAsync(int userId, DateTime recordDate, TimeSpan recordTime, decimal? temperature,
            bool hasInfection, bool hasFever, bool hasOdor, bool hasDischarge, IFormFile photoFile, string notes);
        Task<List<WoundRecord>> GetRecordsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate);
        Task<WoundRecord> UpdateRecordAsync(int woundId, decimal? temperature, bool hasInfection, bool hasFever, bool hasOdor, bool hasDischarge, string notes);
        Task DeleteRecordAsync(int woundId);
        Task<WoundRecord?> GetRecordAsync(int woundId);
    }

    public class WoundService : IWoundService
    {
        private readonly DiabetesDbContext _context;
        private readonly string _uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "wounds");

        public WoundService(DiabetesDbContext context)
        {
            _context = context;
            if (!Directory.Exists(_uploadPath))
                Directory.CreateDirectory(_uploadPath);
        }

        public async Task<WoundRecord> AddRecordAsync(int userId, DateTime recordDate, TimeSpan recordTime, decimal? temperature,
            bool hasInfection, bool hasFever, bool hasOdor, bool hasDischarge, string notes)
        {
            var record = new WoundRecord
            {
                UserId = userId,
                RecordDate = recordDate,
                RecordTime = recordTime,
                SurfaceTemperature = temperature,
                HasInfection = hasInfection,
                HasFever = hasFever,
                HasOdor = hasOdor,
                HasDischarge = hasDischarge,
                Notes = notes,
                CreatedDate = DateTime.Now
            };

            _context.WoundRecords.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<WoundRecord> AddRecordWithPhotoAsync(int userId, DateTime recordDate, TimeSpan recordTime, decimal? temperature,
            bool hasInfection, bool hasFever, bool hasOdor, bool hasDischarge, IFormFile photoFile, string notes)
        {
            string? photoPath = null;
            if (photoFile != null && photoFile.Length > 0)
            {
                var fileName = $"{userId}_{DateTime.Now.Ticks}.jpg";
                var filePath = Path.Combine(_uploadPath, fileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await photoFile.CopyToAsync(stream);
                }
                photoPath = $"/uploads/wounds/{fileName}";
            }

            var record = new WoundRecord
            {
                UserId = userId,
                RecordDate = recordDate,
                RecordTime = recordTime,
                SurfaceTemperature = temperature,
                HasInfection = hasInfection,
                HasFever = hasFever,
                HasOdor = hasOdor,
                HasDischarge = hasDischarge,
                PhotoPath = photoPath,
                Notes = notes,
                CreatedDate = DateTime.Now
            };

            _context.WoundRecords.Add(record);
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task<List<WoundRecord>> GetRecordsByDateRangeAsync(int userId, DateTime startDate, DateTime endDate)
        {
            return await _context.WoundRecords
                .Where(w => w.UserId == userId && w.RecordDate >= startDate && w.RecordDate <= endDate)
                .OrderByDescending(w => w.RecordDate)
                .ToListAsync();
        }

        public async Task<WoundRecord> UpdateRecordAsync(int woundId, decimal? temperature, bool hasInfection, bool hasFever, bool hasOdor, bool hasDischarge, string notes)
        {
            var record = await _context.WoundRecords.FindAsync(woundId);
            if (record == null)
                throw new Exception("伤口记录不存在");

            record.SurfaceTemperature = temperature;
            record.HasInfection = hasInfection;
            record.HasFever = hasFever;
            record.HasOdor = hasOdor;
            record.HasDischarge = hasDischarge;
            record.Notes = notes;
            await _context.SaveChangesAsync();
            return record;
        }

        public async Task DeleteRecordAsync(int woundId)
        {
            var record = await _context.WoundRecords.FindAsync(woundId);
            if (record != null)
            {
                _context.WoundRecords.Remove(record);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<WoundRecord?> GetRecordAsync(int woundId)
        {
            return await _context.WoundRecords
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.WoundId == woundId);
        }
    }
}

