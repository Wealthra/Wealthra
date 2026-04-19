using Wealthra.Application.Features.Recommendations.Models;

namespace Wealthra.Application.Common.Interfaces
{
    public interface ISemanticTipRecommendationService
    {
        Task<List<SemanticTipResult>> GetTipsAsync(string userId, RecommendationSignal? topSignal, CancellationToken cancellationToken);
    }
}
