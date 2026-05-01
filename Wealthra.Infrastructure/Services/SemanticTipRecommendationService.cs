using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Recommendations.Models;
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

        public async Task<List<SemanticTipResult>> GetTipsAsync(string userId, RecommendationSignal? topSignal, string language, CancellationToken cancellationToken)
        {
            var normalizedLanguage = language?.Trim().ToLowerInvariant() ?? "en";
            var preferredLocalePrefix = normalizedLanguage == "tr" ? "tr" : "en";
            var defaultMatchReason = normalizedLanguage == "tr" ? "Varsayilan tavsiye" : "Default suggestion";

            if (topSignal is null)
            {
                var localizedTips = await _context.FinancialTips
                    .AsNoTracking()
                    .Where(x => x.Locale.ToLower().StartsWith(preferredLocalePrefix))
                    .OrderBy(x => x.Id)
                    .Take(3)
                    .Select(x => new SemanticTipResult
                    {
                        TipId = x.Id,
                        Topic = x.Topic,
                        Body = x.Body,
                        Locale = x.Locale,
                        MatchReason = defaultMatchReason
                    })
                    .ToListAsync(cancellationToken);

                if (localizedTips.Count > 0)
                {
                    return localizedTips;
                }

                return await _context.FinancialTips
                    .AsNoTracking()
                    .OrderBy(x => x.Id)
                    .Take(3)
                    .Select(x => new SemanticTipResult
                    {
                        TipId = x.Id,
                        Topic = x.Topic,
                        Body = x.Body,
                        Locale = x.Locale,
                        MatchReason = defaultMatchReason
                    })
                    .ToListAsync(cancellationToken);
            }

            var queryVector = await _textEmbeddingService.CreateEmbeddingAsync(topSignal.Evidence, cancellationToken);
            var vectorLiteral = $"[{string.Join(",", queryVector.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)))}]";
            var matchReason = normalizedLanguage == "tr"
                ? $"'{topSignal.CategoryName}' icin semantik yakin tip"
                : $"Semantically similar tip for '{topSignal.CategoryName}'";

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
    }
}
