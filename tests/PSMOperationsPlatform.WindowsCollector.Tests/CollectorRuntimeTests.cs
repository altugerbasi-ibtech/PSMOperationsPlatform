using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.Application.Runtime;
using PSMOperationsPlatform.CollectorSdk;
using RuntimeExecutionContext = PSMOperationsPlatform.CollectorSdk.ExecutionContext;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class CollectorRuntimeTests
{
    [Theory]
    [InlineData(ExecutionPolicyCodes.ShortReadOnly, 1)]
    [InlineData(ExecutionPolicyCodes.StandardReadOnly, 5)]
    [InlineData(ExecutionPolicyCodes.LongReadOnly, 15)]
    public void Policy_catalog_resolves_versioned_timeout(string code, int minutes)
    {
        ExecutionPolicy policy = new ExecutionPolicyCatalog().Resolve(
            Step(timeout: code));
        Assert.Equal(TimeSpan.FromMinutes(minutes), policy.Timeout.Timeout);
        Assert.Equal(1, policy.Timeout.Version);
        Assert.True(policy.Throttling.MaximumConcurrency > 0);
    }

    [Fact]
    public void Policy_catalog_rejects_unknown_policy_and_version()
    {
        Assert.Equal(RuntimeFailureCategory.ExecutionPolicyNotFound,
            Assert.Throws<ExecutionPolicyException>(() =>
                new ExecutionPolicyCatalog().Resolve(Step(timeout: "Unknown"))).Category);
        Assert.Equal(RuntimeFailureCategory.ExecutionPolicyVersionUnsupported,
            Assert.Throws<ExecutionPolicyException>(() =>
                new ExecutionPolicyCatalog().Resolve(Step(timeoutVersion: 2))).Category);
    }

    [Fact]
    public void Handler_registry_is_explicit_and_rejects_duplicates_and_writes()
    {
        var handler = new FakeHandler("Strategy", _ => CollectorExecutionResult.Success());
        Assert.True(new CollectorPluginRegistry([handler],
                new RuntimePluginCompatibilityMatrix(), "1.0")
            .TryResolve("Strategy", out _));
        Assert.Throws<ArgumentException>(() =>
            new CollectorPluginRegistry([handler, handler],
                new RuntimePluginCompatibilityMatrix(), "1.0"));
        Assert.Throws<ArgumentException>(() => new CollectorPluginRegistry(
            [new FakeHandler("Strategy", _ => CollectorExecutionResult.Success(), false)],
            new RuntimePluginCompatibilityMatrix(), "1.0"));
    }

    [Fact]
    public async Task Successful_handler_produces_completed_state_events_and_metrics()
    {
        var handler = new FakeHandler("Strategy",
            _ => CollectorExecutionResult.Success(12, 3));
        var store = new FakeStore();
        var sink = new FakeEventSink();
        CollectorRuntimeResult result = await Runtime(handler, store, sink)
            .ExecuteAsync(Prepared(handler, Input()), CancellationToken.None);

        Assert.Equal(ExecutionRunStatus.Completed, result.Run.Status);
        ExecutionStepState step = Assert.Single(result.Run.Steps);
        Assert.Equal(ExecutionStepStatus.Completed, step.Status);
        Assert.Equal(12, result.Run.BytesCollected);
        Assert.Equal(3, result.Run.ObjectsCollected);
        Assert.Equal(1, result.Run.AttemptCount);
        Assert.Equal(0, result.Run.RetryCount);
        Assert.Contains(sink.Events, x => x.EventType == ExecutionEventType.ExecutionRunCreated);
        Assert.Contains(sink.Events, x => x.EventType == ExecutionEventType.ExecutionStepAttemptStarted);
        Assert.Contains(sink.Events, x => x.EventType == ExecutionEventType.ExecutionRunCompleted);
        Assert.Equal(Enumerable.Range(1, sink.Events.Count).Select(x => (long)x),
            sink.Events.Select(x => x.Sequence).Order());
        Assert.True(store.SaveCount >= 4);
    }

    [Fact]
    public async Task Handler_failure_is_isolated_and_run_completes_with_failures()
    {
        var handler = new FakeHandler("Strategy",
            (Func<CancellationToken, CollectorExecutionResult>)(_ => throw new InvalidOperationException()));
        CollectorRuntimeResult result = await Runtime(handler, new FakeStore(),
            new FakeEventSink()).ExecuteAsync(Prepared(handler, Input()),
            CancellationToken.None);
        Assert.Equal(ExecutionRunStatus.CompletedWithFailures, result.Run.Status);
        Assert.Equal(ExecutionStepStatus.Failed, Assert.Single(result.Run.Steps).Status);
        Assert.Equal(RuntimeFailureCategory.HandlerExecutionFailure,
            Assert.Single(result.Run.Steps).FailureCategory);
    }

    [Fact]
    public async Task Missing_handler_is_classified_without_executing_an_exclusion()
    {
        var runtime = new StubRuntime();
        var matrix = new RuntimePluginCompatibilityMatrix();
        var dispatcher = new ExecutionDispatcher(
            new CollectorPluginRegistry(Array.Empty<ICollectorPlugin>(), matrix, "1.0"),
            new ExecutionPolicyCatalog(), matrix, new PluginPolicyCompatibilityValidator(),
            runtime, new FakeEventSink(), TimeProvider.System);
        ExecutionDispatchResult result = await dispatcher.DispatchAsync(
            new ExecutionDispatchRequest(Input(withExclusion: true)), CancellationToken.None);
        Assert.Equal(DispatchFailureCategory.HandlerNotFound, result.FailureCategory);
        Assert.False(runtime.Invoked);
    }

    [Fact]
    public async Task Timeout_is_distinct_and_no_retry_policy_stops_after_one_attempt()
    {
        var handler = new FakeHandler("Strategy", async cancellation =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellation);
            return CollectorExecutionResult.Success();
        });
        var policy = new TestPolicy(TimeSpan.FromMilliseconds(20), maxAttempts: 1);
        CollectorRuntimeResult result = await Runtime(handler, new FakeStore(),
            new FakeEventSink(), policy).ExecuteAsync(Prepared(handler, Input(), policy),
            CancellationToken.None);
        ExecutionStepState step = Assert.Single(result.Run.Steps);
        Assert.Equal(ExecutionStepStatus.TimedOut, step.Status);
        Assert.Equal(RuntimeFailureCategory.Timeout, step.FailureCategory);
        Assert.Equal(1, step.AttemptCount);
    }

    [Fact]
    public async Task Retry_is_runtime_owned_and_attempts_are_separate()
    {
        int calls = 0;
        var handler = new FakeHandler("Strategy", _ =>
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new InvalidOperationException();
            return CollectorExecutionResult.Success(4, 1);
        });
        var policy = new TestPolicy(TimeSpan.FromSeconds(1), maxAttempts: 2);
        CollectorRuntimeResult result = await Runtime(handler, new FakeStore(),
            new FakeEventSink(), policy).ExecuteAsync(Prepared(handler, Input(), policy),
            CancellationToken.None);
        ExecutionStepState step = Assert.Single(result.Run.Steps);
        Assert.Equal(ExecutionStepStatus.Completed, step.Status);
        Assert.Equal(2, step.AttemptCount);
        Assert.Equal(1, step.RetryCount);
        Assert.Equal([1, 2], step.Attempts.Select(x => x.AttemptNumber));
    }

    [Fact]
    public async Task External_cancellation_propagates_and_persists_cancelled_state()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new FakeHandler("Strategy", async token =>
        {
            cancellation.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return CollectorExecutionResult.Success();
        });
        var store = new FakeStore();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Runtime(handler, store, new FakeEventSink()).ExecuteAsync(Prepared(handler, Input()),
                cancellation.Token));
        Assert.Equal(ExecutionRunStatus.Cancelled, store.Last!.Status);
        Assert.Equal(RuntimeFailureCategory.Cancellation, store.Last.FailureCategory);
    }

    [Fact]
    public void Terminal_state_cannot_return_to_running()
    {
        ExecutionRunState run = new(Guid.NewGuid(), Input(), DateTime.Today);
        ExecutionStepState step = Assert.Single(run.Steps);
        step.Transition(ExecutionStepStatus.Queued);
        step.Transition(ExecutionStepStatus.Running);
        step.Transition(ExecutionStepStatus.Completed);
        Assert.Throws<InvalidOperationException>(() =>
            step.Transition(ExecutionStepStatus.Running));
    }

    [Fact]
    public async Task Event_failure_does_not_replace_authoritative_completed_state()
    {
        var sink = new FakeEventSink { Throw = true };
        CollectorRuntimeResult result = await Runtime(
            new FakeHandler("Strategy", _ => CollectorExecutionResult.Success()),
            new FakeStore(), sink).ExecuteAsync(Prepared(
                new FakeHandler("Strategy", _ => CollectorExecutionResult.Success()), Input()),
            CancellationToken.None);
        Assert.Equal(ExecutionRunStatus.Completed, result.Run.Status);
        Assert.Equal(RuntimeFailureCategory.EventPublicationFailure,
            result.EventFailureCategory);
    }

    private static CollectorRuntime Runtime(ICollectorPlugin handler,
        IExecutionStateStore store, IExecutionEventSink sink,
        IExecutionPolicyCatalog? policy = null) =>
        new(store, sink, TimeProvider.System);

    private static PreparedExecutionDispatch Prepared(ICollectorPlugin handler,
        CollectorRuntimeInput input, IExecutionPolicyCatalog? catalog = null)
    {
        CollectorRuntimeStep step = Assert.Single(input.Steps);
        CollectorPluginDescriptor descriptor = handler.Describe();
        ExecutionPolicy policy = (catalog ?? new ExecutionPolicyCatalog()).Resolve(step);
        Guid runId = Guid.NewGuid();
        var context = new RuntimeExecutionContext(input.ManagedServerId, input.TargetFqdn,
            input.ExecutionPlanId, runId, step.ExecutionPlanStepId, step.StrategyCode,
            step.StrategyVersion, descriptor.PluginId, descriptor.PluginVersion,
            CollectorPluginSubject.ManagedTargetServer,
            input.SourceDecisionPlanId, input.SourceCapabilitySnapshotId,
            input.SourceInventoryRunId, input.SourceInventoryVersion,
            input.ExecutionPlanSchemaVersion, policy.PolicySchemaVersion,
            descriptor.DescriptorSchemaVersion, ExecutionEventSchemaVersion.Value,
            TimeProvider.System);
        return new(runId, input, Array.AsReadOnly([
            new PreparedExecutionStep(step, handler, descriptor, policy, context)]));
    }

    private static CollectorRuntimeInput Input(bool withExclusion = false) =>
        new(Guid.NewGuid(), "target.example.test", Guid.NewGuid(), 1,
            withExclusion ? ExecutionPlanStatus.PartiallyReady : ExecutionPlanStatus.Ready,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            DateTime.Today, DateTime.Today, "test",
            Array.AsReadOnly([Step()]),
            Array.AsReadOnly(withExclusion
                ? [new CollectorRuntimeExclusion("Excluded",
                    PlanningDisposition.SkippedBlocked, "Blocked")]
                : Array.Empty<CollectorRuntimeExclusion>()));

    private static CollectorRuntimeStep Step(string timeout = ExecutionPolicyCodes.ShortReadOnly,
        int timeoutVersion = 1) =>
        new(Guid.NewGuid(), "Strategy", 1, DecisionSubject.ManagedTargetServer,
            1, 100, 100, ExecutionPolicyCodes.SerialCore, timeout, timeoutVersion,
            ExecutionPolicyCodes.NoRetry, 1, ThrottlingClass.Lightweight, null,
            true, false, Array.AsReadOnly(Array.Empty<string>()));

    private sealed class FakeHandler : ICollectorPlugin
    {
        private readonly string strategy;
        private readonly Func<CancellationToken, Task<CollectorExecutionResult>> execute;
        private readonly bool readOnly;
        public FakeHandler(string strategy,
            Func<CancellationToken, CollectorExecutionResult> execute, bool readOnly = true)
            : this(strategy, token => Task.FromResult(execute(token)), readOnly) { }
        public FakeHandler(string strategy,
            Func<CancellationToken, Task<CollectorExecutionResult>> execute, bool readOnly = true)
        {
            this.strategy = strategy; this.execute = execute; this.readOnly = readOnly;
        }
        public CollectorPluginDescriptor Describe() =>
            new(strategy, strategy, strategy, "Test plugin", 1, 1,
                CollectorPluginSdkVersion.Current, CollectorPluginSdkVersion.Current,
                Array.AsReadOnly([CollectorPluginSubject.ManagedTargetServer]), readOnly,
                CollectorEstimatedCost.Lightweight,
                Array.AsReadOnly(Array.Empty<string>()), true, true, true, true, false,
                Array.AsReadOnly([1]));
        public CollectorPluginValidationResult Validate(CollectorPluginValidationContext context) =>
            Valid(context);
        public Task<CollectorExecutionResult> ExecuteAsync(
            RuntimeExecutionContext context, ExecutionPolicy policy,
            CancellationToken cancellationToken) =>
            execute(cancellationToken);

        private CollectorPluginValidationResult Valid(CollectorPluginValidationContext context)
        {
            PluginCompatibilityResult compatibility = new RuntimePluginCompatibilityMatrix()
                .Evaluate(context.RuntimeVersion, Describe());
            return new(PluginValidationStatus.Valid, "PluginValid",
                "The plugin validation succeeded.", Array.AsReadOnly(Array.Empty<ExecutionWarning>()),
                Array.AsReadOnly(Array.Empty<PluginValidationIssue>()), compatibility);
        }
    }

    private sealed class FakeStore : IExecutionStateStore
    {
        public ExecutionRunState? Last { get; private set; }
        public int SaveCount { get; private set; }
        public Task CreateAsync(ExecutionRunState run, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Last = run; return Task.CompletedTask;
        }
        public Task SaveAsync(ExecutionRunState run, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Last = run; SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventSink : IExecutionEventSink
    {
        private readonly List<ExecutionEvent> events = [];
        public IReadOnlyList<ExecutionEvent> Events { get { lock (events) return events.ToArray(); } }
        public bool Throw { get; init; }
        public Task PublishAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Throw) throw new InvalidOperationException();
            lock (events) events.Add(executionEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class TestPolicy(TimeSpan timeout, int maxAttempts) : IExecutionPolicyCatalog
    {
        public ExecutionPolicy Resolve(CollectorRuntimeStep step) =>
            new(1, new("TestTimeout", 1, timeout),
                new("TestRetry", 1, maxAttempts,
                    new HashSet<string>(StringComparer.Ordinal)
                    {
                        RuntimeFailureCategory.Timeout.ToString(),
                        RuntimeFailureCategory.HandlerExecutionFailure.ToString()
                    }, Array.AsReadOnly([TimeSpan.Zero])),
                new(ExecutionPolicyCodes.SerialCore, 1, 1),
                new(ThrottlingClass.Lightweight.ToString(), 1, 1),
                new("NoBatch", 1, false));
    }

    private sealed class StubRuntime : ICollectorRuntime
    {
        public bool Invoked { get; private set; }
        public Task<CollectorRuntimeResult> ExecuteAsync(
            PreparedExecutionDispatch dispatch, CancellationToken cancellationToken)
        {
            Invoked = true;
            throw new InvalidOperationException("Not expected.");
        }
    }
}
