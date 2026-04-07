using Wealthra.Domain.Common;
using System;
using System.Collections.Generic;

namespace Wealthra.Domain.Entities
{
    public class Category : AuditableEntity
    {
        public string NameEn { get; private set; }
        public string NameTr { get; private set; }

        public virtual ICollection<Budget> Budgets { get; private set; } = new List<Budget>();
        public virtual ICollection<Expense> Expenses { get; private set; } = new List<Expense>();

        public Category(string nameEn, string nameTr)
        {
            if (string.IsNullOrWhiteSpace(nameEn))
                throw new ArgumentException("English category name cannot be empty.");
            if (string.IsNullOrWhiteSpace(nameTr))
                throw new ArgumentException("Turkish category name cannot be empty.");

            NameEn = nameEn;
            NameTr = nameTr;
        }

        public void UpdateNames(string nameEn, string nameTr)
        {
            if (string.IsNullOrWhiteSpace(nameEn))
                throw new ArgumentException("English category name cannot be empty.");
            if (string.IsNullOrWhiteSpace(nameTr))
                throw new ArgumentException("Turkish category name cannot be empty.");

            NameEn = nameEn;
            NameTr = nameTr;
        }
    }
}
