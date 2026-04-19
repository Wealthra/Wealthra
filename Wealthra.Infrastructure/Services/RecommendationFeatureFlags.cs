using Microsoft.Extensions.Configuration;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Infrastructure.Services
{
    public class RecommendationFeatureFlags : IRecommendationFeatureFlags
    {
        private readonly IConfiguration _configuration;

        public RecommendationFeatureFlags(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool EnableCollaborativeFiltering =>
            _configuration.GetValue<bool>("Recommendations:EnableCollaborativeFiltering");

        public bool EnableSemanticTips =>
            _configuration.GetValue<bool>("Recommendations:EnableSemanticTips");
    }
}
