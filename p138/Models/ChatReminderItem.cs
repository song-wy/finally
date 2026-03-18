namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 聊天记录提醒：某医生发给当前患者且未读的消息条数
    /// </summary>
    public class ChatReminderItem
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = "";
        public int UnreadCount { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string FollowUpSummary { get; set; } = "";
        public string FollowUpAdvice { get; set; } = "";
    }
}
