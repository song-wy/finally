using System;

namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 医生端“患者档案-最近健康记录”的隐藏项（不删除患者原始数据）。
    /// </summary>
    public class DoctorHiddenHealthItem
    {
        public int DoctorHiddenHealthItemId { get; set; }
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public string ItemKey { get; set; } = string.Empty;
        public DateTime HiddenAt { get; set; } = DateTime.Now;
    }
}

