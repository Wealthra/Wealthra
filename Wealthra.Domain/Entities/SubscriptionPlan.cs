using Wealthra.Domain.Common;

namespace Wealthra.Domain.Entities
{
    public class SubscriptionPlan : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MonthlyOcrLimit { get; set; }
        public int MonthlySttLimit { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? UpdatedOn { get; set; }
    }
}
