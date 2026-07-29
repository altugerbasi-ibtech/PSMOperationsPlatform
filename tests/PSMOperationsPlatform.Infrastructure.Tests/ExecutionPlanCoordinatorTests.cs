using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class ExecutionPlanCoordinatorTests
{
    [Fact]
    public async Task CancellationPropagatesBeforePlanningAndCreatesNoTrackedPlan()
    {
        using OperationsDbContext context = Context();
        var engine = new RecordingEngine();
        var coordinator = new ExecutionPlanCoordinator(context, engine, TimeProvider.System,
            NullLogger<ExecutionPlanCoordinator>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.BuildAndReplaceAsync(Guid.NewGuid(), DecisionPlan(), cancellation.Token));

        Assert.Equal(0, engine.CallCount);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task CatastrophicPlanningFailureCreatesNoTrackedReplacement()
    {
        using OperationsDbContext context = Context();
        var coordinator = new ExecutionPlanCoordinator(context, new ThrowingEngine(),
            TimeProvider.System, NullLogger<ExecutionPlanCoordinator>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            coordinator.BuildAndReplaceAsync(Guid.NewGuid(), DecisionPlan(), CancellationToken.None));

        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static OperationsDbContext Context()
    {
        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        return new OperationsDbContext(options);
    }

    private static CollectorDecisionPlan DecisionPlan() =>
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            4, 1, 1, new DateTime(2026, 7, 29, 10, 0, 0),
            CollectorDecisionStatus.Eligible, [], []);

    private sealed class RecordingEngine : IExecutionPlanEngine
    {
        public int CallCount { get; private set; }
        public ExecutionPlanResult Build(ExecutionPlanInput input)
        {
            CallCount++;
            throw new InvalidOperationException();
        }
    }

    private sealed class ThrowingEngine : IExecutionPlanEngine
    {
        public ExecutionPlanResult Build(ExecutionPlanInput input) =>
            throw new ArgumentException("InvalidExecutionPlanInput");
    }
}
