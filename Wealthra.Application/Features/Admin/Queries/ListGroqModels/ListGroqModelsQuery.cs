using MediatR;
using Microsoft.Extensions.Configuration;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.ListGroqModels;

public record ListGroqModelsQuery : IRequest<GroqModelsListDto>;

public class ListGroqModelsQueryHandler : IRequestHandler<ListGroqModelsQuery, GroqModelsListDto>
{
    private readonly IGroqModelCatalog _groqModelCatalog;
    private readonly IConfiguration _configuration;

    public ListGroqModelsQueryHandler(IGroqModelCatalog groqModelCatalog, IConfiguration configuration)
    {
        _groqModelCatalog = groqModelCatalog;
        _configuration = configuration;
    }

    public async Task<GroqModelsListDto> Handle(ListGroqModelsQuery request, CancellationToken cancellationToken)
    {
        var apiKeyConfigured = !string.IsNullOrWhiteSpace(_configuration["Groq:ApiKey"]);
        if (!apiKeyConfigured)
        {
            return new GroqModelsListDto(Array.Empty<GroqAvailableModelDto>(), false);
        }

        var models = await _groqModelCatalog.ListModelsAsync(cancellationToken);
        return new GroqModelsListDto(models, true);
    }
}
