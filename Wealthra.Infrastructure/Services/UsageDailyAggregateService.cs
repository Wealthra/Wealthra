using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Services;

public class UsageDailyAggregateService : IUsageDailyAggregateService
{
    private readonly ApplicationDbContext _db;

    public UsageDailyAggregateService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task IncrementOcrAsync(string userId, CancellationToken cancellationToken = default)
        => UpsertIncrementAsync(userId, ocrDelta: 1, sttDelta: 0, copilotDelta: 0, cancellationToken);

    public Task IncrementSttAsync(string userId, CancellationToken cancellationToken = default)
        => UpsertIncrementAsync(userId, ocrDelta: 0, sttDelta: 1, copilotDelta: 0, cancellationToken);

    public Task IncrementCopilotAsync(string userId, CancellationToken cancellationToken = default)
        => UpsertIncrementAsync(userId, ocrDelta: 0, sttDelta: 0, copilotDelta: 1, cancellationToken);

    public Task MarkActiveAsync(string userId, CancellationToken cancellationToken = default)
        => UpsertIncrementAsync(userId, ocrDelta: 0, sttDelta: 0, copilotDelta: 0, cancellationToken);

    private async Task UpsertIncrementAsync(
        string userId,
        int ocrDelta,
        int sttDelta,
        int copilotDelta,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "UsageDailyAggregates" ("UserId", "DateUtc", "OcrCalls", "SttCalls", "CopilotMessages", "WasActive")
             VALUES ({userId}, {today}, {ocrDelta}, {sttDelta}, {copilotDelta}, TRUE)
             ON CONFLICT ("UserId", "DateUtc") DO UPDATE SET
               "OcrCalls" = "UsageDailyAggregates"."OcrCalls" + {ocrDelta},
               "SttCalls" = "UsageDailyAggregates"."SttCalls" + {sttDelta},
               "CopilotMessages" = "UsageDailyAggregates"."CopilotMessages" + {copilotDelta},
               "WasActive" = TRUE;
             """,
            cancellationToken);
    }
}
