using System;
using Wealthra.Domain.Common;

namespace Wealthra.Domain.Entities
{
    public class Expense : AuditableEntity
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TRY";
        public string PaymentMethod { get; set; } = string.Empty;
        public bool IsRecurring { get; set; }
        public DateTime TransactionDate { get; set; }

        public int CategoryId { get; set; }
        public virtual Category Category { get; set; } = null!;
    }
}