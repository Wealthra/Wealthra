using System;
using Wealthra.Domain.Common;

namespace Wealthra.Domain.Entities
{
    public class Goal : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public decimal TargetAmount { get; set; }
        public decimal CurrentAmount { get; set; } 
        public string Currency { get; set; } = "TRY";
        public DateTime Deadline { get; set; }

        public decimal CalculateProgressPercentage()
        {
            if (TargetAmount == 0) return 0;
            return (CurrentAmount / TargetAmount) * 100;
        }
    }
}