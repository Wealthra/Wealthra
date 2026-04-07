using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Application.Common.Interfaces
{
    public interface IExpenseExtractionEnrichmentService
    {
        Task<IReadOnlyList<ExtractedExpenseDto>> EnrichAsync(
            IReadOnlyList<ExtractedExpenseDto> extracted,
            IReadOnlyList<ExpenseCategoryOption> applicationCategories,
            CancellationToken cancellationToken = default);
    }
}

