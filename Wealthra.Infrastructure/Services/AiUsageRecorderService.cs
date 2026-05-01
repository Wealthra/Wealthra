using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Services;

public class AiUsageRecorderService : IAiUsageRecorder
{
    private readonly ApplicationDbContext _db;

    public AiUsageRecorderService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(
        string feature,
        string model,
        int promptTokens,
        int completionTokens,
        decimal? estimatedCostUsd,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        _db.AiUsageRecords.Add(new AiUsageRecord
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Feature = feature,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            EstimatedCostUsd = estimatedCostUsd,
            UserId = userId
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
