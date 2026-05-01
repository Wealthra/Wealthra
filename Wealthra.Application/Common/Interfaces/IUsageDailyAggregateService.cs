namespace Wealthra.Application.Common.Interfaces;

public interface IUsageDailyAggregateService
{
    Task IncrementOcrAsync(string userId, CancellationToken cancellationToken = default);
    Task IncrementSttAsync(string userId, CancellationToken cancellationToken = default);
    Task IncrementCopilotAsync(string userId, CancellationToken cancellationToken = default);
    Task MarkActiveAsync(string userId, CancellationToken cancellationToken = default);
}
