using System;

namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 高危预警通知：患者端触发异常时写入，供医生端「高危患者预警」接收与展示。
    /// </summary>
    public class HighRiskAlertNotification
    {
        public int NotificationId { get; set; }
        public int PatientId { get; set; }
        /// <summary>BloodSugarHigh / BloodSugarLow / FootPressureHigh / WoundAbnormal</summary>
        public string AlertType { get; set; } = string.Empty;
        /// <summary>简要说明，如「高血糖 10.8 mmol/L」</summary>
        public string Summary { get; set; } = string.Empty;
        /// <summary>关联记录主键（血糖/足压/伤口）</summary>
        public int? RelatedRecordId { get; set; }
        /// <summary>BloodSugar / FootPressure / Wound</summary>
        public string? RelatedTable { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual User? Patient { get; set; }
    }
}
