using FluentValidation;
using MediatR;
using Wealthra.Application.Common.Constants;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Admin.Commands.SetFxProviderOrder;

public record SetFxProviderOrderCommand(string ProviderOrderJson) : IRequest<Unit>;

public class SetFxProviderOrderCommandValidator : AbstractValidator<SetFxProviderOrderCommand>
{
    public SetFxProviderOrderCommandValidator()
    {
        RuleFor(x => x.ProviderOrderJson).NotEmpty();
    }
}

public class SetFxProviderOrderCommandHandler : IRequestHandler<SetFxProviderOrderCommand, Unit>
{
    private readonly IRuntimeAppSettings _runtimeAppSettings;

    public SetFxProviderOrderCommandHandler(IRuntimeAppSettings runtimeAppSettings)
    {
        _runtimeAppSettings = runtimeAppSettings;
    }

    public async Task<Unit> Handle(SetFxProviderOrderCommand request, CancellationToken cancellationToken)
    {
        await _runtimeAppSettings.SetAsync(AppSettingsKeys.FxProviderOrder, request.ProviderOrderJson.Trim(), cancellationToken);
        return Unit.Value;
    }
}
