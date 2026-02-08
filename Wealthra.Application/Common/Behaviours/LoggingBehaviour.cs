using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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

        // List of property names that should NEVER be logged
        private static readonly string[] SensitiveProperties = { "Password", "Token", "RefreshToken", "Secret", "NewPassword" };

        public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger, ICurrentUserService currentUserService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var userId = _currentUserService.UserId ?? "Anonymous";

            var filteredRequest = ScrubSensitiveData(request);

            _logger.LogInformation("Wealthra Request: {Name} {@UserId} {@Request}",
                requestName, userId, filteredRequest);

            var timer = Stopwatch.StartNew();
            try
            {
                return await next();
            }
            finally
            {
                timer.Stop();
                if (timer.ElapsedMilliseconds > 500)
                {
                    _logger.LogWarning("Wealthra Long Running Request: {Name} ({ElapsedMilliseconds} ms) {@UserId}",
                        requestName, timer.ElapsedMilliseconds, userId);
                }
            }
        }

        private object ScrubSensitiveData(TRequest request)
        {
            var type = request.GetType();
            var props = type.GetProperties();

            // If the request has no sensitive properties, return it as is for performance
            if (!props.Any(p => SensitiveProperties.Contains(p.Name)))
            {
                return request;
            }

            // Create a dictionary of properties, masking the sensitive ones
            var dict = new Dictionary<string, object?>();
            foreach (var prop in props)
            {
                var value = SensitiveProperties.Contains(prop.Name)
                    ? "*** MASKED ***"
                    : prop.GetValue(request);

                dict.Add(prop.Name, value);
            }

            return dict;
        }
    }
}