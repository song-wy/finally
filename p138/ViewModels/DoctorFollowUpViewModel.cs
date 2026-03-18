using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DiabetesPatientApp.ViewModels
{
    public class DoctorFollowUpPageViewModel
    {
        public DoctorFollowUpCreateRequest Form { get; set; } = new();
        public IEnumerable<DoctorFollowUpPatientOption> Patients { get; set; } = Array.Empty<DoctorFollowUpPatientOption>();
        public IEnumerable<DoctorFollowUpRecordItem> Records { get; set; } = Array.Empty<DoctorFollowUpRecordItem>();
        public string? StatusMessage { get; set; }
    }

    public class DoctorFollowUpCreateRequest
    {
        [Required]
        public int PatientId { get; set; }

        [Required]
        public DateTime FollowUpDate { get; set; } = DateTime.Today;

        [Required]
        public string FollowUpMethod { get; set; } = "电话回访";

        [Required]
        public string Summary { get; set; } = string.Empty;

        public string? Advice { get; set; }

        public DateTime? NextFollowUpDate { get; set; }
    }

    public class DoctorFollowUpPatientOption
    {
        public int PatientId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class DoctorFollowUpRecordItem
    {
        public int FollowUpRecordId { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public DateTime FollowUpDate { get; set; }
        public string FollowUpMethod { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string? Advice { get; set; }
        public DateTime? NextFollowUpDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
