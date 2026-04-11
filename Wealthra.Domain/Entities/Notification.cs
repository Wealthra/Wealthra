using System;
using Wealthra.Domain.Common;
using Wealthra.Domain.Enums;

namespace Wealthra.Domain.Entities
{
    public class Notification : BaseEntity
    {
        public string UserId { get; set; } // Maps to Identity User ID
        public string MessageEn { get; set; }
        public string MessageTr { get; set; }
        public NotificationType Type { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        // Optional link to related entity
        public int? RelatedEntityId { get; set; }
    }
}