using Wealthra.Application.Features.Expenses.Models;

namespace Wealthra.Application.Common.Interfaces
{
    public interface IExpenseExtractionService
    {
        Task<IReadOnlyList<ExtractedExpenseDto>> ExtractFromImageAsync(
            Stream fileStream,
            string fileName,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ExtractedExpenseDto>> ExtractFromAudioAsync(
            Stream fileStream,
            string fileName,
            CancellationToken cancellationToken = default);
    }
}
