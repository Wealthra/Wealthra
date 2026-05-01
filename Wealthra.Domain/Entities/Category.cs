using Wealthra.Domain.Common;
using System;
using System.Collections.Generic;

namespace Wealthra.Domain.Entities
{
    public class Category : AuditableEntity
    {
        public string NameEn { get; private set; }
        public string NameTr { get; private set; }
        public string? IconKey { get; private set; }
        public int SortOrder { get; private set; }
        public bool IsActive { get; private set; } = true;

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
            IconKey = null;
            SortOrder = 0;
            IsActive = true;
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

        public void UpdateDisplay(string? iconKey, int sortOrder, bool isActive)
        {
            IconKey = iconKey;
            SortOrder = sortOrder;
            IsActive = isActive;
        }
    }
}
