using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Infrastructure.Services
{
    public sealed class NullExpenseExtractionEnrichmentService : IExpenseExtractionEnrichmentService
    {
        public Task<IReadOnlyList<ExtractedExpenseDto>> EnrichAsync(
            IReadOnlyList<ExtractedExpenseDto> extracted,
            IReadOnlyList<ExpenseCategoryOption> applicationCategories,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(extracted);
    }
}
