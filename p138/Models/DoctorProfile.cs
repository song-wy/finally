using System;

namespace DiabetesPatientApp.Models
{
    public class DoctorProfile
    {
        public int DoctorProfileId { get; set; }
        public int UserId { get; set; }
        public string Department { get; set; } = string.Empty;
        public string ProfessionalTitle { get; set; } = string.Empty;
        public string HospitalName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public string Introduction { get; set; } = string.Empty;
        public string ConsultationHours { get; set; } = string.Empty;
        public string ClinicAddress { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public User? User { get; set; }
    }
}
