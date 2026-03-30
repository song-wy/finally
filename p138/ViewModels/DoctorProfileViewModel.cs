using System.ComponentModel.DataAnnotations;

namespace DiabetesPatientApp.ViewModels
{
    public class DoctorProfileViewModel
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

        [Display(Name = "科室")]
        public string? Department { get; set; }

        [Display(Name = "职称")]
        public string? ProfessionalTitle { get; set; }

        [Display(Name = "所属医院")]
        public string? HospitalName { get; set; }

        [Display(Name = "专长")]
        public string? Specialty { get; set; }

        [Display(Name = "门诊时间")]
        public string? ConsultationHours { get; set; }

        [Display(Name = "诊所地址")]
        public string? ClinicAddress { get; set; }

        [Display(Name = "个人简介")]
        public string? Introduction { get; set; }
    }
}
