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

        public async Task<List<SemanticTipResult>> GetTipsAsync(string userId, RecommendationSignal? topSignal, CancellationToken cancellationToken)
        {
            if (topSignal is null)
            {
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
                        MatchReason = "Varsayılan tavsiye"
                    })
                    .ToListAsync(cancellationToken);
            }

            var queryVector = await _textEmbeddingService.CreateEmbeddingAsync(topSignal.Evidence, cancellationToken);
            var vectorLiteral = $"[{string.Join(",", queryVector.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)))}]";

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
                    """, vectorLiteral, $"'{topSignal.CategoryName}' için semantik yakın tip")
                .ToListAsync(cancellationToken);
        }
    }
}
