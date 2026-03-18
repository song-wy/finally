using System;

namespace DiabetesPatientApp.Models
{
    public class AdminEntryLog
    {
        public int EntryId { get; set; }
        public string UserType { get; set; } = "Patient";
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}


