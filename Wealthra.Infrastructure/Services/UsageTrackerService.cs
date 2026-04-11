using Microsoft.AspNetCore.Identity;
using System;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Enums;
using Wealthra.Infrastructure.Identity.Models;

namespace Wealthra.Infrastructure.Services
{
    public class UsageTrackerService : IUsageTrackerService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICurrentUserService _currentUserService;

        public UsageTrackerService(
            UserManager<ApplicationUser> userManager,
            ICurrentUserService currentUserService)
        {
            _userManager = userManager;
            _currentUserService = currentUserService;
        }

        private async Task<ApplicationUser?> GetCurrentUserAndResetUsageIfNeededAsync(CancellationToken cancellationToken)
        {
            var userId = _currentUserService.UserId;
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            bool isNewMonth = !user.LastUsageActivityDate.HasValue ||
                              user.LastUsageActivityDate.Value.Year != now.Year ||
                              user.LastUsageActivityDate.Value.Month != now.Month;

            if (isNewMonth)
            {
                user.OcrRequestsThisMonth = 0;
                user.SttRequestsThisMonth = 0;
                user.LastUsageActivityDate = now;
                await _userManager.UpdateAsync(user);
            }

            return user;
        }

        public async Task<bool> CanUseOcrAsync(CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAndResetUsageIfNeededAsync(cancellationToken);
            if (user == null) return false;

            return user.SubscriptionTier switch
            {
                SubscriptionTier.Free => false,
                SubscriptionTier.Basic => user.OcrRequestsThisMonth < 40,
                SubscriptionTier.Limitless => true,
                _ => false
            };
        }

        public async Task<bool> CanUseSttAsync(CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAndResetUsageIfNeededAsync(cancellationToken);
            if (user == null) return false;

            return user.SubscriptionTier switch
            {
                SubscriptionTier.Free => false,
                SubscriptionTier.Basic => user.SttRequestsThisMonth < 30,
                SubscriptionTier.Limitless => true,
                _ => false
            };
        }

        public async Task IncrementOcrAsync(CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAndResetUsageIfNeededAsync(cancellationToken);
            if (user != null)
            {
                user.OcrRequestsThisMonth++;
                user.LastUsageActivityDate = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }
        }

        public async Task IncrementSttAsync(CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAndResetUsageIfNeededAsync(cancellationToken);
            if (user != null)
            {
                user.SttRequestsThisMonth++;
                user.LastUsageActivityDate = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }
        }
    }
}
