using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Models.Export;

namespace Wealthra.Application.Common.Interfaces
{
    public interface IExportService
    {
        Task<byte[]> ExportToPdfAsync(ExportRequestDto request, CancellationToken cancellationToken = default);
        Task<byte[]> ExportToExcelAsync(ExportRequestDto request, CancellationToken cancellationToken = default);
    }
}
