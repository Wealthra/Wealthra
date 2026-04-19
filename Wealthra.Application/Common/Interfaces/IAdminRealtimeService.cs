namespace Wealthra.Application.Common.Interfaces;

public interface IAdminRealtimeService
{
    Task PublishActivityAsync(string activityType, string message, object? payload = null, CancellationToken cancellationToken = default);
    Task PublishSnapshotAsync(object snapshot, CancellationToken cancellationToken = default);
}
