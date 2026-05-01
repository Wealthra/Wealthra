using MediatR;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.GetAiUsageSummary;

public record GetAiUsageSummaryQuery(int Days = 7) : IRequest<AiUsageSummaryDto>;

public class GetAiUsageSummaryQueryHandler : IRequestHandler<GetAiUsageSummaryQuery, AiUsageSummaryDto>
{
    private readonly IApplicationDbContext _db;

    public GetAiUsageSummaryQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AiUsageSummaryDto> Handle(GetAiUsageSummaryQuery request, CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(request.Days, 1, 365));
        var rows = await _db.AiUsageRecords.AsNoTracking()
            .Where(x => x.TimestampUtc >= since)
            .ToListAsync(cancellationToken);

        var totalCost = rows.Any(x => x.EstimatedCostUsd.HasValue)
            ? rows.Sum(x => x.EstimatedCostUsd ?? 0)
            : (decimal?)null;

        return new AiUsageSummaryDto(
            rows.Sum(x => (long)x.PromptTokens),
            rows.Sum(x => (long)x.CompletionTokens),
            totalCost,
            rows.Count);
    }
}
