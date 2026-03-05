namespace Wealthra.Application.Features.Goals.Models;

public record GoalHistoryDto(
    int Id,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal ProgressPercentage,
    decimal AchievedAmount,
    decimal NotAchievedAmount,
    DateTime Deadline,
    bool IsCompleted);
