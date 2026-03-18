using System;

namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 医生端“回访登记-最近回访记录”的隐藏项（不删除数据库原始记录）。
    /// </summary>
    public class DoctorHiddenFollowUpRecord
    {
        public int DoctorHiddenFollowUpRecordId { get; set; }
        public int DoctorId { get; set; }
        public int FollowUpRecordId { get; set; }
        public DateTime HiddenAt { get; set; } = DateTime.Now;
    }
}

