using System;

namespace DiabetesPatientApp.Models
{
    public class BloodSugarRecord
    {
        private const decimal MgDlPerMmolL = 18m;

        public int RecordId { get; set; }
        public int UserId { get; set; }
        public DateTime RecordDate { get; set; }
        public TimeSpan RecordTime { get; set; }
        public string? MealType { get; set; } // 'BeforeMeal', 'AfterMeal', 'Fasting'
        public decimal BloodSugarValue { get; set; }
        public string? Status { get; set; } // 'Normal', 'High', 'Low'
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; }

        public virtual User? User { get; set; }

        public decimal BloodSugarValueMmol => Math.Round(BloodSugarValue / MgDlPerMmolL, 2, MidpointRounding.AwayFromZero);

        public static decimal MmolToMgDl(decimal mmol) => mmol * MgDlPerMmolL;

        public string GetStatusColor()
        {
            return Status switch
            {
                "Normal" => "green",
                "High" => "red",
                "Low" => "orange",
                _ => "gray"
            };
        }

        public static string DetermineStatus(decimal value)
        {
            if (value >= 70 && value <= 130)
                return "Normal";
            else if (value > 130)
                return "High";
            else
                return "Low";
        }

        public static string DetermineStatus(decimal valueMgDl, string? mealType)
        {
            // UI 录入单位为 mmol/L，但数据库存储仍为 mg/dL，因此这里用 mg/dL 阈值判断
            var fastingLow = 4.4m * MgDlPerMmolL;
            var fastingHigh = 7.0m * MgDlPerMmolL;

            var afterMealLow = 6.0m * MgDlPerMmolL;
            var afterMealHigh = 10.0m * MgDlPerMmolL;

            if (string.Equals(mealType, "Fasting", StringComparison.OrdinalIgnoreCase))
            {
                if (valueMgDl < fastingLow) return "Low";
                if (valueMgDl > fastingHigh) return "High";
                return "Normal";
            }

            if (string.Equals(mealType, "AfterMeal", StringComparison.OrdinalIgnoreCase))
            {
                if (valueMgDl < afterMealLow) return "Low";
                if (valueMgDl > afterMealHigh) return "High";
                return "Normal";
            }

            // 兜底：未知餐次时沿用旧逻辑（mg/dL 70-130）
            return DetermineStatus(valueMgDl);
        }
    }
}

