namespace Wealthra.Application.Features.Goals.Models;

public record GoalDto(
    int Id,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal ProgressPercentage,
    DateTime Deadline,
    bool IsCompleted);
