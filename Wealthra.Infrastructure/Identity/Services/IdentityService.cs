using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Identity.Models;
using Wealthra.Infrastructure.Identity.Models;

namespace Wealthra.Infrastructure.Identity.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly TokenGenerator _tokenGenerator;

        public IdentityService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            TokenGenerator tokenGenerator)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenGenerator = tokenGenerator;
        }

        public async Task<string> GetUserNameAsync(string userId)
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

            return (result.ToApplicationResult(), user.Id);
        }

        public async Task<(Result Result, AuthResponse Response)> LoginAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return (Result.Failure(new[] { "Invalid credentials" }), null);

            var result = await _signInManager.CheckPasswordSignInAsync(user, password, false);
            if (!result.Succeeded)
                return (Result.Failure(new[] { "Invalid credentials" }), null);

            return await GenerateAuthResponseAsync(user);
        }

        public async Task<(Result Result, AuthResponse Response)> RefreshTokenAsync(string token, string refreshToken)
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
                user.Email,
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

            return new UserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                user.AvatarUrl,
                user.CreatedAt,
                user.PreferredCurrency
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