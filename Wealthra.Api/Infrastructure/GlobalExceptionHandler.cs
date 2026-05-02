using System.Diagnostics;
using System.Security.Claims;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Exceptions;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Api.Infrastructure
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var problemDetails = new ProblemDetails
            {
                Instance = httpContext.Request.Path
            };

            if (exception is ValidationException validationException)
            {
                problemDetails.Title = "Validation Error";
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Detail = "One or more validation errors occurred.";
                problemDetails.Extensions["errors"] = validationException.Errors;
                _logger.LogWarning(
                    "Validation failed for {RequestPath}. Errors: {@Errors}",httpContext.Request.Path, validationException.Errors
                );
            }
            else if (exception is UnauthorizedAccessException)
            {
                problemDetails.Title = "Unauthorized";
                problemDetails.Status = StatusCodes.Status401Unauthorized;
            }
            else if (exception is NotFoundException notFoundEx)
            {
                problemDetails.Title = "Not Found";
                problemDetails.Status = StatusCodes.Status404NotFound;
                problemDetails.Detail = notFoundEx.Message;
            }
            else if (exception is ForbiddenAccessException forbiddenEx)
            {
                problemDetails.Title = "Forbidden";
                problemDetails.Status = StatusCodes.Status403Forbidden;
                problemDetails.Detail = forbiddenEx.Message;
            }
            else if (exception is AuthenticationException mailAuthEx)
            {
                problemDetails.Title = "SMTP authentication failed";
                problemDetails.Status = StatusCodes.Status502BadGateway;
                problemDetails.Detail = TruncateForClient(mailAuthEx.Message);
                _logger.LogWarning(mailAuthEx, "SMTP authentication rejected by mail server.");
            }
            else if (exception is SmtpCommandException smtpCmdEx)
            {
                problemDetails.Title = "SMTP error";
                problemDetails.Status = StatusCodes.Status502BadGateway;
                problemDetails.Detail = TruncateForClient(smtpCmdEx.Message);
                _logger.LogWarning(smtpCmdEx, "SMTP command failed.");
            }
            else
            {
                // Fallback for unhandled errors (500)
                // Log the full error securely on the server
                _logger.LogError(exception, "An unhandled exception occurred.");

                problemDetails.Title = "Server Error";
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Detail = "An internal error occurred. Please contact support.";
            }

            if (problemDetails.Status == StatusCodes.Status500InternalServerError)
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? httpContext.User.FindFirstValue("sub");
                    db.ApiErrorLogs.Add(new ApiErrorLog
                    {
                        StatusCode = problemDetails.Status.Value,
                        Path = httpContext.Request.Path,
                        Method = httpContext.Request.Method,
                        UserId = userId,
                        ExceptionType = exception.GetType().FullName,
                        Message = exception.Message,
                        StackTrace = exception.StackTrace,
                        CorrelationId = Activity.Current?.Id,
                        CreatedUtc = DateTimeOffset.UtcNow
                    });
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist API error log.");
                }
            }

            httpContext.Response.StatusCode = problemDetails.Status.Value;

            await httpContext.Response
                .WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }

        private static string TruncateForClient(string message, int maxLength = 500)
        {
            if (string.IsNullOrEmpty(message)) return "The mail server rejected the request.";
            var t = message.Trim();
            return t.Length <= maxLength ? t : t[..maxLength] + "…";
        }
    }
}