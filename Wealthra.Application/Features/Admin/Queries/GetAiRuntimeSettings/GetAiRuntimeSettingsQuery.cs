using MediatR;
using Microsoft.Extensions.Configuration;
using Wealthra.Application.Common.Constants;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Application.Features.Admin.Models;

namespace Wealthra.Application.Features.Admin.Queries.GetAiRuntimeSettings;

public record GetAiRuntimeSettingsQuery : IRequest<AiRuntimeSettingsDto>;

public class GetAiRuntimeSettingsQueryHandler : IRequestHandler<GetAiRuntimeSettingsQuery, AiRuntimeSettingsDto>
{
    private readonly IRuntimeAppSettings _runtimeAppSettings;
    private readonly IConfiguration _configuration;

    public GetAiRuntimeSettingsQueryHandler(IRuntimeAppSettings runtimeAppSettings, IConfiguration configuration)
    {
        _runtimeAppSettings = runtimeAppSettings;
        _configuration = configuration;
    }

    public async Task<AiRuntimeSettingsDto> Handle(GetAiRuntimeSettingsQuery request, CancellationToken cancellationToken)
    {
        var enrich = await _runtimeAppSettings.GetAsync(AppSettingsKeys.AiEnrichmentModel, cancellationToken)
            ?? _configuration["Groq:Model"];
        var chat = await _runtimeAppSettings.GetAsync(AppSettingsKeys.AiDefaultChatModel, cancellationToken)
            ?? _configuration["Groq:Model"];
        return new AiRuntimeSettingsDto(enrich, chat);
    }
}
