using Wealthra.Domain.Entities;
using Wealthra.Domain.Enums;

namespace Wealthra.Domain;

/// <summary>
/// Monthly OCR/STT caps: assigned subscription plan when active, otherwise tier defaults (must stay in sync with usage enforcement).
/// </summary>
public static class SubscriptionUsageLimits
{
    public static (int MonthlyOcrLimit, int MonthlySttLimit) ResolveForUser(
        SubscriptionTier tier,
        SubscriptionPlan? activeAssignedPlan)
    {
        if (activeAssignedPlan != null)
            return (activeAssignedPlan.MonthlyOcrLimit, activeAssignedPlan.MonthlySttLimit);

        return tier switch
        {
            SubscriptionTier.Free => (0, 0),
            SubscriptionTier.Basic => (40, 30),
            SubscriptionTier.Limitless => (int.MaxValue, int.MaxValue),
            _ => (0, 0)
        };
    }
}
