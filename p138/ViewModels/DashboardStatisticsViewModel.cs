using System;
using System.Collections.Generic;

namespace DiabetesPatientApp.ViewModels
{
    public class DashboardStatisticsViewModel
    {
        public DateTime ReportTime { get; set; }
        public string TodayLabel { get; set; } = string.Empty;
        public string WeekLabel { get; set; } = string.Empty;
        public string MonthLabel { get; set; } = string.Empty;
        public int TodayTotalCount { get; set; }
        public int WeekTotalCount { get; set; }
        public int MonthTotalCount { get; set; }
        public List<DashboardStatisticRowViewModel> Rows { get; set; } = new();
    }

    public class DashboardStatisticRowViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int TodayCount { get; set; }
        public int WeekCount { get; set; }
        public int MonthCount { get; set; }
    }
}
