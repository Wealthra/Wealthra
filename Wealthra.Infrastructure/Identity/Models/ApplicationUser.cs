using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace Wealthra.Infrastructure.Identity.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }

        // Changed: Store URL to CDN/S3 instead of byte[]
        public string? AvatarUrl { get; set; }

        // Tracking
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginDate { get; set; }

        // Refresh Token logic will be handled here or in a separate table depending on complexity. 
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    }
}