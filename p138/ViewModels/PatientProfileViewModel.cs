using System;
using System.ComponentModel.DataAnnotations;

namespace DiabetesPatientApp.ViewModels
{
    public class PatientProfileViewModel
    {
        public int UserId { get; set; }

        public string Username { get; set; } = string.Empty;

        [Display(Name = "姓名")]
        public string? FullName { get; set; }

        [Display(Name = "邮箱")]
        [EmailAddress(ErrorMessage = "邮箱格式不正确")]
        public string? Email { get; set; }

        [Display(Name = "联系电话")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "性别")]
        public string? Gender { get; set; }

        [Display(Name = "年龄")]
        [Range(0, 150, ErrorMessage = "年龄请输入 0 到 150 之间的数字")]
        public int? Age { get; set; }

        [Display(Name = "居住情况")]
        public string? ResidenceStatus { get; set; }

        [Display(Name = "糖尿病足类型")]
        public string? DiabeticFootType { get; set; }

        [Display(Name = "病程")]
        public string? DiseaseCourse { get; set; }

        [Display(Name = "确诊日期")]
        public DateTime? DiagnosisDate { get; set; }

        [Display(Name = "就诊前是否已有溃疡")]
        public string? HadUlcerBeforeVisit { get; set; }

        [Display(Name = "是否足部术后患者")]
        public string? IsPostFootSurgeryPatient { get; set; }
    }
}
