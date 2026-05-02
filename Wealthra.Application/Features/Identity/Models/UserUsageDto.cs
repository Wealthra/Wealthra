using System;
using Wealthra.Domain.Enums;

namespace Wealthra.Application.Features.Identity.Models
{
    public record UserUsageDto(
        string Id,
        string Email,
        string FirstName,
        string LastName,
        SubscriptionTier SubscriptionTier,
        int? SubscriptionPlanId,
        string? SubscriptionPlanName,
        int OcrRequestsThisMonth,
        int SttRequestsThisMonth,
        /// <summary>Monthly OCR cap; null means unlimited (limitless tier).</summary>
        int? MonthlyOcrLimit,
        /// <summary>Monthly STT cap; null means unlimited (limitless tier).</summary>
        int? MonthlySttLimit,
        DateTime? LastUsageActivityDate
    );
}
