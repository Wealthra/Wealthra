using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Infrastructure.Services;

public class DisplayCurrencyService : IDisplayCurrencyService
{
    private const string DefaultCurrency = "TRY";

    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;

    public DisplayCurrencyService(ICurrentUserService currentUserService, IIdentityService identityService)
    {
        _currentUserService = currentUserService;
        _identityService = identityService;
    }

    public async Task<string> GetEffectiveCurrencyAsync(string? requestCurrency, CancellationToken cancellationToken = default)
    {
        var trimmed = requestCurrency?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            return trimmed.ToUpperInvariant();
        }

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return DefaultCurrency;
        }

        var user = await _identityService.GetUserDetailsAsync(userId);
        var pref = user?.PreferredCurrency?.Trim();
        return string.IsNullOrEmpty(pref) ? DefaultCurrency : pref.ToUpperInvariant();
    }
}
