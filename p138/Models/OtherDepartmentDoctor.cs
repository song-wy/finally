using System;

namespace DiabetesPatientApp.Models
{
    public class OtherDepartmentDoctor
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}

