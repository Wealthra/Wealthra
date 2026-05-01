namespace Wealthra.Domain.Entities;

public class ApiErrorLog
{
    public long Id { get; set; }
    public int StatusCode { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? ExceptionType { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
