using System;
using Wealthra.Domain.Common;
using Wealthra.Domain.Events;
using Wealthra.Domain.Exceptions;

namespace Wealthra.Domain.Entities
{
    public class Budget : AuditableEntity
    {
        public decimal LimitAmount { get; private set; }
        public decimal CurrentAmount { get; private set; }
        public string Currency { get; private set; }

        // Foreign Keys
        public int CategoryId { get; private set; }
        public virtual Category Category { get; private set; }

        public Budget(int categoryId, decimal limitAmount, string currency = "TRY")
        {
            CategoryId = categoryId;
            LimitAmount = limitAmount;
            CurrentAmount = 0;
            Currency = currency;
        }

        public void UpdateLimit(decimal newLimit)
        {
            if (newLimit <= 0)
                throw new UnsupportedBudgetOperationException("Limit must be greater than zero.");

            LimitAmount = newLimit;
        }

        public void AddExpense(decimal amount)
        {
            if (amount < 0)
                throw new UnsupportedBudgetOperationException("Cannot add negative expense to budget.");

            CurrentAmount += amount;

            // Domain Logic: Check for threshold breach
            if (CurrentAmount > LimitAmount)
            {
                // Queue event to notify user later
                AddDomainEvent(new BudgetLimitExceededEvent(this, amount));
            }
        }

        public void RemoveExpense(decimal amount)
        {
            CurrentAmount -= amount;
        }

        public void ResetPeriod()
        {
            CurrentAmount = 0;
        }

        // Helper for UI/Logic
        public decimal GetPercentageUsed()
        {
            if (LimitAmount == 0) return 0;
            return (CurrentAmount / LimitAmount) * 100;
        }
    }
}