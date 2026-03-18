using System;

namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 医生端“患者档案-最近咨询内容”的隐藏消息（不删除数据库消息，患者端仍可见）。
    /// </summary>
    public class DoctorHiddenConsultationMessage
    {
        public int DoctorHiddenConsultationMessageId { get; set; }
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public int MessageId { get; set; }
        public DateTime HiddenAt { get; set; } = DateTime.Now;
    }
}

