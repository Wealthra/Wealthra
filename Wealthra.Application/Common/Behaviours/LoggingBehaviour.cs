using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Application.Common.Behaviours
{
    public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;
        private readonly ICurrentUserService _currentUserService;

        public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger, ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var userId = _currentUserService.UserId ?? string.Empty;

            _logger.LogInformation("Wealthra Request: {Name} {@UserId} {@Request}",
                requestName, userId, request);

            var timer = Stopwatch.StartNew();

            try
            {
                var response = await next();
                timer.Stop();

                if (timer.ElapsedMilliseconds > 500) // Performance Warning
                {
                    _logger.LogWarning("Wealthra Long Running Request: {Name} ({ElapsedMilliseconds} milliseconds) {@UserId} {@Request}",
                       requestName, timer.ElapsedMilliseconds, userId, request);
                }

                return response;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Wealthra Request Failure: {Name} {@UserId}", requestName, userId);
                throw;
            }
        }
    }
}