using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UniCompanion.Worker.Workers;
using Xunit;

namespace UniCompanion.UnitTests;

public sealed class WorkerLifecycleTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private sealed class Probe(Action disposed) : IAsyncDisposable
    {
        public int Disposals { get; private set; }
        public ValueTask DisposeAsync() { Disposals++; disposed(); return ValueTask.CompletedTask; }
    }
    private sealed class Worker(IServiceScopeFactory scopes, Func<IServiceProvider, CancellationToken, Task<bool>> process,
        TimeSpan idle, TimeSpan failure, ILogger? logger = null)
        : ScopedPollingWorker(scopes, logger ?? NullLogger.Instance, TimeProvider.System, idle, failure)
    {
        protected override Task<bool> ProcessAsync(IServiceProvider services, CancellationToken stoppingToken) => process(services, stoppingToken);
    }
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task EachIterationGetsItsOwnAsyncScopeAndDisposesItBeforeWaiting()
    {
        var disposed = Signal(); var probes = new ConcurrentQueue<Probe>(); var iterations = 0;
        await using var services = new ServiceCollection().AddScoped(_ =>
        {
            var probe = new Probe(() => { if (Volatile.Read(ref iterations) == 2) disposed.TrySetResult(); });
            probes.Enqueue(probe); return probe;
        }).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var worker = new Worker(services.GetRequiredService<IServiceScopeFactory>(), (provider, _) =>
        {
            provider.GetRequiredService<Probe>();
            return Task.FromResult(Interlocked.Increment(ref iterations) == 1);
        }, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
        await worker.StartAsync(CancellationToken.None);
        await disposed.Task.WaitAsync(TestTimeout);
        await worker.StopAsync(CancellationToken.None).WaitAsync(TestTimeout);
        Assert.Equal(2, probes.Count); Assert.Equal(2, iterations);
        Assert.All(probes, probe => Assert.Equal(1, probe.Disposals));
        Assert.NotSame(probes.First(), probes.Last());
    }

    [Theory]
    [InlineData("work")]
    [InlineData("idle")]
    [InlineData("backoff")]
    public async Task ShutdownCancelsWorkIdleAndFailureBackoffWithoutFaulting(string phase)
    {
        var started = Signal(); var disposed = Signal();
        await using var services = new ServiceCollection().AddScoped(_ => new Probe(() => disposed.TrySetResult())).BuildServiceProvider();
        using var worker = new Worker(services.GetRequiredService<IServiceScopeFactory>(), async (provider, ct) =>
        {
            provider.GetRequiredService<Probe>(); started.TrySetResult();
            if (phase == "backoff") throw new InvalidOperationException("Fixture failure");
            if (phase == "work") await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return false;
        }, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
        await worker.StartAsync(CancellationToken.None);
        await (phase == "work" ? started.Task : disposed.Task).WaitAsync(TestTimeout);
        await worker.StopAsync(CancellationToken.None).WaitAsync(TestTimeout);
        await disposed.Task.WaitAsync(TestTimeout);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task TransientFailureDisposesScopeAndRecoversWithoutLoggingSensitiveExceptions()
    {
        var recovered = Signal(); var probes = new ConcurrentQueue<Probe>(); var iterations = 0; var logger = new RecordingLogger();
        await using var services = new ServiceCollection().AddScoped(_ =>
        {
            var probe = new Probe(() => { if (Volatile.Read(ref iterations) == 2) recovered.TrySetResult(); }); probes.Enqueue(probe); return probe;
        }).BuildServiceProvider();
        using var worker = new Worker(services.GetRequiredService<IServiceScopeFactory>(), (provider, _) =>
        {
            provider.GetRequiredService<Probe>();
            if (Interlocked.Increment(ref iterations) == 1) throw new InvalidOperationException("private-token-fixture");
            return Task.FromResult(false);
        }, TimeSpan.FromDays(1), TimeSpan.Zero, logger);
        await worker.StartAsync(CancellationToken.None); await recovered.Task.WaitAsync(TestTimeout);
        await worker.StopAsync(CancellationToken.None).WaitAsync(TestTimeout);
        Assert.All(probes, probe => Assert.Equal(1, probe.Disposals));
        Assert.Contains(logger.Messages, text => text.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, text => text.Contains("private-token-fixture", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OneLoopInFailureBackoffDoesNotStopAnotherLoop()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var failed = Signal(); var healthy = Signal(); var scopes = services.GetRequiredService<IServiceScopeFactory>();
        using var failing = new Worker(scopes, (_, _) => { failed.TrySetResult(); throw new InvalidOperationException(); }, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
        using var running = new Worker(scopes, (_, _) => { healthy.TrySetResult(); return Task.FromResult(false); }, TimeSpan.FromDays(1), TimeSpan.FromDays(1));
        await failing.StartAsync(CancellationToken.None); await failed.Task.WaitAsync(TestTimeout);
        await running.StartAsync(CancellationToken.None); await healthy.Task.WaitAsync(TestTimeout);
        await Task.WhenAll(failing.StopAsync(CancellationToken.None), running.StopAsync(CancellationToken.None)).WaitAsync(TestTimeout);
        Assert.True(failing.ExecuteTask!.IsCompletedSuccessfully); Assert.True(running.ExecuteTask!.IsCompletedSuccessfully);
    }

    private sealed class RecordingLogger : ILogger
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Enqueue(formatter(state, exception));
    }
}
