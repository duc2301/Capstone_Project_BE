using System;
using System.Collections.Generic;
using System.Text;

using Domain.Common;

namespace Domain.Entities
{
    public class Notification : IEntity
    {
        public Guid Id { get; set; }
        public string Message { get; set; }
        public DateTime SendAt { get; set; }
        public string SenderName { get; set; }
        public bool IsRead { get; set; } = true;
        public Guid AccountId { get; set; }
        public Account Account { get; set; }
    }
}
