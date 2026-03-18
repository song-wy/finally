using System;

namespace DiabetesPatientApp.Models
{
    public class DoctorOrder
    {
        public int DoctorOrderId { get; set; }
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        /// <summary>用药/运动/饮食/其他</summary>
        public string Category { get; set; } = "其他";
        public string Content { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}

