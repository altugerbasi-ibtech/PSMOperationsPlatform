using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.Application.Runtime;
using PSMOperationsPlatform.CollectorSdk;
using RuntimeExecutionContext = PSMOperationsPlatform.CollectorSdk.ExecutionContext;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class ExecutionHistoryProjectionTests
{
    [Fact]
    public async Task Terminal_runtime_facts_map_explicitly_and_deterministically()
    {
        DateTimeOffset now = new(new DateTime(2026, 7, 29, 12, 0, 0),
            TimeSpan.FromHours(3));
        var time = new FixedTimeProvider(now);
        var events = new EventSink();
        var plugin = new Handler();
        CollectorRuntimeInput input = Input(now.DateTime);
        PreparedExecutionDispatch dispatch = Prepared(plugin, input, time);
        var runtime = new CollectorRuntime(new StateStore(), events, time);
        CollectorRuntimeResult result =
            await runtime.ExecuteAsync(dispatch, CancellationToken.None);
        ExecutionHistoryProjection first = ExecutionHistoryProjector.Project(
            dispatch, result.Run, events.Values, result.ArtifactMetadata, time);
        ExecutionHistoryProjection second = ExecutionHistoryProjector.Project(
            dispatch, result.Run, events.Values, result.ArtifactMetadata, time);

        Assert.Equal(ExecutionHistoryProjectionStatus.Completed,
            first.Run.ProjectionStatus);
        Assert.Equal(1, first.Run.CompletedStepCount);
        Assert.Equal(1, first.Run.AttemptCount);
        Assert.Equal("sample.plugin", first.Run.PluginId);
        Assert.Equal("ShortReadOnly", first.Policies[0].TimeoutPolicyCode);
        Assert.Empty(first.Artifacts);
        Assert.Equal(first.Run, second.Run);
        Assert.Equal(first.Steps, second.Steps);
        Assert.Equal(first.Attempts, second.Attempts);
        Assert.Equal(first.Transitions, second.Transitions);
        Assert.Equal(first.Artifacts, second.Artifacts);
        Assert.Equal(first.Policies, second.Policies);
        Assert.Equal(first.Transitions.OrderBy(x => x.TransitionSequence),
            first.Transitions);
        Assert.Throws<ArgumentException>(() => ExecutionHistoryProjector.Project(
            dispatch, result.Run, events.Values.Reverse(),
            result.ArtifactMetadata, time));
    }

    [Fact]
    public void Non_terminal_state_is_rejected_without_mutation()
    {
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-07-29T12:00:00+03:00"));
        var plugin = new Handler();
        CollectorRuntimeInput input = Input(time.GetLocalNow().DateTime);
        PreparedExecutionDispatch dispatch = Prepared(plugin, input, time);
        var state = new ExecutionRunState(dispatch.ExecutionRunId, input,
            time.GetLocalNow().DateTime);

        Assert.Throws<ArgumentException>(() => ExecutionHistoryProjector.Project(
            dispatch, state, [], null, time));
        Assert.Equal(ExecutionRunStatus.Created, state.Status);
    }

    private static CollectorRuntimeInput Input(DateTime now)
    {
        var step = new CollectorRuntimeStep(Guid.NewGuid(), "sample.strategy", 1,
            DecisionSubject.ManagedTargetServer, 1, 100, 100,
            ExecutionPolicyCodes.SerialCore, ExecutionPolicyCodes.ShortReadOnly, 1,
            ExecutionPolicyCodes.NoRetry, 1, ThrottlingClass.Lightweight, null,
            true, false, Array.AsReadOnly(Array.Empty<string>()));
        return new(Guid.NewGuid(), "sample.invalid", Guid.NewGuid(), 1,
            ExecutionPlanStatus.Ready, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            1, now, now, "history-test", Array.AsReadOnly([step]),
            Array.AsReadOnly(Array.Empty<CollectorRuntimeExclusion>()));
    }

    private static PreparedExecutionDispatch Prepared(
        ICollectorPlugin plugin, CollectorRuntimeInput input, TimeProvider time)
    {
        CollectorRuntimeStep step = input.Steps[0];
        CollectorPluginDescriptor descriptor = plugin.Describe();
        ExecutionPolicy policy = new ExecutionPolicyCatalog().Resolve(step);
        Guid runId = Guid.NewGuid();
        var context = new RuntimeExecutionContext(input.ManagedServerId, input.TargetFqdn,
            input.ExecutionPlanId, runId, step.ExecutionPlanStepId, step.StrategyCode,
            step.StrategyVersion, descriptor.PluginId, descriptor.PluginVersion,
            CollectorPluginSubject.ManagedTargetServer, input.SourceDecisionPlanId,
            input.SourceCapabilitySnapshotId, input.SourceInventoryRunId,
            input.SourceInventoryVersion, input.ExecutionPlanSchemaVersion,
            policy.PolicySchemaVersion, descriptor.DescriptorSchemaVersion,
            ExecutionEventSchemaVersion.Value, time);
        return new(runId, input, Array.AsReadOnly([
            new PreparedExecutionStep(step, plugin, descriptor, policy, context)]));
    }

    private sealed class Handler : ICollectorPlugin
    {
        public CollectorPluginDescriptor Describe() => new("sample.plugin",
            "sample.strategy", "Sample", "History projection test plugin", 1, 1,
            CollectorPluginSdkVersion.Current, CollectorPluginSdkVersion.Current,
            Array.AsReadOnly([CollectorPluginSubject.ManagedTargetServer]), true,
            CollectorEstimatedCost.Lightweight, Array.AsReadOnly(Array.Empty<string>()),
            true, false, true, false, false, Array.AsReadOnly([1]));
        public CollectorPluginValidationResult Validate(
            CollectorPluginValidationContext context) => new(PluginValidationStatus.Valid,
            "Valid", "Valid.", Array.AsReadOnly(Array.Empty<ExecutionWarning>()),
            Array.AsReadOnly(Array.Empty<PluginValidationIssue>()),
            new RuntimePluginCompatibilityMatrix().Evaluate(context.RuntimeVersion, Describe()));
        public Task<CollectorExecutionResult> ExecuteAsync(RuntimeExecutionContext context,
            ExecutionPolicy policy, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CollectorExecutionResult.Success(0, 1));
        }
    }

    private sealed class StateStore : IExecutionStateStore
    {
        public Task CreateAsync(ExecutionRunState run, CancellationToken token) =>
            Task.CompletedTask;
        public Task SaveAsync(ExecutionRunState run, CancellationToken token) =>
            Task.CompletedTask;
    }

    private sealed class EventSink : IExecutionEventSink
    {
        private readonly List<ExecutionEvent> values = [];
        public IReadOnlyList<ExecutionEvent> Values => values.AsReadOnly();
        public Task PublishAsync(ExecutionEvent value, CancellationToken token)
        {
            values.Add(value); return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone =>
            TimeZoneInfo.CreateCustomTimeZone("Fixed", TimeSpan.FromHours(3),
                "Fixed", "Fixed");
    }
}
