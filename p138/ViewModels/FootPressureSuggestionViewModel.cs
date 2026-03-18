namespace DiabetesPatientApp.ViewModels
{
    /// <summary>
    /// 足部压力 AI 分析建议单视图模型
    /// </summary>
    public class FootPressureSuggestionViewModel
    {
        public int RecordId { get; set; }
        public string? PatientName { get; set; }
        public int? Age { get; set; }
        public string? LeftStatus { get; set; }
        public string? RightStatus { get; set; }
        public decimal? LeftPressure { get; set; }
        public decimal? RightPressure { get; set; }

        /// <summary>日常行为建议</summary>
        public string DailyBehaviorAdvice { get; set; } = string.Empty;

        /// <summary>随访建议</summary>
        public string FollowUpAdvice { get; set; } = string.Empty;

        /// <summary>减压鞋垫选择建议</summary>
        public string InsoleAdvice { get; set; } = string.Empty;

        /// <summary>压力过载 / 步态异常 / 行走超时等综合分析与建议</summary>
        public string RiskPatternAdvice { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; }
    }
}
