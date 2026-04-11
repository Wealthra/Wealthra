using System.Threading;
using System.Threading.Tasks;

namespace Wealthra.Application.Common.Interfaces
{
    public interface IUsageTrackerService
    {
        Task<bool> CanUseOcrAsync(CancellationToken cancellationToken);
        Task<bool> CanUseSttAsync(CancellationToken cancellationToken);
        Task IncrementOcrAsync(CancellationToken cancellationToken);
        Task IncrementSttAsync(CancellationToken cancellationToken);
    }
}
