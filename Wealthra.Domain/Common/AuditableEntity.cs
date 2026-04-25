using System;

namespace Wealthra.Domain.Common
{
    public abstract class AuditableEntity : BaseEntity
    {
        public string CreatedBy { get; set; } = string.Empty;
        public DateTimeOffset CreatedOn { get; set; }
        public string LastModifiedBy { get; set; } = string.Empty;
        public DateTimeOffset? LastModifiedOn { get; set; }
    }
}