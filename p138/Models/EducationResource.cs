using System;

namespace DiabetesPatientApp.Models
{
    public class EducationResource
    {
        public int EducationResourceId { get; set; }
        public int UploaderDoctorId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ResourceType { get; set; } = "文件"; // 视频 / 文件
        public string FileUrl { get; set; } = string.Empty; // e.g. /uploads/education/xxx.mp4
        public string OriginalFileName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

