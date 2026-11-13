using UniCompanion.Infrastructure.Activity;

namespace UniCompanion.Worker.Workers;

public sealed class ActivityWorker(IServiceScopeFactory scopes, ILogger<ActivityWorker> logger, TimeProvider clock)
    : ScopedPollingWorker(scopes, logger, clock, TimeSpan.FromMilliseconds(1500), TimeSpan.FromSeconds(5))
{
    protected override Task<bool> ProcessAsync(IServiceProvider services, CancellationToken stoppingToken) =>
        services.GetRequiredService<ActivityDispatcher>().DispatchNextAsync(stoppingToken);
}
