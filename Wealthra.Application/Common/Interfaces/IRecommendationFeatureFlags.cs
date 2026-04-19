namespace Wealthra.Application.Common.Interfaces
{
    public interface IRecommendationFeatureFlags
    {
        bool EnableCollaborativeFiltering { get; }
        bool EnableSemanticTips { get; }
    }
}
