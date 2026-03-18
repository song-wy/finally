using System;
using System.Collections.Generic;
using DiabetesPatientApp.ViewModels;

namespace DiabetesPatientApp.ViewModels
{
    public class DoctorOrdersPageViewModel
    {
        public int SelectedPatientId { get; set; }
        public IEnumerable<DoctorPatientSummary> Patients { get; set; } = Array.Empty<DoctorPatientSummary>();
        public IEnumerable<DoctorOrderItem> Orders { get; set; } = Array.Empty<DoctorOrderItem>();
        public DateTime Today { get; set; } = DateTime.Today;
        public IEnumerable<DoctorDailyTaskItem> TodayTasks { get; set; } = Array.Empty<DoctorDailyTaskItem>();
        public int AnalyticsDays { get; set; } = 7;
        public List<string> AnalyticsLabels { get; set; } = new();
        public List<int> AnalyticsTotalTasks { get; set; } = new();
        public List<int> AnalyticsCompletedTasks { get; set; } = new();
        public List<DoctorTaskCategorySummary> AnalyticsByCategory { get; set; } = new();
        public string? StatusMessage { get; set; }
    }

    public class DoctorOrderItem
    {
        public int DoctorOrderId { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DoctorDailyTaskItem
    {
        public int PatientDailyTaskId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class DoctorTaskCategorySummary
    {
        public string Category { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Completed { get; set; }
    }
}

