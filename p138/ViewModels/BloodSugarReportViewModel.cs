namespace DiabetesPatientApp.ViewModels
{
    /// <summary>
    /// 血糖分析报告视图模型
    /// </summary>
    public class BloodSugarReportViewModel
    {
        public DateTime ReportDate { get; set; }
        public int Days { get; set; }

        #region 患者基本信息
        public string? PatientName { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        #endregion

        #region 监测概况
        public string MonitoringPeriodText { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public string FrequencyText { get; set; } = string.Empty;
        #endregion

        #region 血糖控制达标率
        /// <summary>血糖趋势分析（AI 生成时可选）</summary>
        public string TrendAnalysis { get; set; } = string.Empty;
        public decimal FastingComplianceRate { get; set; }
        public decimal AfterMealComplianceRate { get; set; }
        public string FastingComplianceText { get; set; } = string.Empty;
        public string AfterMealComplianceText { get; set; } = string.Empty;
        #endregion

        #region 相关风险
        public string HyperglycemiaRiskText { get; set; } = string.Empty;
        public string HypoglycemiaRiskText { get; set; } = string.Empty;
        #endregion

        #region 建议
        public string DietSuggestion { get; set; } = string.Empty;
        public string MedicationSuggestion { get; set; } = string.Empty;
        public string ExerciseSuggestion { get; set; } = string.Empty;
        public string MonitoringSuggestion { get; set; } = string.Empty;
        #endregion
    }
}
