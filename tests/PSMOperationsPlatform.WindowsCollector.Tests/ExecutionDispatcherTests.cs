using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.Application.Runtime;
using PSMOperationsPlatform.CollectorSdk;
using RuntimeExecutionContext = PSMOperationsPlatform.CollectorSdk.ExecutionContext;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class ExecutionDispatcherTests
{
    [Fact]
    public async Task Valid_request_is_prepared_and_submitted()
    {
        var runtime = new CapturingRuntime();
        var events = new CapturingEvents();
        ExecutionDispatchResult result = await Dispatcher(Handler(), runtime, events)
            .DispatchAsync(new(Input()), CancellationToken.None);

        Assert.Equal(ExecutionDispatchDisposition.Submitted, result.Disposition);
        PreparedExecutionDispatch dispatch = Assert.IsType<PreparedExecutionDispatch>(runtime.Dispatch);
        PreparedExecutionStep prepared = Assert.Single(dispatch.Steps);
        Assert.Equal("Strategy", prepared.Descriptor.StrategyCode);
        Assert.Equal(1, prepared.Policy.PolicySchemaVersion);
        Assert.Equal(dispatch.ExecutionRunId, prepared.Context.ExecutionRunId);
        Assert.Contains(events.Items, x => x.EventType == ExecutionEventType.ExecutionDispatchPrepared);
        Assert.Contains(events.Items, x => x.EventType == ExecutionEventType.ExecutionDispatchSubmitted);
    }

    [Fact]
    public async Task Missing_handler_rejects_without_runtime()
    {
        var runtime = new CapturingRuntime();
        var dispatcher = new ExecutionDispatcher(
            new CollectorPluginRegistry([], new RuntimePluginCompatibilityMatrix(), "1.0"),
            new ExecutionPolicyCatalog(), new RuntimePluginCompatibilityMatrix(),
            new PluginPolicyCompatibilityValidator(), runtime, new CapturingEvents(),
            TimeProvider.System);
        ExecutionDispatchResult result = await dispatcher.DispatchAsync(new(Input()), default);
        Assert.Equal(DispatchFailureCategory.HandlerNotFound, result.FailureCategory);
        Assert.Null(runtime.Dispatch);
    }

    [Theory]
    [InlineData(true, false, true, true, false, DispatchFailureCategory.RetryCapabilityUnsupported)]
    [InlineData(true, true, false, true, false, DispatchFailureCategory.TimeoutCapabilityUnsupported)]
    public async Task Capability_mismatch_rejects_without_silent_downgrade(
        bool cancellation, bool retry, bool timeout, bool parallel, bool batch,
        DispatchFailureCategory expected)
    {
        var runtime = new CapturingRuntime();
        ExecutionDispatchResult result = await Dispatcher(
            Handler(cancellation, retry, timeout, parallel, batch), runtime)
            .DispatchAsync(new(Input(retry: true)), default);
        Assert.Equal(expected, result.FailureCategory);
        Assert.Null(runtime.Dispatch);
    }

    [Fact]
    public async Task Parallel_capability_is_required_for_parallel_policy()
    {
        var runtime = new CapturingRuntime();
        ExecutionDispatchResult result = await Dispatcher(
            Handler(parallel: false), runtime)
            .DispatchAsync(new(Input(parallel: true)), default);
        Assert.Equal(DispatchFailureCategory.ParallelCapabilityUnsupported,
            result.FailureCategory);
    }

    [Fact]
    public async Task Cancellation_before_dispatch_propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Dispatcher(Handler(), new CapturingRuntime())
                .DispatchAsync(new(Input()), cancellation.Token));
    }

    [Theory]
    [InlineData("ManagedServer")]
    [InlineData("ExecutionPlan")]
    [InlineData("DecisionPlan")]
    [InlineData("CapabilitySnapshot")]
    [InlineData("InventoryRun")]
    public async Task Missing_required_identity_rejects_request(string missing)
    {
        CollectorRuntimeInput input = Input();
        input = missing switch
        {
            "ManagedServer" => input with { ManagedServerId = Guid.Empty },
            "ExecutionPlan" => input with { ExecutionPlanId = Guid.Empty },
            "DecisionPlan" => input with { SourceDecisionPlanId = Guid.Empty },
            "CapabilitySnapshot" => input with { SourceCapabilitySnapshotId = Guid.Empty },
            _ => input with { SourceInventoryRunId = Guid.Empty }
        };
        var runtime = new CapturingRuntime();
        ExecutionDispatchResult result = await Dispatcher(Handler(), runtime)
            .DispatchAsync(new(input), default);
        Assert.Equal(DispatchFailureCategory.DispatchRequestInvalid, result.FailureCategory);
        Assert.Null(runtime.Dispatch);
    }

    [Fact]
    public async Task Unknown_policy_rejects_before_runtime()
    {
        CollectorRuntimeInput input = Input();
        input = input with
        {
            Steps = Array.AsReadOnly([
                input.Steps[0] with { TimeoutPolicyCode = "UnknownPolicy" }])
        };
        var runtime = new CapturingRuntime();
        ExecutionDispatchResult result = await Dispatcher(Handler(), runtime)
            .DispatchAsync(new(input), default);
        Assert.Equal(DispatchFailureCategory.ExecutionPolicyNotFound, result.FailureCategory);
        Assert.Null(runtime.Dispatch);
    }

    [Fact]
    public async Task Incompatible_sdk_is_explained_and_never_reaches_runtime()
    {
        ICollectorPlugin plugin = new FakeHandler(Handler().Describe() with
        {
            MinimumSupportedSdkVersion = new CollectorPluginSdkVersion(2, 0),
            TargetSdkVersion = new CollectorPluginSdkVersion(2, 0)
        });
        var runtime = new CapturingRuntime();
        var dispatcher = new ExecutionDispatcher(new FixedRegistry(plugin),
            new ExecutionPolicyCatalog(), new RuntimePluginCompatibilityMatrix(),
            new PluginPolicyCompatibilityValidator(), runtime, new CapturingEvents(),
            TimeProvider.System);
        ExecutionDispatchResult result = await dispatcher.DispatchAsync(new(Input()), default);
        Assert.Equal(DispatchFailureCategory.PluginSdkVersionUnsupported,
            result.FailureCategory);
        Assert.Equal("PluginSdkVersionIncompatible", result.ReasonCode);
        Assert.Equal("2.0", result.Diagnostic.TargetSdkVersion);
        Assert.Equal("1.0", result.Diagnostic.RuntimeVersion);
        Assert.Contains("SdkCompatibility", result.Diagnostic.FailedChecks);
        Assert.Null(runtime.Dispatch);
    }

    [Fact]
    public void Registry_uses_ordinal_codes_and_rejects_duplicate_registration()
    {
        var handler = Handler();
        var registry = new CollectorPluginRegistry([handler],
            new RuntimePluginCompatibilityMatrix(), "1.0");
        Assert.True(registry.TryResolve("Strategy", out _));
        Assert.False(registry.TryResolve("strategy", out _));
        Assert.Throws<ArgumentException>(() =>
            new CollectorPluginRegistry([handler, handler],
                new RuntimePluginCompatibilityMatrix(), "1.0"));
    }

    [Fact]
    public void Artifacts_are_bounded_sorted_and_validate_identity_and_counts()
    {
        ExecutionArtifacts artifacts = ExecutionArtifacts.Create(
            [new("b", "b.log", "text/plain", 2, null),
                new("a", "a.log", "text/plain", 1, null)],
            [new("c", "Type", "key", 0)], [], []);
        Assert.Equal(["a", "b"], artifacts.Files.Select(x => x.ArtifactId));
        Assert.Equal(ExecutionArtifactSchemaVersion.Value, artifacts.ArtifactSchemaVersion);
        Assert.Throws<ArgumentException>(() => ExecutionArtifacts.Create(
            [new("a", "a", "text/plain", -1, null)], [], [], []));
        Assert.Throws<ArgumentException>(() => ExecutionArtifacts.Create(
            [new("a", "a", "text/plain", 1, null)],
            [new("a", "T", "k", 1)], [], []));
    }

    [Fact]
    public void Context_and_prepared_contracts_expose_no_runtime_service_locator()
    {
        string[] forbidden = ["DbContext", "IServiceProvider", "IConfiguration",
            "Credentials", "ConnectionString", "CancellationToken"];
        string[] properties = typeof(RuntimeExecutionContext).GetProperties()
            .Select(x => x.Name).ToArray();
        Assert.DoesNotContain(properties, x => forbidden.Contains(x, StringComparer.Ordinal));
        Assert.True(typeof(PreparedExecutionDispatch).IsSealed);
        Assert.True(typeof(ExecutionPolicy).IsSealed);
    }

    [Fact]
    public void Event_schema_is_independent_and_positive()
    {
        Assert.Equal(1, ExecutionEventSchemaVersion.Value);
        Assert.NotEqual(CollectorRuntimeVersions.ExecutionStateSchemaVersion + 1,
            ExecutionEventSchemaVersion.Value);
    }

    [Fact]
    public void Runtime_has_no_registry_or_policy_catalog_constructor_dependency()
    {
        Type[] dependencies = typeof(CollectorRuntime).GetConstructors().Single()
            .GetParameters().Select(x => x.ParameterType).ToArray();
        Assert.DoesNotContain(typeof(ICollectorPluginRegistry), dependencies);
        Assert.DoesNotContain(typeof(IExecutionPolicyCatalog), dependencies);
    }

    private static ExecutionDispatcher Dispatcher(ICollectorPlugin handler,
        CapturingRuntime runtime, CapturingEvents? events = null) =>
        new(new CollectorPluginRegistry([handler], new RuntimePluginCompatibilityMatrix(), "1.0"),
            new ExecutionPolicyCatalog(), new RuntimePluginCompatibilityMatrix(),
            new PluginPolicyCompatibilityValidator(), runtime, events ?? new CapturingEvents(),
            TimeProvider.System);

    private static ICollectorPlugin Handler(bool cancellation = true,
        bool retry = true, bool timeout = true, bool parallel = true, bool batch = false) =>
        new FakeHandler(new("plugin.test", "Strategy", "Test handler", "Test plugin", 1, 1,
            CollectorPluginSdkVersion.Current, CollectorPluginSdkVersion.Current,
            Array.AsReadOnly([CollectorPluginSubject.ManagedTargetServer]), true,
            CollectorEstimatedCost.Lightweight, Array.AsReadOnly(Array.Empty<string>()),
            cancellation, retry, timeout, parallel, batch,
            Array.AsReadOnly([CollectorPluginContractVersions.ArtifactSchemaVersion])));

    private static CollectorRuntimeInput Input(bool retry = false, bool parallel = false)
    {
        var step = new CollectorRuntimeStep(Guid.NewGuid(), "Strategy", 1,
            DecisionSubject.ManagedTargetServer, 1, 100, 100,
            parallel ? ExecutionPolicyCodes.ParallelReadOnlyA : ExecutionPolicyCodes.SerialCore,
            ExecutionPolicyCodes.ShortReadOnly, 1,
            retry ? ExecutionPolicyCodes.StandardReadOnlyRetry : ExecutionPolicyCodes.NoRetry,
            1, ThrottlingClass.Lightweight, null, true, false,
            Array.AsReadOnly(Array.Empty<string>()));
        return new(Guid.NewGuid(), "target.example.test", Guid.NewGuid(), 1,
            ExecutionPlanStatus.Ready, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            DateTime.Today, DateTime.Today, "test", Array.AsReadOnly([step]),
            Array.AsReadOnly(Array.Empty<CollectorRuntimeExclusion>()));
    }

    private sealed class FakeHandler(CollectorPluginDescriptor descriptor)
        : ICollectorPlugin
    {
        public CollectorPluginDescriptor Describe() => descriptor;
        public CollectorPluginValidationResult Validate(CollectorPluginValidationContext context)
        {
            PluginCompatibilityResult compatibility = new RuntimePluginCompatibilityMatrix()
                .Evaluate(context.RuntimeVersion, descriptor);
            return new(PluginValidationStatus.Valid, "PluginValid",
                "The plugin validation succeeded.", Array.AsReadOnly(Array.Empty<ExecutionWarning>()),
                Array.AsReadOnly(Array.Empty<PluginValidationIssue>()), compatibility);
        }
        public Task<CollectorExecutionResult> ExecuteAsync(
            RuntimeExecutionContext context, ExecutionPolicy policy,
            CancellationToken cancellationToken) =>
            Task.FromResult(CollectorExecutionResult.Success());
    }

    private sealed class CapturingRuntime : ICollectorRuntime
    {
        public PreparedExecutionDispatch? Dispatch { get; private set; }
        public Task<CollectorRuntimeResult> ExecuteAsync(
            PreparedExecutionDispatch dispatch, CancellationToken cancellationToken)
        {
            Dispatch = dispatch;
            var run = new ExecutionRunState(dispatch.ExecutionRunId, dispatch.Plan,
                DateTime.Today);
            return Task.FromResult(new CollectorRuntimeResult(run, RuntimeFailureCategory.None));
        }
    }

    private sealed class CapturingEvents : IExecutionEventSink
    {
        public List<ExecutionEvent> Items { get; } = [];
        public Task PublishAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken)
        {
            Items.Add(executionEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedRegistry(ICollectorPlugin plugin) : ICollectorPluginRegistry
    {
        public IReadOnlyList<CollectorPluginDescriptor> Descriptors =>
            Array.AsReadOnly([plugin.Describe()]);
        public bool TryResolve(string strategyCode, out ICollectorPlugin? value)
        {
            value = string.Equals(strategyCode, plugin.Describe().StrategyCode,
                StringComparison.Ordinal) ? plugin : null;
            return value is not null;
        }
    }
}
