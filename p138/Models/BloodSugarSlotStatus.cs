namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 血糖记录时间段完成状态，用于提醒设置页的“消息通知”列
    /// </summary>
    public class BloodSugarSlotStatus
    {
        public string Label { get; set; } = string.Empty;
        public string Range { get; set; } = string.Empty;
        public bool Completed { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
    }
}
