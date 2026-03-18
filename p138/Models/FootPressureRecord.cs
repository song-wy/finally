using System;

namespace DiabetesPatientApp.Models
{
    public class FootPressureRecord
    {
        public int FootPressureId { get; set; }
        public int UserId { get; set; }
        public DateTime RecordDate { get; set; }
        public TimeSpan RecordTime { get; set; }
        
        // 左脚压力数据
        public decimal? LeftFootPressure { get; set; }
        public string? LeftFootStatus { get; set; }
        
        // 右脚压力数据
        public decimal? RightFootPressure { get; set; }
        public string? RightFootStatus { get; set; }
        
        // 整体评估
        public string? OverallAssessment { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; }

        public virtual User? User { get; set; }

        // 判断足部压力状态（单位：kPa）
        // 低于150为低风险；150-200为中风险；200-300为高风险；大于300为极高风险
        public static string DeterminePressureStatus(decimal? pressure)
        {
            if (pressure == null) return "未测量";
            if (pressure < 150) return "低风险";
            if (pressure < 200) return "中风险";
            if (pressure < 300) return "高风险";
            return "极高风险";
        }

        // 获取足部状态摘要
        public string GetStatusSummary()
        {
            var summary = "";
            if (!string.IsNullOrEmpty(LeftFootStatus))
                summary += $"左脚: {LeftFootStatus}";
            if (!string.IsNullOrEmpty(RightFootStatus))
            {
                if (!string.IsNullOrEmpty(summary))
                    summary += " | ";
                summary += $"右脚: {RightFootStatus}";
            }
            return !string.IsNullOrEmpty(summary) ? summary : "未记录";
        }
    }
}

