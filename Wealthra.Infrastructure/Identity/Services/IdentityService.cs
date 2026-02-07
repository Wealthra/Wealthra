using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Common.Models;
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

        public async Task<(Result Result, string UserId)> CreateUserAsync(string email, string password)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                CreatedAt = System.DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, password);

            return (result.ToApplicationResult(), user.Id);
        }

        public async Task<Result> DeleteUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                return await DeleteUserAsync(user);
            }
            return Result.Success();
        }

        public async Task<Result> DeleteUserAsync(ApplicationUser user)
        {
            var result = await _userManager.DeleteAsync(user);
            return result.ToApplicationResult();
        }

        // New method specific to Infrastructure (not in generic interface if we want to keep Application layer unaware of JWT details, 
        // but for simplicity we will handle the Command handler logic here or inject this service)
        public async Task<string?> AuthenticateAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return null;

            var result = await _signInManager.CheckPasswordSignInAsync(user, password, false);

            if (!result.Succeeded) return null;

            var roles = await _userManager.GetRolesAsync(user);
            return _tokenGenerator.GenerateToken(user, roles);
        }
    }

    // Helper extension to map IdentityResult to our Application Result
    public static class IdentityResultExtensions
    {
        public static Result ToApplicationResult(this IdentityResult result)
        {
            return result.Succeeded
                ? Result.Success()
                : Result.Failure(System.Linq.Enumerable.Select(result.Errors, e => e.Description));
        }
    }
}