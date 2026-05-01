using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Constants;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Services;

public class ChainedCurrencyExchangeService : ICurrencyExchangeService
{
    private readonly ApplicationDbContext _db;
    private readonly FrankfurterExchangeService _frankfurter;
    private readonly IRuntimeAppSettings _runtimeAppSettings;

    public ChainedCurrencyExchangeService(
        ApplicationDbContext db,
        FrankfurterExchangeService frankfurter,
        IRuntimeAppSettings runtimeAppSettings)
    {
        _db = db;
        _frankfurter = frankfurter;
        _runtimeAppSettings = runtimeAppSettings;
    }

    public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        if (amount == 0) return 0;
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return amount;

        var orderJson = await _runtimeAppSettings.GetAsync(AppSettingsKeys.FxProviderOrder, cancellationToken);
        string[] order;
        try
        {
            order = string.IsNullOrWhiteSpace(orderJson)
                ? ["Manual", "Frankfurter"]
                : JsonSerializer.Deserialize<string[]>(orderJson) ?? ["Manual", "Frankfurter"];
        }
        catch (JsonException)
        {
            order = ["Manual", "Frankfurter"];
        }

        foreach (var provider in order)
        {
            if (string.Equals(provider, "Manual", StringComparison.OrdinalIgnoreCase))
            {
                var manual = await TryManualAsync(amount, fromCurrency, toCurrency, cancellationToken);
                if (manual.HasValue)
                    return manual.Value;
            }
            else if (string.Equals(provider, "Frankfurter", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return await _frankfurter.ConvertAsync(amount, fromCurrency, toCurrency, cancellationToken);
                }
                catch
                {
                    // try next provider
                }
            }
        }

        return await _frankfurter.ConvertAsync(amount, fromCurrency, toCurrency, cancellationToken);
    }

    private async Task<decimal?> TryManualAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken)
    {
        var from = fromCurrency.ToUpperInvariant();
        var to = toCurrency.ToUpperInvariant();

        var direct = await _db.ManualExchangeRates.AsNoTracking()
            .FirstOrDefaultAsync(r => r.FromCurrency == from && r.ToCurrency == to, cancellationToken);
        if (direct != null)
            return amount * direct.Rate;

        var inverse = await _db.ManualExchangeRates.AsNoTracking()
            .FirstOrDefaultAsync(r => r.FromCurrency == to && r.ToCurrency == from, cancellationToken);
        if (inverse != null && inverse.Rate != 0)
            return amount / inverse.Rate;

        return null;
    }
}
