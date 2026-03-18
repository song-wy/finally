using System;

namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 医生自定义患者分组
    /// </summary>
    public class DoctorCustomGroup
    {
        public int GroupId { get; set; }
        public int DoctorId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public virtual User? Doctor { get; set; }
    }
}

