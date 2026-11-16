using UniCompanion.Infrastructure.Learning;

namespace UniCompanion.Worker.Workers;

public sealed class ReminderWorker(IServiceScopeFactory scopes, ILogger<ReminderWorker> logger, TimeProvider clock)
    : ScopedPollingWorker(scopes, logger, clock, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30))
{
    protected override Task<bool> ProcessAsync(IServiceProvider services, CancellationToken stoppingToken) =>
        services.GetRequiredService<RevisionReminders>().DispatchNextAsync(stoppingToken);
}
