using System;

namespace DiabetesPatientApp.Models
{
    /// <summary>
    /// 医生发送给患者的问卷任务：患者填写后提交，自动保存并回传给医生端查看。
    /// </summary>
    public class QuestionnaireAssignment
    {
        public int QuestionnaireAssignmentId { get; set; }

        public int DoctorId { get; set; }
        public int PatientId { get; set; }

        /// <summary>
        /// 生成时的问卷 JSON（AiQuestionnaireViewModel 序列化结果）
        /// </summary>
        public string QuestionnaireJson { get; set; } = string.Empty;

        /// <summary>
        /// 患者提交的答案 JSON（包含每题文本回答+评分）
        /// </summary>
        public string? AnswerJson { get; set; }

        /// <summary>
        /// 访问令牌：用于患者填写链接校验
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? SubmittedAt { get; set; }
    }
}

