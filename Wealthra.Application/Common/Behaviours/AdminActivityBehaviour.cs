using MediatR;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Common.Behaviours;

public class AdminActivityBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly string[] TrackedPrefixes = ["CreateExpense", "UpdateExpense", "DeleteExpense", "CreateIncome", "UpdateIncome", "DeleteIncome"];
    private readonly IAdminRealtimeService _adminRealtimeService;
    private readonly ICurrentUserService _currentUserService;

    public AdminActivityBehaviour(IAdminRealtimeService adminRealtimeService, ICurrentUserService currentUserService)
    {
        _adminRealtimeService = adminRealtimeService;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        var requestName = typeof(TRequest).Name;
        if (TrackedPrefixes.Any(requestName.StartsWith))
        {
            await _adminRealtimeService.PublishActivityAsync(
                "finance.command",
                $"{requestName} handled.",
                new { request = requestName, userId = _currentUserService.UserId },
                cancellationToken);
        }

        return response;
    }
}
