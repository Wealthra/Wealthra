using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.Admin.Models;

public record AdminUserListItemDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    SubscriptionTier SubscriptionTier,
    int? SubscriptionPlanId,
    string? PlanName,
    DateTime? LastLoginDate,
    bool LockoutEnabled,
    DateTimeOffset? LockoutEnd,
    bool EmailConfirmed,
    int AccessFailedCount,
    IReadOnlyList<string> Roles);
