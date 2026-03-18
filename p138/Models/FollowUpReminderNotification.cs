using System;

namespace DiabetesPatientApp.Models
{
    public class FollowUpReminderNotification
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public int FollowUpRecordId { get; set; }
        public DateTime NextFollowUpDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        public virtual User? Patient { get; set; }
    }
}

