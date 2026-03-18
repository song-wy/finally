using System;

namespace DiabetesPatientApp.Models
{
    public class FootPressureRiskAlert
    {
        public DateTime RecordDate { get; set; }
        public TimeSpan RecordTime { get; set; }
        public string Side { get; set; } = string.Empty; // 左脚/右脚
        public decimal? Pressure { get; set; } // kPa
        public string RiskLevel { get; set; } = string.Empty; // 高风险/极高风险

        public string DisplayTime => $"{RecordDate:yyyy-MM-dd} {RecordTime:hh\\:mm}";
        public string Message =>
            Pressure.HasValue
                ? $"{DisplayTime} {Side}{RiskLevel}（{Pressure.Value:F0} kPa）"
                : $"{DisplayTime} {Side}{RiskLevel}";
    }
}

