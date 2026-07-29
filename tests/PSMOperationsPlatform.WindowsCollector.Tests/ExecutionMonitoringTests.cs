using System.Diagnostics;
using System.Diagnostics.Metrics;
using PSMOperationsPlatform.Application.Runtime;
using PSMOperationsPlatform.CollectorSdk;
using PSMOperationsPlatform.HelloCollector;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class ExecutionMonitoringTests
{
    [Fact]
    public void Compatibility_badge_is_deterministic_and_never_promotes_unknown()
    {
        var generator = new SdkCompatibilityBadgeGenerator(
            new RuntimePluginCompatibilityMatrix());
        CollectorPluginDescriptor descriptor = new HelloCollectorPlugin().Describe();
        SdkCompatibilityBadge first = generator.Generate("1.0", descriptor);
        SdkCompatibilityBadge second = generator.Generate("1.0", descriptor);
        Assert.Equal(first, second);
        Assert.Equal(PluginCompatibilityStatus.Compatible, first.CompatibilityStatus);
        Assert.Equal("Compatible with PSM Runtime 1.0", first.Label);
        Assert.Equal(first.ToMarkdown(), second.ToMarkdown());

        SdkCompatibilityBadge unknown = generator.Generate("", descriptor);
        Assert.Equal(PluginCompatibilityStatus.Unknown, unknown.CompatibilityStatus);
        Assert.DoesNotContain("Compatible with", unknown.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void Certification_is_advisory_and_package_metadata_is_safe_and_deterministic()
    {
        PluginCertificationMetadata certification =
            PluginCertificationMetadata.Experimental.Validate();
        Assert.Equal(PluginCertificationStatus.Experimental, certification.Status);
        Assert.Equal(certification,
            new HelloCollectorPlugin().Describe().Certification);

        var metadata = new PluginPackageMetadata(1, " Author ", "Company", "MIT",
            "docs/support.md", "samples/plugin", "docs/sdk/plugin.md");
        Assert.Equal(metadata.Normalize().ToDeterministicJson(),
            metadata.Normalize().ToDeterministicJson());
        Assert.Throws<ArgumentException>(() =>
            (metadata with { SupportReference = "file:///c:/secret" }).Normalize());
        Assert.Throws<ArgumentException>(() =>
            (metadata with { RepositoryReference = "https://x?token=secret" }).Normalize());
    }

    [Fact]
    public async Task All_samples_are_unique_read_only_deterministic_and_cancellation_aware()
    {
        ICollectorPlugin[] samples =
        [
            new HelloCollectorPlugin(), new NoDataCollectorPlugin(),
            new FailureCollectorPlugin(), new LongRunningCollectorPlugin(),
            new CancellationCollectorPlugin()
        ];
        Assert.Equal(samples.Length,
            samples.Select(x => x.Describe().PluginId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(samples.Length,
            samples.Select(x => x.Describe().StrategyCode)
                .Distinct(StringComparer.Ordinal).Count());
        Assert.All(samples, sample =>
        {
            CollectorPluginDescriptor descriptor = sample.Describe().Normalize();
            Assert.True(descriptor.IsReadOnly);
            Assert.True(descriptor.SupportsCancellation);
            Assert.Equal(PluginCertificationStatus.Experimental,
                descriptor.Certification!.Status);
        });

        var hello = new HelloCollectorPlugin();
        CollectorExecutionResult helloFirst = await hello.ExecuteAsync(Context(),
            Policy(), CancellationToken.None);
        CollectorExecutionResult helloSecond = await hello.ExecuteAsync(Context(),
            Policy(), CancellationToken.None);
        Assert.Equal(helloFirst.Outcome, helloSecond.Outcome);
        Assert.Equal(helloFirst.ReasonCode, helloSecond.ReasonCode);
        Assert.Equal(helloFirst.Artifacts.Objects.Select(x => x.ArtifactId),
            helloSecond.Artifacts.Objects.Select(x => x.ArtifactId));
        Assert.Equal(CollectorExecutionOutcome.NoData,
            (await new NoDataCollectorPlugin().ExecuteAsync(
                Context(), Policy(), CancellationToken.None)).Outcome);
        Assert.Equal(CollectorExecutionOutcome.Failed,
            (await new FailureCollectorPlugin().ExecuteAsync(
                Context(), Policy(), CancellationToken.None)).Outcome);

        foreach (ICollectorPlugin sample in samples.Skip(3))
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sample.ExecuteAsync(Context(), Policy(), cancellation.Token));
        }
    }

    [Fact]
    public async Task Subscriber_records_bounded_metrics_and_safe_activities()
    {
        var measurements = new List<(string Name, long Value, string[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ExecutionMetricCatalog.InstrumentationName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
            measurements.Add((instrument.Name, value,
                tags.ToArray().Select(x => x.Key).ToArray())));
        listener.Start();
        var activities = new List<Activity>();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == ExecutionMetricCatalog.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = activity => activities.Add(activity)
        };
        ActivitySource.AddActivityListener(activityListener);

        using var monitoring = new ExecutionMonitoringSubscriber(TimeProvider.System);
        Guid runId = Guid.NewGuid();
        await monitoring.PublishAsync(Event(ExecutionEventType.ExecutionRunStarted, runId, 1),
            CancellationToken.None);
        await monitoring.PublishAsync(Event(ExecutionEventType.ExecutionStepStarted, runId, 2),
            CancellationToken.None);
        await monitoring.PublishAsync(Event(ExecutionEventType.ExecutionStepAttemptStarted,
            runId, 3), CancellationToken.None);
        await monitoring.PublishAsync(Event(ExecutionEventType.ExecutionStepAttemptCompleted,
            runId, 4, TimeSpan.FromSeconds(2)), CancellationToken.None);
        await monitoring.PublishAsync(Event(ExecutionEventType.ExecutionStepCompleted, runId, 5),
            CancellationToken.None);
        await monitoring.PublishAsync(Event(ExecutionEventType.ExecutionRunCompleted, runId, 6),
            CancellationToken.None);

        Assert.Contains(measurements, x => x.Name == "psm.execution.runs.started");
        Assert.Contains(measurements, x => x.Name == "psm.execution.steps.completed");
        Assert.All(measurements.SelectMany(x => x.Tags), tag =>
            Assert.Contains(tag, ExecutionMetricCatalog.AllowedTagNames));
        Assert.DoesNotContain(measurements.SelectMany(x => x.Tags),
            x => x is "ManagedServerId" or "ExecutionRunId" or "ExecutionPlanId"
                or "TargetFqdn");
        Assert.Contains(activities, x => x.OperationName == "execution.run");
        Assert.Contains(activities, x => x.OperationName == "execution.step");
        Assert.Contains(activities, x => x.OperationName == "execution.attempt");
        Assert.Equal(ExecutionMonitoringStatus.Healthy, monitoring.Snapshot.Status);
        Assert.Equal(0, monitoring.Snapshot.ActiveRunCount);
        Assert.Equal(0, monitoring.Snapshot.ActiveStepCount);
    }

    [Fact]
    public async Task Subscriber_handles_schema_duplicates_order_and_health_without_throwing()
    {
        using var monitoring = new ExecutionMonitoringSubscriber(TimeProvider.System);
        Guid run = Guid.NewGuid();
        ExecutionEvent unsupported = Event(ExecutionEventType.ExecutionRunStarted, run, 1)
            with { EventSchemaVersion = 99 };
        await monitoring.PublishAsync(unsupported, CancellationToken.None);
        Assert.Contains(monitoring.Snapshot.Diagnostics,
            x => x.Category == MonitoringFailureCategory.MonitoringSchemaUnsupported);

        ExecutionEvent failure = Event(ExecutionEventType.ExecutionRunFailed, run, 2);
        await monitoring.PublishAsync(failure, CancellationToken.None);
        await monitoring.PublishAsync(failure, CancellationToken.None);
        await monitoring.PublishAsync(Event(ExecutionEventType.ExecutionRunFailed,
            Guid.NewGuid(), 1), CancellationToken.None);
        await monitoring.PublishAsync(Event(ExecutionEventType.ExecutionRunFailed,
            Guid.NewGuid(), 1), CancellationToken.None);

        ExecutionMonitoringSnapshot snapshot = monitoring.Snapshot;
        Assert.Equal(ExecutionMonitoringStatus.Degraded, snapshot.Status);
        Assert.Contains(MonitoringAlertSignal.RepeatedRuntimeFailure,
            snapshot.AlertSignals);
        Assert.Contains(snapshot.Diagnostics,
            x => x.Category == MonitoringFailureCategory.DuplicateEventObserved);
        Assert.True(snapshot.RecentFailureCount <= 256);
    }

    [Fact]
    public async Task Composite_sink_isolates_subscribers_and_propagates_cancellation()
    {
        var failing = new StubSubscriber(throws: true);
        var succeeding = new StubSubscriber(throws: false);
        var composite = new PSMOperationsPlatform.Infrastructure.Persistence
            .CompositeExecutionEventSink([failing, succeeding]);
        await composite.PublishAsync(Event(ExecutionEventType.ExecutionRunStarted,
            Guid.NewGuid(), 1), CancellationToken.None);
        Assert.Equal(1, failing.Count);
        Assert.Equal(1, succeeding.Count);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            composite.PublishAsync(Event(ExecutionEventType.ExecutionRunStarted,
                Guid.NewGuid(), 1), cancellation.Token));
    }

    [Fact]
    public void Monitoring_contract_has_no_infrastructure_or_service_locator_inputs()
    {
        Type type = typeof(ExecutionMonitoringSubscriber);
        string[] parameterTypes = type.GetConstructors().SelectMany(x => x.GetParameters())
            .Select(x => x.ParameterType.FullName ?? "").ToArray();
        Assert.DoesNotContain(parameterTypes, x =>
            x.Contains("DbContext", StringComparison.Ordinal)
            || x.Contains("IServiceProvider", StringComparison.Ordinal)
            || x.Contains("IConfiguration", StringComparison.Ordinal));
        Assert.Equal(1, ExecutionMonitoringSchemaVersion.Value);
        Assert.DoesNotContain("ManagedServerId", ExecutionMetricCatalog.AllowedTagNames);
        Assert.DoesNotContain("ExecutionRunId", ExecutionMetricCatalog.AllowedTagNames);
        Assert.DoesNotContain("ExecutionPlanId", ExecutionMetricCatalog.AllowedTagNames);
        Assert.DoesNotContain("TargetFqdn", ExecutionMetricCatalog.AllowedTagNames);
    }

    private static ExecutionEvent Event(
        ExecutionEventType type, Guid runId, long sequence, TimeSpan? duration = null) =>
        new(Guid.NewGuid(), ExecutionEventSchemaVersion.Value, sequence, type,
            Guid.NewGuid(), Guid.NewGuid(), runId, Guid.NewGuid(), "SampleStrategy",
            "psm.sample", 1, type is ExecutionEventType.ExecutionStepAttemptStarted
                or ExecutionEventType.ExecutionStepAttemptCompleted ? 1 : null,
            DateTime.Now.AddMilliseconds(sequence), duration, "Completed",
            RuntimeFailureCategory.None, type.ToString(), "Safe event.",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);

    private static PSMOperationsPlatform.CollectorSdk.ExecutionContext Context() =>
        new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SampleStrategy", 1, "psm.sample", 1,
            CollectorPluginSubject.ManagedTargetServer, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, 1, 1, 1, 1, TimeProvider.System);

    private static ExecutionPolicy Policy() =>
        new(1, new("ShortReadOnly", 1, TimeSpan.FromMinutes(1)),
            new("NoRetry", 1, 1, new HashSet<string>(StringComparer.Ordinal),
                Array.AsReadOnly(Array.Empty<TimeSpan>())),
            new("SerialCore", 1, 1), new("Lightweight", 1, 1),
            new("NoBatch", 1, false));

    private sealed class StubSubscriber(bool throws)
        : PSMOperationsPlatform.Infrastructure.Persistence.IExecutionEventSubscriber
    {
        public int Count { get; private set; }
        public Task PublishAsync(
            ExecutionEvent executionEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Count++;
            if (throws) throw new InvalidOperationException("Test-only.");
            return Task.CompletedTask;
        }
    }
}
