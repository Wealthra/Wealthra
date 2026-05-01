using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Wealthra.Application.Common.Interfaces;
using Wealthra.Domain.Entities;
using Wealthra.Infrastructure.Persistence;

namespace Wealthra.Infrastructure.Services;

public class RuntimeAppSettingsService : IRuntimeAppSettings
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public RuntimeAppSettingsService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var cacheKey = "appsetting:" + key;
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

        var row = await _db.AppConfigurationEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key, cancellationToken);

        _cache.Set(cacheKey, row?.Value, CacheDuration);
        return row?.Value;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var row = await _db.AppConfigurationEntries.FirstOrDefaultAsync(e => e.Key == key, cancellationToken);
        if (row == null)
        {
            row = new AppConfigurationEntry { Key = key, Value = value, UpdatedUtc = DateTimeOffset.UtcNow };
            _db.AppConfigurationEntries.Add(row);
        }
        else
        {
            row.Value = value;
            row.UpdatedUtc = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _cache.Remove("appsetting:" + key);
    }
}
