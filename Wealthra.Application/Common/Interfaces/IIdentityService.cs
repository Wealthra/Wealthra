using System.Threading.Tasks;
using Wealthra.Application.Common.Models;
using Wealthra.Application.Features.Identity.Models; // Import the new model

namespace Wealthra.Application.Common.Interfaces
{
    public interface IIdentityService
    {
        Task<string> GetUserNameAsync(string userId);
        Task<(Result Result, string UserId)> CreateUserAsync(string email, string password, string firstName, string lastName);
        Task<(Result Result, AuthResponse Response)> LoginAsync(string email, string password);
        Task<(Result Result, AuthResponse Response)> RefreshTokenAsync(string token, string refreshToken);
        Task<Result> DeleteUserAsync(string userId);
        Task<UserDto?> GetUserDetailsAsync(string userId);
        Task<Result> UpdateUserAsync(string userId, string firstName, string lastName, string? avatarUrl);
        Task<Result> ChangePreferredCurrencyAsync(string userId, string currency);
        Task<Result> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<bool> UpdateUserTierAsync(string email, Wealthra.Domain.Enums.SubscriptionTier newTier);
    }
}