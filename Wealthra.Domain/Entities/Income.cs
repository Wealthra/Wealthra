using System;
using Wealthra.Domain.Common;

namespace Wealthra.Domain.Entities
{
    public class Income : AuditableEntity
    {
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; } 
        public bool IsRecurring { get; set; }
        public DateTime TransactionDate { get; set; }
    }
}