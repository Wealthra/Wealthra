namespace Wealthra.Application.Features.Expenses.Models
{
    public class ExtractedExpenseDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime? Date { get; set; }
        public string? CategoryHint { get; set; }
        public int? SuggestedCategoryId { get; set; }
        public string? CategorySuggestion { get; set; }
        public decimal? Confidence { get; set; }
        public string Source { get; set; } = string.Empty;
    }
}

