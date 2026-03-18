using System;

namespace DiabetesPatientApp.Models
{
    public class FollowUpRecord
    {
        public int FollowUpRecordId { get; set; }
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public DateTime FollowUpDate { get; set; }
        public string FollowUpMethod { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string? Advice { get; set; }
        public DateTime? NextFollowUpDate { get; set; }
        public DateTime CreatedDate { get; set; }

        public virtual User? Doctor { get; set; }
        public virtual User? Patient { get; set; }
    }
}
