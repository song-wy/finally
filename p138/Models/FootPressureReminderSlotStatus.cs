namespace DiabetesPatientApp.Models
{
    public class FootPressureReminderSlotStatus
    {
        public string Label { get; set; } = string.Empty;
        public string Range { get; set; } = string.Empty;
        public bool Completed { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string StatusText { get; set; } = string.Empty;
    }
}
