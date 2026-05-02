using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Identity.Models;

namespace Wealthra.Infrastructure.Services
{
    public class UsageTrackerService : IUsageTrackerService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICurrentUserService _currentUserService;
        private readonly IApplicationDbContext _applicationDbContext;
        private readonly IAdminRealtimeService _adminRealtimeService;
        private readonly IUsageDailyAggregateService _usageDailyAggregateService;

        public UsageTrackerService(
            UserManager<ApplicationUser> userManager,
            ICurrentUserService currentUserService,
            IApplicationDbContext applicationDbContext,
            IAdminRealtimeService adminRealtimeService,
            IUsageDailyAggregateService usageDailyAggregateService)
        {
            _userManager = userManager;
            _currentUserService = currentUserService;
            _applicationDbContext = applicationDbContext;
            _adminRealtimeService = adminRealtimeService;
            _usageDailyAggregateService = usageDailyAggregateService;
        }

        private sealed record PlanLimits(int OcrLimit, int SttLimit);

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

            var limits = await ResolvePlanLimitsAsync(user, cancellationToken);
            return user.OcrRequestsThisMonth < limits.OcrLimit;
        }

        public async Task<bool> CanUseSttAsync(CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAndResetUsageIfNeededAsync(cancellationToken);
            if (user == null) return false;

            var limits = await ResolvePlanLimitsAsync(user, cancellationToken);
            return user.SttRequestsThisMonth < limits.SttLimit;
        }

        public async Task IncrementOcrAsync(CancellationToken cancellationToken)
        {
            var user = await GetCurrentUserAndResetUsageIfNeededAsync(cancellationToken);
            if (user != null)
            {
                user.OcrRequestsThisMonth++;
                user.LastUsageActivityDate = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                await _usageDailyAggregateService.IncrementOcrAsync(user.Id, cancellationToken);
                await _adminRealtimeService.PublishActivityAsync(
                    "usage.ocr.incremented",
                    $"OCR usage incremented for {user.Email}.",
                    new { user.Id, user.Email, user.OcrRequestsThisMonth },
                    cancellationToken);
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
                await _usageDailyAggregateService.IncrementSttAsync(user.Id, cancellationToken);
                await _adminRealtimeService.PublishActivityAsync(
                    "usage.stt.incremented",
                    $"STT usage incremented for {user.Email}.",
                    new { user.Id, user.Email, user.SttRequestsThisMonth },
                    cancellationToken);
            }
        }

        private async Task<PlanLimits> ResolvePlanLimitsAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            SubscriptionPlan? activePlan = null;
            if (user.SubscriptionPlanId.HasValue)
            {
                activePlan = await _applicationDbContext.SubscriptionPlans
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == user.SubscriptionPlanId.Value && x.IsActive, cancellationToken);
            }

            var (ocr, stt) = SubscriptionUsageLimits.ResolveForUser(user.SubscriptionTier, activePlan);
            return new PlanLimits(ocr, stt);
        }
    }
}
