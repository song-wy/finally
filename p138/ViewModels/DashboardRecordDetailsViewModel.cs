using System;
using System.Collections.Generic;

namespace DiabetesPatientApp.ViewModels
{
    public class DashboardRecordDetailsViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string PeriodLabel { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public List<DashboardRecordDetailRowViewModel> Rows { get; set; } = new();

        // 今日详情页：最近7天时间轴（患者本人）
        public bool ShowTimeline { get; set; }
        public List<string> TimelineLabels { get; set; } = new();
        public List<int> TimelineBloodSugarCounts { get; set; } = new();
        public List<int> TimelineWoundCounts { get; set; } = new();
        public List<int> TimelineFootPressureCounts { get; set; } = new();
        /// <summary>基于时间轴数据生成的图表分析文案</summary>
        public string ChartAnalysisText { get; set; } = string.Empty;
        /// <summary>时间轴说明（今日/近一周/本月）</summary>
        public string TimelineHint { get; set; } = string.Empty;
    }

    public class DashboardRecordDetailRowViewModel
    {
        public string DataType { get; set; } = string.Empty;
        public DateTime RecordDate { get; set; }
        public string RecordTimeText { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
