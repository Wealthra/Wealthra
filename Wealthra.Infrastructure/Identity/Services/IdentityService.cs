using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Admin.Models;
using Wealthra.Application.Features.Identity.Models;
using Wealthra.Domain.Entities;
using Wealthra.Domain.Enums;
using Wealthra.Infrastructure.Identity.Models;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Identity.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly TokenGenerator _tokenGenerator;
        private readonly ApplicationDbContext _dbContext;
        private readonly IAdminRealtimeService _adminRealtimeService;
        private readonly IUsageDailyAggregateService _usageDailyAggregateService;

        public IdentityService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            TokenGenerator tokenGenerator,
            ApplicationDbContext dbContext,
            IAdminRealtimeService adminRealtimeService,
            IUsageDailyAggregateService usageDailyAggregateService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenGenerator = tokenGenerator;
            _dbContext = dbContext;
            _adminRealtimeService = adminRealtimeService;
            _usageDailyAggregateService = usageDailyAggregateService;
        }

        public async Task<string?> GetUserNameAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user?.UserName;
        }

        public async Task<(Result Result, string UserId)> CreateUserAsync(string email, string password, string firstName, string lastName)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _adminRealtimeService.PublishActivityAsync(
                    "user.registered",
                    $"User {email} registered.");
            }

            return (result.ToApplicationResult(), user.Id);
        }

        public async Task<(Result Result, AuthResponse? Response)> LoginAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return (Result.Failure(new[] { "Invalid credentials" }), null);

            var result = await _signInManager.CheckPasswordSignInAsync(user, password, false);
            if (!result.Succeeded)
                return (Result.Failure(new[] { "Invalid credentials" }), null);

            user.LastLoginDate = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            await _usageDailyAggregateService.MarkActiveAsync(user.Id);

            return await GenerateAuthResponseAsync(user);
        }

        public async Task<(Result Result, AuthResponse? Response)> RefreshTokenAsync(string token, string refreshToken)
        {
            var principal = _tokenGenerator.GetPrincipalFromExpiredToken(token);
            if (principal == null)
                return (Result.Failure(new[] { "Invalid access token" }), null);

            var email = principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value
                        ?? principal.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                        ?? principal.Identity?.Name;

            if (string.IsNullOrEmpty(email))
                return (Result.Failure(new[] { "Invalid token claims" }), null);

            var user = await _userManager.Users
                .Include(u => u.RefreshTokens)
                .SingleOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return (Result.Failure(new[] { "User not found" }), null);

            var storedToken = user.RefreshTokens.SingleOrDefault(x => x.Token == refreshToken);

            if (storedToken == null)
                return (Result.Failure(new[] { "Refresh token not found in database" }), null);

            if (!storedToken.IsActive)
                return (Result.Failure(new[] { "Refresh token is expired or revoked" }), null);

            storedToken.Revoked = DateTime.UtcNow;

            return await GenerateAuthResponseAsync(user);
        }

        private async Task<(Result Result, AuthResponse Response)> GenerateAuthResponseAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _tokenGenerator.GenerateToken(user, roles);
            var refreshToken = _tokenGenerator.GenerateRefreshToken();

            refreshToken.UserId = user.Id;

            user.RefreshTokens.Add(refreshToken);

            await _userManager.UpdateAsync(user);

            return (Result.Success(), new AuthResponse(
                user.Id,
                user.Email ?? string.Empty,
                accessToken,
                refreshToken.Token,
                refreshToken.Expires));
        }

        public async Task<Result> DeleteUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user != null ? await DeleteUserAsync(user) : Result.Success();
        }

        public async Task<Result> DeleteUserAsync(ApplicationUser user)
        {
            var result = await _userManager.DeleteAsync(user);
            return result.ToApplicationResult();
        }

        public async Task<UserDto?> GetUserDetailsAsync(string userId)
        {
            var user = await _userManager.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == userId);

            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            var isStaff = roles.Any(r => r is "SuperAdmin" or "Admin" or "Finance" or "Support");

            return new UserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                user.AvatarUrl,
                user.CreatedAt,
                user.PreferredCurrency,
                isStaff
            );
        }

        public async Task<Result> UpdateUserAsync(string userId, string firstName, string lastName, string? avatarUrl)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Result.Failure(new[] { "User not found" });

            user.FirstName = firstName;
            user.LastName = lastName;
            user.AvatarUrl = avatarUrl;

            var result = await _userManager.UpdateAsync(user);
            return result.ToApplicationResult();
        }

        public async Task<Result> ChangePreferredCurrencyAsync(string userId, string currency)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Result.Failure(new[] { "User not found" });

            if (currency != "TRY" && currency != "USD" && currency != "EUR")
                return Result.Failure(new[] { "Unsupported currency." });

            user.PreferredCurrency = currency;
            var result = await _userManager.UpdateAsync(user);
            return result.ToApplicationResult();
        }

        public async Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Result.Failure(new[] { "User not found" });

            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            return result.ToApplicationResult();
        }

        public async Task<bool> UpdateUserTierAsync(string email, Wealthra.Domain.Enums.SubscriptionTier newTier)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return false;

            user.SubscriptionTier = newTier;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _adminRealtimeService.PublishActivityAsync(
                    "user.tier.updated",
                    $"Tier updated for {email}.",
                    new { email, tier = newTier.ToString() });
            }

            return result.Succeeded;
        }

        public async Task<bool> AssignUserPlanAsync(string email, int planId)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return false;

            var planExists = await _dbContext.SubscriptionPlans.AnyAsync(x => x.Id == planId && x.IsActive);
            if (!planExists) return false;

            user.SubscriptionPlanId = planId;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _adminRealtimeService.PublishActivityAsync(
                    "user.plan.assigned",
                    $"Plan {planId} assigned to {email}.",
                    new { email, planId });
            }

            return result.Succeeded;
        }

        public async Task<UserUsageDto?> GetUserUsageAsync(string userId)
        {
            var user = await _userManager.Users
                .AsNoTracking()
                .Include(u => u.SubscriptionPlan)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return null;

            return new UserUsageDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FirstName,
                user.LastName,
                user.SubscriptionTier,
                user.SubscriptionPlanId,
                user.SubscriptionPlan?.Name,
                user.OcrRequestsThisMonth,
                user.SttRequestsThisMonth,
                user.LastUsageActivityDate
            );
        }

        public async Task<System.Collections.Generic.List<UserUsageDto>> SearchUserUsagesAsync(string? email, string? name)
        {
            var query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(email))
            {
                query = query.Where(u => u.Email != null && u.Email.Contains(email));
            }
            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(u => (u.FirstName != null && u.FirstName.Contains(name)) || (u.LastName != null && u.LastName.Contains(name)));
            }

            var limitRows = await query
                .Include(u => u.SubscriptionPlan)
                .Take(50)
                .ToListAsync();

            return limitRows.Select(user => new UserUsageDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FirstName,
                user.LastName,
                user.SubscriptionTier,
                user.SubscriptionPlanId,
                user.SubscriptionPlan?.Name,
                user.OcrRequestsThisMonth,
                user.SttRequestsThisMonth,
                user.LastUsageActivityDate
            )).ToList();
        }

        public async Task<System.Collections.Generic.List<UserUsageDto>> GetUsersByPlanAsync(int planId)
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .Include(u => u.SubscriptionPlan)
                .Where(u => u.SubscriptionPlanId == planId)
                .Take(100)
                .ToListAsync();

            return users.Select(user => new UserUsageDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FirstName,
                user.LastName,
                user.SubscriptionTier,
                user.SubscriptionPlanId,
                user.SubscriptionPlan?.Name,
                user.OcrRequestsThisMonth,
                user.SttRequestsThisMonth,
                user.LastUsageActivityDate
            )).ToList();
        }

        public async Task<AppUsageSummaryDto> GetAppUsageSummaryAsync()
        {
            var plans = await _dbContext.SubscriptionPlans
                .AsNoTracking()
                .ToListAsync();

            var users = await _userManager.Users
                .AsNoTracking()
                .Include(u => u.SubscriptionPlan)
                .ToListAsync();

            var grouped = users
                .GroupBy(u => new { u.SubscriptionPlanId, PlanName = u.SubscriptionPlan?.Name ?? "Legacy/Unassigned" })
                .Select(g => new PlanUsageBreakdownDto(
                    g.Key.SubscriptionPlanId,
                    g.Key.PlanName,
                    g.Count(),
                    g.Sum(x => x.OcrRequestsThisMonth),
                    g.Sum(x => x.SttRequestsThisMonth)))
                .OrderByDescending(x => x.UserCount)
                .ToList();

            return new AppUsageSummaryDto(
                users.Count,
                plans.Count(x => x.IsActive),
                users.Sum(x => x.OcrRequestsThisMonth),
                users.Sum(x => x.SttRequestsThisMonth),
                grouped);
        }

        public async Task<PaginatedList<AdminUserListItemDto>> GetAdminUsersPageAsync(int pageNumber, int pageSize, string? search, CancellationToken cancellationToken = default)
        {
            var query = _userManager.Users.AsNoTracking().Include(u => u.SubscriptionPlan);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(u =>
                    (u.Email != null && u.Email.Contains(s)) ||
                    u.FirstName.Contains(s) ||
                    u.LastName.Contains(s));
            }

            var ordered = query.OrderBy(u => u.Email);
            var projected = ordered.Select(u => new AdminUserListItemDto(
                u.Id,
                u.Email ?? string.Empty,
                u.FirstName,
                u.LastName,
                u.SubscriptionTier,
                u.SubscriptionPlanId,
                u.SubscriptionPlan != null ? u.SubscriptionPlan.Name : null,
                u.LastLoginDate,
                u.LockoutEnabled,
                u.LockoutEnd,
                u.EmailConfirmed,
                u.AccessFailedCount,
                Array.Empty<string>()));

            var page = await PaginatedList<AdminUserListItemDto>.CreateAsync(projected, pageNumber, pageSize, cancellationToken);
            var ids = page.Items.Select(x => x.Id).ToList();
            if (ids.Count == 0)
            {
                return page;
            }

            var roleRows = await (
                from ur in _dbContext.Set<IdentityUserRole<string>>().AsNoTracking()
                join r in _dbContext.Set<IdentityRole>().AsNoTracking() on ur.RoleId equals r.Id
                where ids.Contains(ur.UserId)
                select new { ur.UserId, RoleName = r.Name! }).ToListAsync(cancellationToken);

            var rolesByUser = roleRows
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.RoleName).Distinct().ToList());

            var enriched = page.Items
                .Select(u => u with { Roles = rolesByUser.GetValueOrDefault(u.Id, Array.Empty<string>()) })
                .ToList();

            return new PaginatedList<AdminUserListItemDto>(enriched, page.TotalCount, page.PageNumber, pageSize);
        }

        public async Task<AdminUserDetailDto?> GetAdminUserDetailAsync(string userId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.Users
                .AsNoTracking()
                .Include(u => u.SubscriptionPlan)
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null) return null;

            var userTracked = await _userManager.FindByIdAsync(userId);
            if (userTracked == null) return null;

            var roles = await _userManager.GetRolesAsync(userTracked);

            return new AdminUserDetailDto(
                user.Id,
                user.Email ?? string.Empty,
                user.FirstName,
                user.LastName,
                user.AvatarUrl,
                user.CreatedAt,
                user.PreferredCurrency,
                user.SubscriptionTier,
                user.SubscriptionPlanId,
                user.SubscriptionPlan?.Name,
                user.LastLoginDate,
                user.LockoutEnabled,
                user.LockoutEnd,
                user.EmailConfirmed,
                user.AccessFailedCount,
                roles.OrderBy(r => r).ToList());
        }

        public async Task<Result> SetUserLockoutAsync(string actorUserId, string targetUserId, bool lockout, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken = default)
        {
            var target = await _userManager.FindByIdAsync(targetUserId);
            if (target == null) return Result.Failure(new[] { "User not found" });

            await _userManager.SetLockoutEnabledAsync(target, lockout);
            await _userManager.SetLockoutEndDateAsync(target, lockout ? lockoutEnd : null);
            return Result.Success();
        }

        public async Task<Result> SetUserRolesAsync(string actorUserId, string targetUserId, IReadOnlyList<string> roles, CancellationToken cancellationToken = default)
        {
            var actor = await _userManager.FindByIdAsync(actorUserId);
            var target = await _userManager.FindByIdAsync(targetUserId);
            if (actor == null || target == null) return Result.Failure(new[] { "User not found" });

            var actorIsSuper = await _userManager.IsInRoleAsync(actor, Roles.SuperAdmin.ToString());
            var normalized = roles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().ToList();

            foreach (var r in normalized)
            {
                if ((r == Roles.Admin.ToString() || r == Roles.SuperAdmin.ToString()) && !actorIsSuper)
                {
                    return Result.Failure(new[] { "Only SuperAdmin can assign Admin or SuperAdmin roles." });
                }
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Roles.Basic.ToString(),
                Roles.Support.ToString(),
                Roles.Finance.ToString(),
                Roles.Admin.ToString(),
                Roles.SuperAdmin.ToString()
            };

            if (normalized.Any(r => !allowed.Contains(r)))
            {
                return Result.Failure(new[] { "One or more role names are not allowed." });
            }

            var current = await _userManager.GetRolesAsync(target);
            var remove = current.Except(normalized).ToList();
            var add = normalized.Except(current).ToList();

            if (remove.Count > 0)
            {
                var rem = await _userManager.RemoveFromRolesAsync(target, remove);
                if (!rem.Succeeded) return rem.ToApplicationResult();
            }

            if (add.Count > 0)
            {
                var ad = await _userManager.AddToRolesAsync(target, add);
                if (!ad.Succeeded) return ad.ToApplicationResult();
            }

            return Result.Success();
        }

        public async Task<Result> RevokeAllRefreshTokensAsync(string targetUserId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.Users.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
            if (user == null) return Result.Failure(new[] { "User not found" });

            var now = DateTime.UtcNow;
            foreach (var t in user.RefreshTokens.Where(x => x.Revoked == null && !x.IsExpired))
            {
                t.Revoked = now;
            }

            await _userManager.UpdateAsync(user);
            return Result.Success();
        }

        public async Task<Result> AdminSetPasswordAsync(string targetUserId, string newPassword, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByIdAsync(targetUserId);
            if (user == null) return Result.Failure(new[] { "User not found" });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            return result.ToApplicationResult();
        }

        public async Task<RevenueAnalyticsDto> GetRevenueAnalyticsAsync(CancellationToken cancellationToken = default)
        {
            const string mrrCurrency = "TRY";
            var prices = await (
                from u in _userManager.Users.AsNoTracking()
                join p in _dbContext.SubscriptionPlans.AsNoTracking() on u.SubscriptionPlanId equals p.Id
                where p.MonthlyPrice.HasValue && p.PriceCurrency == mrrCurrency
                select p.MonthlyPrice!.Value).ToListAsync(cancellationToken);

            var mrr = prices.Sum();
            var paying = prices.Count;
            var arpu = paying == 0 ? 0 : mrr / paying;
            return new RevenueAnalyticsDto(mrr, mrrCurrency, paying, arpu);
        }

        public async Task<GrowthAnalyticsDto> GetGrowthAnalyticsAsync(CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var yesterday = today.AddDays(-1);
            var start30 = today.AddDays(-30);

            var dau = await _dbContext.UsageDailyAggregates.AsNoTracking()
                .Where(x => x.DateUtc == yesterday && x.WasActive)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            var mau = await _dbContext.UsageDailyAggregates.AsNoTracking()
                .Where(x => x.DateUtc >= start30 && x.DateUtc <= today && x.WasActive)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            var totalUsers = await _userManager.Users.AsNoTracking().CountAsync(cancellationToken);
            var churnCutoff = DateTime.UtcNow.AddDays(-30);
            var inactive = await _userManager.Users.AsNoTracking()
                .CountAsync(u => u.LastLoginDate == null || u.LastLoginDate < churnCutoff, cancellationToken);
            var churnRatio = totalUsers == 0 ? 0 : (double)inactive / totalUsers;

            var slice = await _dbContext.UsageDailyAggregates.AsNoTracking()
                .Where(x => x.DateUtc >= start30 && x.DateUtc <= today)
                .Select(x => new { x.DateUtc, x.UserId, x.WasActive, x.OcrCalls, x.SttCalls, x.CopilotMessages })
                .ToListAsync(cancellationToken);

            var series = slice
                .Where(x => x.WasActive)
                .GroupBy(x => x.DateUtc)
                .Select(g => new DailyActivePointDto(g.Key, g.Select(x => x.UserId).Distinct().Count()))
                .OrderBy(x => x.Date)
                .ToList();

            var totals = new FeatureUsageTotalsDto(
                slice.Sum(x => (long)x.OcrCalls),
                slice.Sum(x => (long)x.SttCalls),
                slice.Sum(x => (long)x.CopilotMessages));

            return new GrowthAnalyticsDto(dau, mau, churnRatio, series, totals);
        }

        public async Task<(bool Success, string UserId, string Token)> GeneratePasswordResetTokenAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return (false, string.Empty, string.Empty);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            return (true, user.Id, token);
        }

        public async Task<Result> ResetPasswordWithTokenAsync(string userId, string token, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Result.Failure(new[] { "User not found" });

            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            return result.ToApplicationResult();
        }
    }

    public static class IdentityResultExtensions
    {
        public static Result ToApplicationResult(this IdentityResult result)
        {
            return result.Succeeded
                ? Result.Success()
                : Result.Failure(result.Errors.Select(e => e.Description));
        }
    }
}