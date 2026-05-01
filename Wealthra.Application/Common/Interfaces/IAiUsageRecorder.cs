namespace Wealthra.Application.Common.Interfaces;

public interface IAiUsageRecorder
{
    Task RecordAsync(
        string feature,
        string model,
        int promptTokens,
        int completionTokens,
        decimal? estimatedCostUsd,
        string? userId,
        CancellationToken cancellationToken = default);
}
