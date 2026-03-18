using System;
using System.Collections.Generic;

namespace DiabetesPatientApp.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public string? ResidenceStatus { get; set; }
        public string? DiabeticFootType { get; set; }
        public string? DiseaseCourse { get; set; }
        public DateTime? DiagnosisDate { get; set; }
        /// <summary>就诊前是否已有溃疡（是/否）</summary>
        public string? HadUlcerBeforeVisit { get; set; }
        /// <summary>是否足部术后患者（是/否）</summary>
        public string? IsPostFootSurgeryPatient { get; set; }
        public string? UserType { get; set; } // 'Patient', 'Doctor', 'Nurse'
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public bool IsActive { get; set; }
        public virtual DoctorProfile? DoctorProfile { get; set; }

        public virtual ICollection<BloodSugarRecord> BloodSugarRecords { get; set; } = new List<BloodSugarRecord>();
        public virtual ICollection<WoundRecord> WoundRecords { get; set; } = new List<WoundRecord>();
        public virtual ICollection<ConsultationMessage> SentMessages { get; set; } = new List<ConsultationMessage>();
        public virtual ICollection<ConsultationMessage> ReceivedMessages { get; set; } = new List<ConsultationMessage>();
        public virtual ICollection<FootPressureRecord> FootPressureRecords { get; set; } = new List<FootPressureRecord>();
    }
}

