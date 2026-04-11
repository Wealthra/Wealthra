using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Infrastructure.Services
{
    public class FrankfurterExchangeService : ICurrencyExchangeService
    {
        private readonly HttpClient _httpClient;
        private readonly ICacheService _cacheService;

        public FrankfurterExchangeService(HttpClient httpClient, ICacheService cacheService)
        {
            _httpClient = httpClient;
            _cacheService = cacheService;
            // Best practice to set a base address if we configure HttpClient, but we can do it here for simplicity
            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri("https://api.frankfurter.app/");
            }
        }

        public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
        {
            if (amount == 0) return 0;
            if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
                return amount;

            fromCurrency = fromCurrency.ToUpperInvariant();
            toCurrency = toCurrency.ToUpperInvariant();

            // Cache key involves both currencies
            var cacheKey = $"ExchangeRate_{fromCurrency}_{toCurrency}";

            // Check Cache
            var cachedRateStr = await _cacheService.GetAsync<string>(cacheKey, cancellationToken);
            decimal rate = 0;

            if (cachedRateStr != null && decimal.TryParse(cachedRateStr, out var cachedRate))
            {
                rate = cachedRate;
            }
            else
            {
                try
                {
                    // Call API: GET https://api.frankfurter.app/latest?amount=1&from=USD&to=EUR
                    var response = await _httpClient.GetAsync($"latest?amount=1&from={fromCurrency}&to={toCurrency}", cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var document = JsonDocument.Parse(content);
                    
                    if (document.RootElement.TryGetProperty("rates", out var ratesElement) && 
                        ratesElement.TryGetProperty(toCurrency, out var targetRateElement))
                    {
                        rate = targetRateElement.GetDecimal();
                    }
                    else
                    {
                        throw new Exception($"Could not extract rate for {toCurrency} from Frankfurter API response.");
                    }

                    // Cache for 1 day
                    await _cacheService.SetAsync(cacheKey, rate.ToString(), TimeSpan.FromDays(1), cancellationToken);
                }
                catch (Exception ex)
                {
                    // Fallback to 1 if external API fails (or log error)
                    System.Console.WriteLine($"Error fetching exchange rates: {ex.Message}");
                    return amount; // Return original amount as fallback, or you could throw.
                }
            }

            return amount * rate;
        }
    }
}
