using Microsoft.EntityFrameworkCore;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Api.Middleware;

public sealed class IpBlockMiddleware
{
    private readonly RequestDelegate _next;

    public IpBlockMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IServiceScopeFactory scopeFactory)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(ip))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTimeOffset.UtcNow;
            var blocked = await db.BlockedIpAddresses.AsNoTracking()
                .AnyAsync(b =>
                    b.IpAddress == ip &&
                    (b.ExpiresUtc == null || b.ExpiresUtc > now));

            if (blocked)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await _next(context);
    }
}
