using System;

namespace Wealthra.Domain.Entities
{
    public class MonthlyCategoryMetric
    {
        public string UserId { get; set; }
        public DateTime Month { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public decimal TotalSpend { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal SpendPercentageOfIncome { get; set; }
        public decimal PreviousMonthSpend { get; set; }
    }
}
