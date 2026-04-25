using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

using Wealthra.Domain.Entities;
using Wealthra.Domain.Enums;

namespace Wealthra.Infrastructure.Identity.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string PreferredCurrency { get; set; } = "TRY";
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginDate { get; set; }

        public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Basic;
        public int? SubscriptionPlanId { get; set; }
        public SubscriptionPlan? SubscriptionPlan { get; set; }
        public int OcrRequestsThisMonth { get; set; } = 0;
        public int SttRequestsThisMonth { get; set; } = 0;
        public DateTime? LastUsageActivityDate { get; set; }

        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}