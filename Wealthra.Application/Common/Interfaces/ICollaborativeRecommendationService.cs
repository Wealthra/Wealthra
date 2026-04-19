using Wealthra.Application.Features.Recommendations.Models;

namespace Wealthra.Application.Common.Interfaces
{
    public interface ICollaborativeRecommendationService
    {
        Task<List<CollaborativeSuggestion>> GetSuggestionsAsync(string userId, CancellationToken cancellationToken);
    }
}
