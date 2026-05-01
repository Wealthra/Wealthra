namespace Wealthra.Domain.Entities;

/// <summary>Key/value app settings (e.g. AI model ids, FX provider order JSON).</summary>
public class AppConfigurationEntry
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset UpdatedUtc { get; set; }
}
