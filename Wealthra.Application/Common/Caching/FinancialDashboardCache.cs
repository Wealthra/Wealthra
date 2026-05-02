using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Categories.Models;

namespace Wealthra.Application.Common.Caching;

/// <summary>Cache keys for financial dashboard API responses (mobile + web).</summary>
public static class FinancialDashboardCache
{
    /// <summary>Suffixes for <c>?currency=</c> query variants; must cover supported dashboard currencies.</summary>
    private static readonly string[] CurrencyKeySuffixes = ["TRY", "USD", "EUR"];

    public static string DashboardKey(
        string userId,
        string? targetCurrencyUpper = null,
        CategoryDisplayLanguage categoryLanguage = CategoryDisplayLanguage.English)
    {
        var lang = categoryLanguage == CategoryDisplayLanguage.Turkish ? "tr" : "en";
        if (string.IsNullOrEmpty(targetCurrencyUpper))
        {
            return $"dashboard_{userId}_{lang}";
        }

        return $"dashboard_{userId}_{targetCurrencyUpper}_{lang}";
    }

    public static string DashboardWebKey(
        string userId,
        string? targetCurrencyUpper = null,
        CategoryDisplayLanguage categoryLanguage = CategoryDisplayLanguage.English)
    {
        var lang = categoryLanguage == CategoryDisplayLanguage.Turkish ? "tr" : "en";
        if (string.IsNullOrEmpty(targetCurrencyUpper))
        {
            return $"dashboard_web_{userId}_{lang}";
        }

        return $"dashboard_web_{userId}_{targetCurrencyUpper}_{lang}";
    }

    public static IEnumerable<string> AllKeysForUser(string userId)
    {
        foreach (var lang in new[] { CategoryDisplayLanguage.English, CategoryDisplayLanguage.Turkish })
        {
            yield return DashboardKey(userId, null, lang);
            yield return DashboardWebKey(userId, null, lang);
            foreach (var c in CurrencyKeySuffixes)
            {
                yield return DashboardKey(userId, c, lang);
                yield return DashboardWebKey(userId, c, lang);
            }
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
