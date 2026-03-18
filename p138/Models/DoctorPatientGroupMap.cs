using System;

namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 医生-患者-分组关联
    /// </summary>
    public class DoctorPatientGroupMap
    {
        public int Id { get; set; }
        public int DoctorId { get; set; }
        public int PatientId { get; set; }
        public int GroupId { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual DoctorCustomGroup? Group { get; set; }
        public virtual User? Patient { get; set; }
    }
}

