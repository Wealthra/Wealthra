using System;

namespace Wealthra.Domain.Common
{
    public abstract class AuditableEntity : BaseEntity
    {
        public string CreatedBy { get; set; }
        public DateTimeOffset CreatedOn { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTimeOffset? LastModifiedOn { get; set; }
    }
}