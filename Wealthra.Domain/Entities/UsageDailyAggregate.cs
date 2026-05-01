namespace Wealthra.Domain.Entities;

/// <summary>Daily per-user usage for DAU/MAU and feature mix analytics.</summary>
public class UsageDailyAggregate
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateOnly DateUtc { get; set; }
    public int OcrCalls { get; set; }
    public int SttCalls { get; set; }
    public int CopilotMessages { get; set; }
    public bool WasActive { get; set; }
}
