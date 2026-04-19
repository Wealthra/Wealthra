using Wealthra.Application.Common.Interfaces;

namespace Wealthra.Api.Realtime;

public class AdminSnapshotHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminSnapshotHostedService> _logger;

    public AdminSnapshotHostedService(IServiceScopeFactory scopeFactory, ILogger<AdminSnapshotHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
                var realtime = scope.ServiceProvider.GetRequiredService<IAdminRealtimeService>();
                var snapshot = await identityService.GetAppUsageSummaryAsync();
                await realtime.PublishSnapshotAsync(snapshot, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish admin snapshot.");
            }
        }
    }
}
