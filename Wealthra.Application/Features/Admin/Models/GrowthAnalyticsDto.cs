namespace Wealthra.Application.Features.Admin.Models;

public record DailyActivePointDto(DateOnly Date, int ActiveUsers);
public record FeatureUsageTotalsDto(long TotalOcr, long TotalStt, long TotalCopilot);

public record GrowthAnalyticsDto(
    int DauYesterday,
    int MauLast30Days,
    double ChurnRatioLast30Days,
    IReadOnlyList<DailyActivePointDto> DailyActiveSeries,
    FeatureUsageTotalsDto FeatureTotalsLast30Days);
