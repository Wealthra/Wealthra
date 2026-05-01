using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Services;

public class AdminAuditService : IAdminAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly ApplicationDbContext _db;

    public AdminAuditService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(
        string actorUserId,
        string action,
        string? targetUserId,
        object? details,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            TargetUserId = targetUserId,
            DetailsJson = details == null ? null : JsonSerializer.Serialize(details, JsonOptions),
            IpAddress = ipAddress,
            CreatedUtc = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
