namespace Wealthra.Application.Common.Interfaces;

public interface IAdminAuditService
{
    Task WriteAsync(
        string actorUserId,
        string action,
        string? targetUserId,
        object? details,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}
