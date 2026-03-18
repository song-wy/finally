using System;

namespace DiabetesPatientApp.Models
{
    public class WoundRecord
    {
        public int WoundId { get; set; }
        public int UserId { get; set; }
        public DateTime RecordDate { get; set; }
        public TimeSpan RecordTime { get; set; }
        public decimal? SurfaceTemperature { get; set; }
        public string? WoundStatus { get; set; }
        public bool HasInfection { get; set; }
        public bool HasFever { get; set; }
        public bool HasOdor { get; set; }
        public bool HasDischarge { get; set; }
        public string? PhotoPath { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; }

        public virtual User? User { get; set; }

        public string GetStatusSummary()
        {
            var status = new System.Collections.Generic.List<string>();
            if (HasInfection) status.Add("感染");
            if (HasFever) status.Add("发烧");
            if (HasOdor) status.Add("恶臭");
            if (HasDischarge) status.Add("分泌物");
            
            return status.Count > 0 ? string.Join(", ", status) : "正常";
        }
    }
}

