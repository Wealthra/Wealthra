using MediatR;
using Wealthra.Application.Common.Constants;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Features.Admin.Queries.GetFxProviderOrder;

public record GetFxProviderOrderQuery : IRequest<string?>;

public class GetFxProviderOrderQueryHandler : IRequestHandler<GetFxProviderOrderQuery, string?>
{
    private readonly IRuntimeAppSettings _runtimeAppSettings;

    public GetFxProviderOrderQueryHandler(IRuntimeAppSettings runtimeAppSettings)
    {
        _runtimeAppSettings = runtimeAppSettings;
    }

    public Task<string?> Handle(GetFxProviderOrderQuery request, CancellationToken cancellationToken)
        => _runtimeAppSettings.GetAsync(AppSettingsKeys.FxProviderOrder, cancellationToken);
}
