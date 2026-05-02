using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Common.Caching;

/// <summary>Cache keys for financial dashboard API responses (mobile + web).</summary>
public static class FinancialDashboardCache
{
    /// <summary>Suffixes for <c>?currency=</c> query variants; must cover supported dashboard currencies.</summary>
    private static readonly string[] CurrencyKeySuffixes = ["TRY", "USD", "EUR"];

    public static string DashboardKey(string userId, string? targetCurrencyUpper = null) =>
        string.IsNullOrEmpty(targetCurrencyUpper)
            ? $"dashboard_{userId}"
            : $"dashboard_{userId}_{targetCurrencyUpper}";

    public static string DashboardWebKey(string userId, string? targetCurrencyUpper = null) =>
        string.IsNullOrEmpty(targetCurrencyUpper)
            ? $"dashboard_web_{userId}"
            : $"dashboard_web_{userId}_{targetCurrencyUpper}";

    public static IEnumerable<string> AllKeysForUser(string userId)
    {
        yield return DashboardKey(userId, null);
        yield return DashboardWebKey(userId, null);
        foreach (var c in CurrencyKeySuffixes)
        {
            yield return DashboardKey(userId, c);
            yield return DashboardWebKey(userId, c);
        }
    }

    public static async Task InvalidateForUserAsync(
        ICacheService cache,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        foreach (var key in AllKeysForUser(userId))
        {
            await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }
}
