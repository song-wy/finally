using System;
using System.Collections.Generic;

namespace DiabetesPatientApp.ViewModels
{
    public class DoctorDashboardViewModel
    {
        public string DoctorName { get; set; } = "医生";
        public int PatientCount { get; set; }
        public int UnreadMessageCount { get; set; }
        public int NotificationCount { get; set; }
        public int TodayConsultationCount { get; set; }
        public int ActiveReminderCount { get; set; }
        public List<string> TrendLabels { get; set; } = new();
        public List<int> TrendBloodSugarAlerts { get; set; } = new();
        public List<int> TrendFootPressureAlerts { get; set; } = new();
        public List<int> TrendWoundAlerts { get; set; } = new();
        public IEnumerable<DoctorPatientSummary> RecentPatients { get; set; } = Array.Empty<DoctorPatientSummary>();
        public IEnumerable<DoctorMessageSummary> RecentUnreadMessages { get; set; } = Array.Empty<DoctorMessageSummary>();
        public IEnumerable<DoctorConsultationPatientSummary> ConsultationPatients { get; set; } = Array.Empty<DoctorConsultationPatientSummary>();
        public IEnumerable<DoctorPendingReplySummary> PendingReplies { get; set; } = Array.Empty<DoctorPendingReplySummary>();
        public IEnumerable<DoctorNotificationSummary> Notifications { get; set; } = Array.Empty<DoctorNotificationSummary>();
        /// <summary>首页高危患者预警（基于患者端自动推送预警汇总）</summary>
        public IEnumerable<DoctorPatientAlertSummary> HomeHighRiskPatients { get; set; } = Array.Empty<DoctorPatientAlertSummary>();
    }

    public class DoctorNotificationPageViewModel
    {
        public int NotificationCount { get; set; }
        public IEnumerable<DoctorNotificationSummary> Notifications { get; set; } = Array.Empty<DoctorNotificationSummary>();
    }

    public class DoctorPatientSummary
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
    }

    public class DoctorMessageSummary
    {
        public int MessageId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Preview { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    public class DoctorConsultationPatientSummary
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string LastMessagePreview { get; set; } = string.Empty;
        public DateTime LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public bool ShowUnreadCount { get; set; }
    }

    public class DoctorPendingReplySummary
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string LatestMessagePreview { get; set; } = string.Empty;
        public DateTime LatestMessageTime { get; set; }
        public int UnreadCount { get; set; }
        public bool ShowUnreadCount { get; set; }
    }

    public class DoctorNotificationSummary
    {
        public string NotificationType { get; set; } = string.Empty;
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime NotificationTime { get; set; }
        public int UnreadCount { get; set; }
        public bool ShowUnreadCount { get; set; }
        public DateTime? NextFollowUpDate { get; set; }
    }

    public class DoctorPatientArchiveViewModel
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Gender { get; set; }
        public string? PhoneNumber { get; set; }
        public int? Age { get; set; }
        public string? ResidenceStatus { get; set; }
        public string? DiabeticFootType { get; set; }
        public string? DiseaseCourse { get; set; }
        public DateTime? DiagnosisDate { get; set; }
        public string? HadUlcerBeforeVisit { get; set; }
        public string? IsPostFootSurgeryPatient { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public int BloodSugarRecordCount { get; set; }
        public int WoundRecordCount { get; set; }
        public int FootPressureRecordCount { get; set; }
        public int ConsultationCount { get; set; }
        public DoctorArchiveRecordSummary? LatestBloodSugarRecord { get; set; }
        public DoctorArchiveRecordSummary? LatestWoundRecord { get; set; }
        public DoctorArchiveRecordSummary? LatestFootPressureRecord { get; set; }
        public DoctorArchiveRecordSummary? LatestMedicationRecord { get; set; }
        public DoctorArchiveRecordSummary? LatestDietRecord { get; set; }
        public DoctorArchiveRecordSummary? LatestExerciseRecord { get; set; }
        public IEnumerable<DoctorArchiveMessageSummary> RecentMessages { get; set; } = Array.Empty<DoctorArchiveMessageSummary>();
    }

    public class DoctorArchiveRecordSummary
    {
        public int RecordId { get; set; }
        public string ItemKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public DateTime RecordDateTime { get; set; }
    }

    public class DoctorArchiveMessageSummary
    {
        public int MessageId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
    }

    /// <summary>
    /// 医生端自定义分组页：自动风险分组（由患者异常数据聚合）。
    /// </summary>
    public class DoctorRiskGroupSummary
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string BadgeClass { get; set; } = "bg-secondary";
        public List<int> PatientIds { get; set; } = new();
        public Dictionary<int, DateTime> LatestDates { get; set; } = new();
    }

    /// <summary>
    /// 医生端高危患者预警：某患者在统计周期内触发的预警汇总
    /// </summary>
    public class DoctorPatientAlertSummary
    {
        public int PatientId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public int BloodSugarAlertCount { get; set; }
        public int FootPressureAlertCount { get; set; }
        public int WoundAlertCount { get; set; }
        public DateTime? LatestAlertDate { get; set; }
        /// <summary>该患者最新一条自动推送的预警消息（用于合并表格展示）</summary>
        public HighRiskAlertNotificationItem? LatestPush { get; set; }
    }

    public class DoctorPatientAlertsPageViewModel
    {
        public string DateRangeText { get; set; } = "最近30天";
        public IEnumerable<DoctorPatientAlertSummary> Patients { get; set; } = Array.Empty<DoctorPatientAlertSummary>();
        /// <summary>患者端触发后自动发送的预警消息（按时间倒序）</summary>
        public IEnumerable<HighRiskAlertNotificationItem> LatestNotifications { get; set; } = Array.Empty<HighRiskAlertNotificationItem>();
    }

    /// <summary>
    /// 高危预警通知项（供医生端列表展示）
    /// </summary>
    public class HighRiskAlertNotificationItem
    {
        public int NotificationId { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string AlertTypeDisplay { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
