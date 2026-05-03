using MediatR;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.Recommendations.Queries.GetPersonalizedRecommendations
{
    public class GetPersonalizedRecommendationsQuery : IRequest<PersonalizedRecommendationResponse>
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Language { get; set; } = "en";
    }

    public class GetPersonalizedRecommendationsQueryHandler : IRequestHandler<GetPersonalizedRecommendationsQuery, PersonalizedRecommendationResponse>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IHeuristicRecommendationService _heuristicRecommendationService;
        private readonly ICollaborativeRecommendationService _collaborativeRecommendationService;
        private readonly ISemanticTipRecommendationService _semanticTipRecommendationService;
        private readonly IRecommendationFeatureFlags _recommendationFeatureFlags;
        private readonly IIdentityService _identityService;
        private readonly IDisplayCurrencyService _displayCurrencyService;
        private readonly IMonthlyCategoryMetricsCalculator _metricsCalculator;

        public GetPersonalizedRecommendationsQueryHandler(
            ICurrentUserService currentUserService,
            IHeuristicRecommendationService heuristicRecommendationService,
            ICollaborativeRecommendationService collaborativeRecommendationService,
            ISemanticTipRecommendationService semanticTipRecommendationService,
            IRecommendationFeatureFlags recommendationFeatureFlags,
            IIdentityService identityService,
            IDisplayCurrencyService displayCurrencyService,
            IMonthlyCategoryMetricsCalculator metricsCalculator)
        {
            _currentUserService = currentUserService;
            _heuristicRecommendationService = heuristicRecommendationService;
            _collaborativeRecommendationService = collaborativeRecommendationService;
            _semanticTipRecommendationService = semanticTipRecommendationService;
            _recommendationFeatureFlags = recommendationFeatureFlags;
            _identityService = identityService;
            _displayCurrencyService = displayCurrencyService;
            _metricsCalculator = metricsCalculator;
        }

        public async Task<PersonalizedRecommendationResponse> Handle(GetPersonalizedRecommendationsQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId!;
            var normalizedLanguage = request.Language?.Trim().ToLowerInvariant() ?? "en";
            var effective = await _displayCurrencyService.GetEffectiveCurrencyAsync(null, cancellationToken);

            var metrics = await _metricsCalculator.ComputeForMonthAsync(
                userId,
                request.Year,
                request.Month,
                effective,
                cancellationToken);

            var signals = _heuristicRecommendationService.Evaluate(metrics, normalizedLanguage);
            var response = new PersonalizedRecommendationResponse
            {
                Signals = signals
            };

            var userUsage = await _identityService.GetUserUsageAsync(userId);
            var tier = userUsage?.SubscriptionTier ?? SubscriptionTier.Free;

            var canUseLayer2 = tier is SubscriptionTier.Basic or SubscriptionTier.Limitless;
            var canUseLayer3 = tier is SubscriptionTier.Limitless;

            if (canUseLayer2 && _recommendationFeatureFlags.EnableCollaborativeFiltering)
            {
                response.CollaborativeSuggestions = await _collaborativeRecommendationService.GetSuggestionsAsync(userId, normalizedLanguage, cancellationToken);
            }

            if (canUseLayer3 && _recommendationFeatureFlags.EnableSemanticTips)
            {
                response.SemanticTips = await _semanticTipRecommendationService.GetTipsAsync(
                    userId,
                    signals,
                    metrics,
                    normalizedLanguage,
                    cancellationToken);
            }

            return response;
        }
    }
}
