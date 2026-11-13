using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UniCompanion.Worker.Workers;

/// <summary>
/// Owns host lifecycle only. Each iteration gets a fresh async scope; processors
/// remain responsible for durable claims, transactions, idempotency and retries.
/// </summary>
public abstract class ScopedPollingWorker(
    IServiceScopeFactory scopes,
    ILogger logger,
    TimeProvider clock,
    TimeSpan idleDelay,
    TimeSpan failureDelay) : BackgroundService
{
    protected abstract Task<bool> ProcessAsync(IServiceProvider services, CancellationToken stoppingToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var name = GetType().Name;
        logger.LogInformation("{Worker} started", name);
        var delay = TimeSpan.Zero;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (delay > TimeSpan.Zero) await Task.Delay(delay, clock, stoppingToken);
                bool processed;
                await using (var scope = scopes.CreateAsyncScope())
                    processed = await ProcessAsync(scope.ServiceProvider, stoppingToken);
                delay = processed ? TimeSpan.Zero : idleDelay;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                // Exceptions can contain credentials, user text or provider responses.
                logger.LogWarning("{Worker} iteration failed ({ErrorType}); retrying", name, error.GetType().Name);
                delay = failureDelay;
            }
        }
    }
}
