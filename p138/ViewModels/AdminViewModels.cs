using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DiabetesPatientApp.ViewModels
{
    public class AdminDashboardViewModel
    {
        public string AdminName { get; set; } = "管理员";
        public int PatientCount { get; set; }
        public int ActivePatientCount { get; set; }
        public int DoctorCount { get; set; }
        public int BloodSugarRecordCount { get; set; }
        public int WoundRecordCount { get; set; }
        public int FootPressureRecordCount { get; set; }
        public IEnumerable<AdminPatientSummary> RecentPatients { get; set; } = Array.Empty<AdminPatientSummary>();
        public IEnumerable<AdminDoctorSummary> RecentDoctors { get; set; } = Array.Empty<AdminDoctorSummary>();
        public IEnumerable<AdminReminderSummary> UpcomingReminders { get; set; } = Array.Empty<AdminReminderSummary>();
        /// <summary>时间轴日期标签（如最近30天 MM-dd）</summary>
        public List<string> TimelineLabels { get; set; } = new List<string>();
        /// <summary>高危足预警（足压高风险/极高风险记录累计）</summary>
        public List<int> TimelineHighRiskFootCounts { get; set; } = new List<int>();
        /// <summary>换药量（伤口记录累计）</summary>
        public List<int> TimelineDressingCounts { get; set; } = new List<int>();
        /// <summary>血糖值（血糖记录数累计）</summary>
        public List<int> TimelineBloodSugarCounts { get; set; } = new List<int>();
        /// <summary>伤口上传数（伤口记录累计）</summary>
        public List<int> TimelineWoundCounts { get; set; } = new List<int>();
        /// <summary>足压记录数（足压记录累计）</summary>
        public List<int> TimelineFootPressureCounts { get; set; } = new List<int>();
        /// <summary>患者数（累计）</summary>
        public List<int> TimelinePatientCounts { get; set; } = new List<int>();
        /// <summary>医生数（累计）</summary>
        public List<int> TimelineDoctorCounts { get; set; } = new List<int>();
    }

    public class AdminPatientSummary
    {
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class AdminDoctorSummary
    {
        public int UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ProfessionalTitle { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class AdminReminderSummary
    {
        public int ReminderId { get; set; }
        public string? Title { get; set; }
        public DateTime ReminderTime { get; set; }
        public string? PatientName { get; set; }
    }

    public class AdminDataEntryViewModel
    {
        public List<AdminPatientSummary> LatestPatients { get; set; } = new List<AdminPatientSummary>();
        public List<AdminDoctorSummary> LatestDoctors { get; set; } = new List<AdminDoctorSummary>();
    }

    public class AdminAnalyticsViewModel
    {
        public int PatientCount { get; set; }
        public int DoctorCount { get; set; }
        public int TotalBloodSugarRecords { get; set; }
        public double? AverageBloodSugar { get; set; }
        public int HighRiskBloodSugarCount { get; set; }
        public int TotalFootPressureRecords { get; set; }
        public int HighPressureCases { get; set; }
        public int TotalWoundRecords { get; set; }
        public int OpenWoundCases { get; set; }
        public int ConsultationMessageCount { get; set; }
        public int ActiveReminderCount { get; set; }
        public int FollowUpRecordCount { get; set; }
        public int QuestionnaireCount { get; set; }
        public int PostCount { get; set; }
        public int CommentCount { get; set; }
        /// <summary>患者端统计项标签（用于图表）</summary>
        public List<string> PatientChartLabels { get; set; } = new List<string>();
        /// <summary>患者端统计项数值</summary>
        public List<int> PatientChartData { get; set; } = new List<int>();
        /// <summary>医生端统计项标签（用于图表）</summary>
        public List<string> DoctorChartLabels { get; set; } = new List<string>();
        /// <summary>医生端统计项数值</summary>
        public List<int> DoctorChartData { get; set; } = new List<int>();
        /// <summary>简要分析结论（用于分析展示）</summary>
        public List<AnalyticsConclusionItem> AnalysisConclusions { get; set; } = new List<AnalyticsConclusionItem>();
    }

    public class AnalyticsConclusionItem
    {
        public string Level { get; set; } = "Info";
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class AdminDataMaintenanceViewModel
    {
        public int InactivePatientCount { get; set; }
        public int PendingReminderCount { get; set; }
        public int OldConsultationMessageCount { get; set; }
        public DateTime? LastPatientLogin { get; set; }
        public IEnumerable<AdminPatientSummary> Patients { get; set; } = Array.Empty<AdminPatientSummary>();
        public IEnumerable<AdminDoctorSummary> Doctors { get; set; } = Array.Empty<AdminDoctorSummary>();
        public string? StatusMessage { get; set; }
    }

    public class AdminDocumentManagementViewModel
    {
        public IEnumerable<AdminDocumentFile> Files { get; set; } = Array.Empty<AdminDocumentFile>();
        public string? StatusMessage { get; set; }
    }

    public class AdminDocumentFile
    {
        public string FileName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
		public string RelativePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    public class AdminUserCreateRequest
    {
        [Required]
        [Display(Name = "用户名")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "邮箱")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "姓名")]
        public string? FullName { get; set; }

        [Display(Name = "性别")]
        public string? Gender { get; set; }

        [Display(Name = "电话")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "登录密码")]
        public string? Password { get; set; }

        public string? ReturnView { get; set; }
    }

    public class AdminUserExport
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public string UserType { get; set; } = "Patient";
        public string? PasswordHash { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class AdminBackupPackage
    {
        public DateTime ExportedAt { get; set; }
        public IEnumerable<AdminUserExport> Patients { get; set; } = Array.Empty<AdminUserExport>();
        public IEnumerable<AdminUserExport> Doctors { get; set; } = Array.Empty<AdminUserExport>();
    }

    public class AdminUserUpdateRequest
    {
        [Required]
        public int UserId { get; set; }

        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
    }
}

