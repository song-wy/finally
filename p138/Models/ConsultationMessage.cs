using System;

namespace DiabetesPatientApp.Models
{
    public class ConsultationMessage
    {
        public int MessageId { get; set; }
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public string? MessageType { get; set; } // 'Text', 'Voice'
        public string? MessageContent { get; set; }
        public string? VoiceFilePath { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; }

        public virtual User? Sender { get; set; }
        public virtual User? Receiver { get; set; }
    }
}

