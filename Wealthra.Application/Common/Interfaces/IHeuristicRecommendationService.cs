using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Domain.Entities;

namespace Wealthra.Application.Common.Interfaces
{
    public interface IHeuristicRecommendationService
    {
        List<RecommendationSignal> Evaluate(IReadOnlyCollection<MonthlyCategoryMetric> metrics);
    }
}
