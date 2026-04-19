namespace Wealthra.Application.Features.Recommendations.Models
{
    public class PersonalizedRecommendationResponse
    {
        public List<RecommendationSignal> Signals { get; set; } = new();
        public List<CollaborativeSuggestion> CollaborativeSuggestions { get; set; } = new();
        public List<SemanticTipResult> SemanticTips { get; set; } = new();
    }
}
