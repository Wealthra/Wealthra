namespace Wealthra.Application.Features.Admin.Models;

public record ManualExchangeRateDto(int Id, string FromCurrency, string ToCurrency, decimal Rate, DateTimeOffset UpdatedOn);
