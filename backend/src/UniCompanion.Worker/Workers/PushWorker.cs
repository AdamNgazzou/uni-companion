using UniCompanion.Infrastructure.Push;

namespace UniCompanion.Worker.Workers;

public sealed class PushWorker(IServiceScopeFactory scopes, ILogger<PushWorker> logger, TimeProvider clock)
    : ScopedPollingWorker(scopes, logger, clock, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15))
{
    protected override async Task<bool> ProcessAsync(IServiceProvider services, CancellationToken stoppingToken)
    {
        var processor = services.GetRequiredService<PushProcessor>();
        var queued = await processor.QueueNextAsync(stoppingToken);
        var processed = await processor.ProcessNextAsync(stoppingToken);
        return queued || processed;
    }
}
