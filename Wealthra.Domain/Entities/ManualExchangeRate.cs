namespace Wealthra.Domain.Entities;

/// <summary>Admin-defined FX rate: amount in ToCurrency = amount in FromCurrency * Rate.</summary>
public class ManualExchangeRate
{
    public int Id { get; set; }
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTimeOffset UpdatedOn { get; set; }
}
