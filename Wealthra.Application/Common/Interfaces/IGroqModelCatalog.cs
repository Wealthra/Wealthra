using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Common.Interfaces;

public interface IGroqModelCatalog
{
    Task<IReadOnlyList<GroqAvailableModelDto>> ListModelsAsync(CancellationToken cancellationToken = default);
}
