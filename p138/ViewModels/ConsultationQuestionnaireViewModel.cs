using System;
using System.Collections.Generic;
using DiabetesPatientApp.Models;

namespace DiabetesPatientApp.ViewModels
{
    public class ConsultationQuestionnaireViewModel
    {
        public bool IsDoctorPortal { get; set; }
        public string PageTitle { get; set; } = "在线咨询";
        public string Keyword { get; set; } = string.Empty;
        public string Requirements { get; set; } = string.Empty;
        public string? GenerationError { get; set; }
        public AiQuestionnaireViewModel? GeneratedQuestionnaire { get; set; }
        public List<ConsultationMessage> Messages { get; set; } = new();
        public List<DoctorCollectedQuestionnaireSummary> CollectedQuestionnaires { get; set; } = new();
    }

    public class AiQuestionnaireViewModel
    {
        public string Title { get; set; } = string.Empty;
        public string Introduction { get; set; } = string.Empty;
        public List<string> Questions { get; set; } = new();
        public string Source { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }

    public class QuestionnaireResultPageViewModel
    {
        public AiQuestionnaireViewModel Questionnaire { get; set; } = new();
        public List<QuestionnairePatientOption> Patients { get; set; } = new();
        public List<int> SelectedPatientIds { get; set; } = new();
    }

    public class QuestionnairePatientOption
    {
        public int PatientId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public class DoctorCollectedQuestionnaireSummary
    {
        public int QuestionnaireAssignmentId { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public bool IsSubmitted => SubmittedAt.HasValue;
    }
}
