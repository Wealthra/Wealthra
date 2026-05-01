namespace Wealthra.Application.Features.Admin.Models;

public record SubscriptionPlanDto(
    int Id,
    string Name,
    string Description,
    int MonthlyOcrLimit,
    int MonthlySttLimit,
    decimal? MonthlyPrice,
    string PriceCurrency,
    bool IsActive,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);
