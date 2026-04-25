using System;
using Wealthra.Domain.Common;

namespace Wealthra.Domain.Entities
{
    public class Income : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TRY";
        public string Method { get; set; } = string.Empty; 
        public bool IsRecurring { get; set; }
        public DateTime TransactionDate { get; set; }
    }
}