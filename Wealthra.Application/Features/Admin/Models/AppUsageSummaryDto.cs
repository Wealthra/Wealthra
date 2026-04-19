namespace Wealthra.Application.Features.Admin.Models;

public record PlanUsageBreakdownDto(
    int? PlanId,
    string PlanName,
    int UserCount,
    int TotalOcrRequests,
    int TotalSttRequests);

public record AppUsageSummaryDto(
    int TotalUsers,
    int ActivePlans,
    int TotalOcrRequestsThisMonth,
    int TotalSttRequestsThisMonth,
    IReadOnlyList<PlanUsageBreakdownDto> PlanBreakdown);
