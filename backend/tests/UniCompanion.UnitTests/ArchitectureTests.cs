using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Worker.Workers;
using Xunit;

namespace UniCompanion.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void CoreStaysIndependentOfHostingPersistenceAndAdapters()
    {
        var references = typeof(StudyJob).Assembly.GetReferencedAssemblies();
        Assert.All(references, reference => Assert.True(reference.Name!.StartsWith("System.", StringComparison.Ordinal) || reference.Name is "System" or "netstandard", $"Core must not depend on {reference.Name}."));
    }

    [Theory]
    [InlineData(typeof(AppDbContext), "UniCompanion.Api")]
    [InlineData(typeof(AppDbContext), "UniCompanion.Worker")]
    [InlineData(typeof(StudyWorker), "UniCompanion.Api")]
    public void WorkflowsAndWorkerDoNotDependOnTheHttpHost(Type marker, string forbiddenAssembly)
    {
        Assert.DoesNotContain(marker.Assembly.GetReferencedAssemblies(), reference => reference.Name == forbiddenAssembly);
    }

    [Fact]
    public void EveryHostedLoopUsesTheScopeSafeLifecycle()
    {
        Type[] workers = [typeof(StudyWorker), typeof(ActivityWorker), typeof(ReminderWorker), typeof(PushWorker)];
        Assert.All(workers, worker => Assert.True(typeof(ScopedPollingWorker).IsAssignableFrom(worker)));
    }
}
