using MediatR;
using Wealthra.Application.Common.Constants;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Admin.Commands.SetAiRuntimeSettings;

public record SetAiRuntimeSettingsCommand(string? EnrichmentModel, string? DefaultChatModel) : IRequest<Unit>;

public class SetAiRuntimeSettingsCommandHandler : IRequestHandler<SetAiRuntimeSettingsCommand, Unit>
{
    private readonly IRuntimeAppSettings _runtimeAppSettings;

    public SetAiRuntimeSettingsCommandHandler(IRuntimeAppSettings runtimeAppSettings)
    {
        _runtimeAppSettings = runtimeAppSettings;
    }

    public async Task<Unit> Handle(SetAiRuntimeSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.EnrichmentModel))
        {
            await _runtimeAppSettings.SetAsync(AppSettingsKeys.AiEnrichmentModel, request.EnrichmentModel.Trim(), cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.DefaultChatModel))
        {
            await _runtimeAppSettings.SetAsync(AppSettingsKeys.AiDefaultChatModel, request.DefaultChatModel.Trim(), cancellationToken);
        }

        return Unit.Value;
    }
}
