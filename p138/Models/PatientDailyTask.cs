using System;

namespace DiabetesPatientApp.Models
{
    public class PatientDailyTask
    {
        public int PatientDailyTaskId { get; set; }
        public int PatientId { get; set; }
        public int DoctorOrderId { get; set; }
        public DateTime TaskDate { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

