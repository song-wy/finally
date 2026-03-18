using System;

namespace DiabetesPatientApp.Models
{
    public class Reminder
    {
        public int ReminderId { get; set; }
        public int UserId { get; set; }
        public string? MealType { get; set; }
        public TimeSpan ReminderTime { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }

        public virtual User? User { get; set; }
    }
}

