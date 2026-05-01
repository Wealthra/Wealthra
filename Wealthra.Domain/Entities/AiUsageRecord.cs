namespace Wealthra.Domain.Entities;

public class AiUsageRecord
{
    public long Id { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public string Feature { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public string? UserId { get; set; }
}
