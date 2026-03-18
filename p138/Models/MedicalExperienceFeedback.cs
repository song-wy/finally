using System;

namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 患者就医体验评价（1-5 对应 emoji 等级）
    /// </summary>
    public class MedicalExperienceFeedback
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        /// <summary>对医生的评价 1-5</summary>
        public int DoctorRating { get; set; }
        /// <summary>对线上就医的评价 1-5</summary>
        public int OnlineConsultRating { get; set; }
        /// <summary>对该系统的评价 1-5</summary>
        public int SystemRating { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual User? User { get; set; }
    }
}
