using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.Recommendations.Queries.GetPersonalizedRecommendations
{
    public class GetPersonalizedRecommendationsQuery : IRequest<PersonalizedRecommendationResponse>
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class GetPersonalizedRecommendationsQueryHandler : IRequestHandler<GetPersonalizedRecommendationsQuery, PersonalizedRecommendationResponse>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHeuristicRecommendationService _heuristicRecommendationService;
        private readonly ICollaborativeRecommendationService _collaborativeRecommendationService;
        private readonly ISemanticTipRecommendationService _semanticTipRecommendationService;
        private readonly IRecommendationFeatureFlags _recommendationFeatureFlags;
        private readonly IIdentityService _identityService;

        public GetPersonalizedRecommendationsQueryHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            IHeuristicRecommendationService heuristicRecommendationService,
            ICollaborativeRecommendationService collaborativeRecommendationService,
            ISemanticTipRecommendationService semanticTipRecommendationService,
            IRecommendationFeatureFlags recommendationFeatureFlags,
            IIdentityService identityService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _heuristicRecommendationService = heuristicRecommendationService;
            _collaborativeRecommendationService = collaborativeRecommendationService;
            _semanticTipRecommendationService = semanticTipRecommendationService;
            _recommendationFeatureFlags = recommendationFeatureFlags;
            _identityService = identityService;
        }

        public async Task<PersonalizedRecommendationResponse> Handle(GetPersonalizedRecommendationsQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId!;
            var targetMonth = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var metrics = await _context.MonthlyCategoryMetrics
                .Where(m => m.UserId == userId && m.Month == targetMonth)
                .ToListAsync(cancellationToken);

            var signals = _heuristicRecommendationService.Evaluate(metrics);
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
                response.CollaborativeSuggestions = await _collaborativeRecommendationService.GetSuggestionsAsync(userId, cancellationToken);
            }

            if (canUseLayer3 && _recommendationFeatureFlags.EnableSemanticTips)
            {
                var topSignal = signals
                    .OrderByDescending(s => s.Severity == "high")
                    .ThenBy(s => s.ReasonCode)
                    .FirstOrDefault();
                response.SemanticTips = await _semanticTipRecommendationService.GetTipsAsync(userId, topSignal, cancellationToken);
            }

            return response;
        }
    }
}
