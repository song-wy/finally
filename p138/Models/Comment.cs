using System;

namespace DiabetesPatientApp.Models
{
    public class Comment
    {
        public int CommentId { get; set; }
        public int PostId { get; set; }
        public int UserId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; }
        public DateTime CreatedDate { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual Post Post { get; set; } = null!;
    }
}
