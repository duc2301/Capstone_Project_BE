using System;
using System.Collections.Generic;
using System.Text;

using Domain.Common;

namespace Domain.Entities
{
    public class Notification : IEntity
    {
        public Guid Id { get; set; }
        public string? Message { get; set; }
        public DateTime SendAt { get; set; }
        public string? SenderName { get; set; }
        public bool IsRead { get; set; } = true;
        public bool IsEmailSent { get; set; } = false;
        public Guid AccountId { get; set; }

        // Liên kết tới đối tượng nguồn (Submittal/Discussion/Issue/FileVersion...) -> FE bấm vô nhảy đúng chỗ
        public string? LinkType { get; set; }
        public string? LinkId { get; set; }

        public Account? Account { get; set; }
    }
}
