using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Wealthra.Application.Common.Exceptions;
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

            //Log entry (Info)
            _logger.LogInformation("Wealthra Request: {Name} {@UserId} {@Request}", requestName, userId, request);

            var timer = Stopwatch.StartNew();

            try
            {
                var response = await next();
                timer.Stop();
                return response;
            }
            catch (Exception ex)
            {
                if (ex is ValidationException || ex is NotFoundException)
                {
                    throw;
                }

                _logger.LogError(ex, "Wealthra Request Failure: {Name} {@UserId}", requestName, userId);
                throw;
            }
        }
    }
}