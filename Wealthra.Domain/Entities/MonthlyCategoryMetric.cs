using System;

namespace Wealthra.Domain.Entities
{
    public class MonthlyCategoryMetric
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime Month { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryNameTr { get; set; } = string.Empty;
        public decimal TotalSpend { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal SpendPercentageOfIncome { get; set; }
        public decimal PreviousMonthSpend { get; set; }
    }
}
