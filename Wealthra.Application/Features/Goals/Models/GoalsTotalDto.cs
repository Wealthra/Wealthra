namespace Wealthra.Application.Features.Goals.Models;

public record GoalsTotalDto(
    decimal TotalTargetAmount,
    decimal TotalCurrentAmount,
    decimal OverallProgressPercentage,
    int TotalGoals,
    int AchievedGoals,
    int NotAchievedGoals,
    string Currency);
