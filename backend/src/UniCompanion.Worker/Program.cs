using UniCompanion.Infrastructure;
using UniCompanion.Infrastructure.Study;
using UniCompanion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
// Shared Identity authorization services require the routing data source even in a non-HTTP host.
builder.Services.AddRouting();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Services.AddHostedService<StudyWorker>();
await builder.Build().RunAsync();

internal sealed class StudyWorker(IServiceScopeFactory scopes, ILogger<StudyWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Study worker started; concurrency 1.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (!await db.Database.CanConnectAsync(stoppingToken)) throw new InvalidOperationException("Database not ready.");
                if (!await scope.ServiceProvider.GetRequiredService<StudyProcessor>().ProcessNextAsync(stoppingToken))
                    await Task.Delay(1500, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                logger.LogWarning("Study worker temporarily unavailable ({ErrorType})", e.GetType().Name);
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
