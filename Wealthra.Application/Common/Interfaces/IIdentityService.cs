using System.Threading.Tasks;
using Wealthra.Application.Common.Models;

namespace Wealthra.Application.Common.Interfaces
{
    public interface IIdentityService
    {
        Task<string> GetUserNameAsync(string userId);
        Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password);
        Task<Result> DeleteUserAsync(string userId);
        // We will add Login/Token generation here later
    }
}