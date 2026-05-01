using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.Admin.Models;

public record AdminUserDetailDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    DateTime CreatedAt,
    string PreferredCurrency,
    SubscriptionTier SubscriptionTier,
    int? SubscriptionPlanId,
    string? PlanName,
    DateTime? LastLoginDate,
    bool LockoutEnabled,
    DateTimeOffset? LockoutEnd,
    bool EmailConfirmed,
    int AccessFailedCount,
    IReadOnlyList<string> Roles);
