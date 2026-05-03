using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Models;
using Wealthra.Application.Features.Recommendations.Services;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Services
{
    public class SemanticTipRecommendationService : ISemanticTipRecommendationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITextEmbeddingService _textEmbeddingService;

        public SemanticTipRecommendationService(ApplicationDbContext context, ITextEmbeddingService textEmbeddingService)
        {
            _context = context;
            _textEmbeddingService = textEmbeddingService;
        }

        public async Task<List<SemanticTipResult>> GetTipsAsync(
            string userId,
            IReadOnlyList<RecommendationSignal> signals,
            IReadOnlyList<MonthlyCategoryMetric> metrics,
            string language,
            CancellationToken cancellationToken)
        {
            _ = userId;

            var normalizedLanguage = language?.Trim().ToLowerInvariant() ?? "en";
            var preferredLocalePrefix = normalizedLanguage == "tr" ? "tr" : "en";

            var situationText = RecommendationSituationTextBuilder.Build(signals, metrics, normalizedLanguage);
            var queryVector = await _textEmbeddingService.CreateEmbeddingAsync(situationText, cancellationToken);
            var vectorLiteral = $"[{string.Join(",", queryVector.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)))}]";

            var primarySignal = signals
                .OrderByDescending(SeverityRank)
                .ThenBy(s => s.ReasonCode, StringComparer.Ordinal)
                .FirstOrDefault();

            var matchReason = primarySignal is null
                ? (normalizedLanguage == "tr"
                    ? "Aylık durumuna göre semantik olarak yakın ipuçları"
                    : "Semantically similar tips for your monthly situation")
                : (normalizedLanguage == "tr"
                    ? $"'{primarySignal.CategoryName}' ve diğer sinyallere yakın ipuçları"
                    : $"Tips aligned with '{primarySignal.CategoryName}' and your signals");

            var localizedResults = await _context.Database
                .SqlQueryRaw<SemanticTipResult>(
                    """
                    SELECT 
                        ft."Id" AS "TipId",
                        ft."Topic",
                        ft."Body",
                        ft."Locale",
                        @p1 AS "MatchReason"
                    FROM "FinancialTips" ft
                    WHERE LOWER(ft."Locale") LIKE @p2
                    ORDER BY ft."Embedding" <=> CAST(@p0 AS vector)
                    LIMIT 3;
                    """, vectorLiteral, matchReason, $"{preferredLocalePrefix}%")
                .ToListAsync(cancellationToken);

            if (localizedResults.Count > 0)
            {
                return localizedResults;
            }

            return await _context.Database
                .SqlQueryRaw<SemanticTipResult>(
                    """
                    SELECT 
                        ft."Id" AS "TipId",
                        ft."Topic",
                        ft."Body",
                        ft."Locale",
                        @p1 AS "MatchReason"
                    FROM "FinancialTips" ft
                    ORDER BY ft."Embedding" <=> CAST(@p0 AS vector)
                    LIMIT 3;
                    """, vectorLiteral, matchReason)
                .ToListAsync(cancellationToken);
        }

        private static int SeverityRank(RecommendationSignal s) =>
            s.Severity?.ToLowerInvariant() switch
            {
                "high" => 3,
                "medium" => 2,
                "info" => 1,
                _ => 0
            };
    }
}
