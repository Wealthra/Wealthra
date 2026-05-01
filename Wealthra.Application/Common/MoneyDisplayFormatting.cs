using System.Globalization;

namespace Wealthra.Application.Common;

/// <summary>
/// Consistent display formatting for monetary amounts: two fractional digits, invariant culture (no locale-specific grouping/separators).
/// </summary>
public static class MoneyDisplayFormatting
{
    public static string FormatAmount(decimal amount)
        => Math.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture);
}
