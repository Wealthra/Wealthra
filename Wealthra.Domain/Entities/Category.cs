using Wealthra.Domain.Common;
using System.Collections.Generic;

namespace Wealthra.Domain.Entities
{
    public class Category : AuditableEntity
    {
        public string Name { get; private set; }

        // Navigation Property (One-to-Many)
        public virtual ICollection<Budget> Budgets { get; private set; } = new List<Budget>();
        public virtual ICollection<Expense> Expenses { get; private set; } = new List<Expense>();

        // Constructor enforces required data
        public Category(string name)
        {
            Name = name;
        }

        public void UpdateName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Category name cannot be empty.");

            Name = newName;
        }
    }
}