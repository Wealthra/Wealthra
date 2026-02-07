using System;
using Wealthra.Domain.Common;
using Wealthra.Domain.Entities;

namespace Wealthra.Domain.Events
{
    public class BudgetLimitExceededEvent : IDomainEvent
    {
        public BudgetLimitExceededEvent(Budget budget, decimal expenseAmount)
        {
            Budget = budget;
            AttemptedExpenseAmount = expenseAmount;
            OccurredOn = DateTime.UtcNow;
        }

        public Budget Budget { get; }
        public decimal AttemptedExpenseAmount { get; }
        public DateTime OccurredOn { get; }
    }
}