using System;
using System.Collections.Generic;

namespace DiabetesPatientApp.Models
{
    public class Post
    {
        public int PostId { get; set; }
        public int UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsAnonymous { get; set; }
        public DateTime CreatedDate { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
