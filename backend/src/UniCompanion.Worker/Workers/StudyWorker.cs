using UniCompanion.Infrastructure.Study;

namespace UniCompanion.Worker.Workers;

public sealed class StudyWorker(IServiceScopeFactory scopes, ILogger<StudyWorker> logger, TimeProvider clock)
    : ScopedPollingWorker(scopes, logger, clock, TimeSpan.FromMilliseconds(1500), TimeSpan.FromSeconds(5))
{
    protected override Task<bool> ProcessAsync(IServiceProvider services, CancellationToken stoppingToken) =>
        services.GetRequiredService<StudyProcessor>().ProcessNextAsync(stoppingToken);
}
