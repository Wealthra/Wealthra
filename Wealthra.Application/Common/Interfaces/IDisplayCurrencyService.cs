using System.Threading;
using System.Threading.Tasks;

namespace Wealthra.Application.Common.Interfaces;

/// <summary>
/// Resolves the ISO currency code used for display and aggregation:
/// explicit request wins, then the user's preferred currency, then TRY.
/// </summary>
public interface IDisplayCurrencyService
{
    /// <param name="requestCurrency">Optional override from API (e.g. query <c>currency</c>).</param>
    Task<string> GetEffectiveCurrencyAsync(string? requestCurrency, CancellationToken cancellationToken = default);
}
