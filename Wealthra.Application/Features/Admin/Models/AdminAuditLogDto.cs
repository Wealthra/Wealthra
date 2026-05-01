namespace Wealthra.Application.Features.Admin.Models;

public record AdminAuditLogDto(
    long Id,
    string ActorUserId,
    string Action,
    string? TargetUserId,
    string? DetailsJson,
    string? IpAddress,
    DateTimeOffset CreatedUtc);
