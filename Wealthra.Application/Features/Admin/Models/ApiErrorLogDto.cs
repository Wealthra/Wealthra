namespace Wealthra.Application.Features.Admin.Models;

public record ApiErrorLogDto(
    long Id,
    int StatusCode,
    string Path,
    string Method,
    string? UserId,
    string? ExceptionType,
    string Message,
    string? CorrelationId,
    DateTimeOffset CreatedUtc);
