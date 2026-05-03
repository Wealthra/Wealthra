using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Common.Interfaces
{
    public interface ISemanticTipRecommendationService
    {
        Task<List<SemanticTipResult>> GetTipsAsync(
            string userId,
            IReadOnlyList<RecommendationSignal> signals,
            IReadOnlyList<MonthlyCategoryMetric> metrics,
            string language,
            CancellationToken cancellationToken);
    }
}
