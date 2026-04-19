namespace Wealthra.Api.Realtime;

public record AdminActivityEvent(
    string Type,
    string Message,
    DateTimeOffset OccurredOn,
    object? Payload);

public record AdminSnapshotEvent(
    DateTimeOffset GeneratedAt,
    object Snapshot);
