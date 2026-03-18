namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 伤口监护：每周上传统计项
    /// </summary>
    public class WoundWeekStat
    {
        public string Label { get; set; } = string.Empty;
        public bool Uploaded { get; set; }
    }
}
