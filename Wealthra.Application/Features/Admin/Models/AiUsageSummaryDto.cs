namespace Wealthra.Application.Features.Admin.Models;

public record AiUsageSummaryDto(
    long TotalPromptTokens,
    long TotalCompletionTokens,
    decimal? TotalEstimatedCostUsd,
    int RequestCount);
