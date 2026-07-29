using System.Collections.Concurrent;
using System.Diagnostics;
using PSMOperationsPlatform.Application.Runtime;
using PSMOperationsPlatform.CollectorSdk;
using PSMOperationsPlatform.HelloCollector;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class ExecutionMonitoringQualityTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Snapshot_is_versioned_immutable_bounded_and_deterministic()
    {
        using var first = new ExecutionMonitoringSubscriber(new FixedTimeProvider(FixedNow));
        using var second = new ExecutionMonitoringSubscriber(new FixedTimeProvider(FixedNow));
        ExecutionEvent[] events =
        [
            Event(ExecutionEventType.ExecutionRunStarted, 1),
            Event(ExecutionEventType.ExecutionStepRetryScheduled, 2),
            Event(ExecutionEventType.ExecutionRunCompleted, 3)
        ];
        foreach (ExecutionEvent value in events)
        {
            await first.PublishAsync(value, CancellationToken.None);
            await second.PublishAsync(value, CancellationToken.None);
        }

        ExecutionMonitoringSnapshot a = first.GetCurrentSnapshot();
        ExecutionMonitoringSnapshot b = second.GetCurrentSnapshot();
        Assert.Equal(1, a.MonitoringSchemaVersion);
        Assert.Equal(1, a.SnapshotSchemaVersion);
        Assert.Equal(FixedNow.DateTime, a.GeneratedAt);
        Assert.Equal(ExecutionMetricCatalog.InstrumentationName, a.InstrumentationName);
        Assert.Equal(ExecutionMonitoringStatus.Healthy, a.Status);
        Assert.Equal(100, a.HealthScore);
        Assert.Equal(MonitoringHealthRating.Healthy, a.HealthRating);
        Assert.Equal(1, a.RecentSuccessfulRunCount);
        Assert.Equal(1, a.RecentRetryCount);
        Assert.Equal(a.GeneratedAt, b.GeneratedAt);
        Assert.Equal(a.HealthScore, b.HealthScore);
        Assert.Equal(a.ReasonCodes, b.ReasonCodes);
        Assert.Equal(a.WarningCodes, b.WarningCodes);
        Assert.True(a.Diagnostics.Count <= 32);
        Assert.True(a.WarningCodes.Count <= 32);
        Assert.DoesNotContain(typeof(ExecutionMonitoringSnapshot).GetProperties(),
            x => x.PropertyType == typeof(ExecutionEvent)
                || x.PropertyType == typeof(ExecutionArtifacts));
    }

    [Fact]
    public async Task Snapshot_provider_is_read_only_and_safe_for_concurrent_readers()
    {
        using var monitoring = new ExecutionMonitoringSubscriber(
            new FixedTimeProvider(FixedNow));
        await monitoring.PublishAsync(Event(ExecutionEventType.ExecutionRunStarted, 1),
            CancellationToken.None);
        IExecutionMonitoringSnapshotProvider provider = monitoring;
        var snapshots = new ConcurrentBag<ExecutionMonitoringSnapshot>();
        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
            snapshots.Add(provider.GetCurrentSnapshot()))));
        Assert.Equal(64, snapshots.Count);
        Assert.All(snapshots, x => Assert.Equal(1, x.ActiveRunCount));
        Assert.DoesNotContain(typeof(IExecutionMonitoringSnapshotProvider).GetMethods(),
            x => x.Name.StartsWith("Set", StringComparison.Ordinal)
                || x.Name.StartsWith("Update", StringComparison.Ordinal)
                || x.Name.StartsWith("Cancel", StringComparison.Ordinal));
    }

    [Fact]
    public void Health_assessment_is_explicit_bounded_and_handles_unknown()
    {
        var calculator = new MonitoringHealthAssessmentCalculator(
            new FixedTimeProvider(FixedNow));
        MonitoringHealthAssessment unknown = calculator.Evaluate(new(false,
            true, true, true, 0, 0, 0, []));
        Assert.Null(unknown.Score);
        Assert.Equal(MonitoringHealthRating.Unknown, unknown.Rating);

        MonitoringHealthAssessment healthy = calculator.Evaluate(new(true,
            true, true, true, 0, 0, 0, []));
        Assert.Equal(100, healthy.Score);
        Assert.Equal(MonitoringHealthRating.Healthy, healthy.Rating);
        Assert.Equal(100, healthy.DimensionResults.Sum(x => x.MaximumPoints));

        MonitoringHealthAssessment degraded = calculator.Evaluate(new(true,
            true, true, true, 5, 5, 3, []));
        Assert.InRange(degraded.Score!.Value, 70, 89);
        Assert.Equal(MonitoringHealthRating.Degraded, degraded.Rating);

        MonitoringHealthAssessment unhealthy = calculator.Evaluate(new(true,
            false, false, false, 100, 100, 100, [
                new(MonitoringFailureCategory.MonitoringSchemaUnsupported,
                    "MonitoringSchemaUnsupported", "Safe.")
            ]));
        Assert.InRange(unhealthy.Score!.Value, 0, 69);
        Assert.Equal(MonitoringHealthRating.Unhealthy, unhealthy.Rating);
        Assert.Equal(FixedNow.DateTime, unhealthy.EvaluatedAt);
    }

    [Fact]
    public void Health_penalties_are_deterministic_and_capped()
    {
        var calculator = new MonitoringHealthAssessmentCalculator(
            new FixedTimeProvider(FixedNow));
        MonitoringDiagnostic[] diagnostics =
        [
            new(MonitoringFailureCategory.DuplicateEventObserved,
                "DuplicateEventObserved", "Safe."),
            new(MonitoringFailureCategory.EventSequenceInvalid,
                "EventSequenceInvalid", "Safe.")
        ];
        MonitoringHealthAssessment first = calculator.Evaluate(new(true,
            true, true, true, 1000, 1000, 1000, diagnostics));
        MonitoringHealthAssessment second = calculator.Evaluate(new(true,
            true, true, true, 1000, 1000, 1000, diagnostics));
        Assert.Equal(first.Score, second.Score);
        Assert.InRange(first.Score!.Value, 0, 100);
        Assert.Equal(first.ReasonCodes, second.ReasonCodes);
    }

    [Fact]
    public void Metric_catalog_is_unique_structured_and_matches_documentation()
    {
        ExecutionMetricDefinition[] definitions =
            ExecutionMetricCatalog.Definitions.ToArray();
        Assert.Equal(definitions.Length,
            definitions.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(definitions, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Unit));
            Assert.NotEmpty(definition.AllowedDimensions);
            Assert.NotEmpty(definition.ProhibitedDimensions);
            Assert.Equal("1.0", definition.InstrumentationVersion);
        });
        Assert.DoesNotContain(definitions.SelectMany(x => x.AllowedDimensions),
            x => ExecutionMetricCatalog.ProhibitedTagNames.Contains(x));

        string metricsRoot = Path.Combine(RepositoryRoot(), "docs", "runtime", "metrics");
        string documented = string.Join(Environment.NewLine,
            Directory.GetFiles(metricsRoot, "*.md").Select(File.ReadAllText));
        Assert.All(definitions, definition =>
            Assert.Contains(definition.Name, documented, StringComparison.Ordinal));
        Assert.Equal(definitions.Length, definitions.Select(x => x.Name)
            .Count(name => documented.Contains(name, StringComparison.Ordinal)));
    }

    [Fact]
    public void Plugin_monitoring_readiness_is_explicit_and_separate()
    {
        var evaluator = new PluginMonitoringReadinessEvaluator(
            new RuntimePluginCompatibilityMatrix());
        CollectorPluginDescriptor descriptor = new HelloCollectorPlugin().Describe();
        var full = new PluginMonitoringReadinessEvidence(true, true, true, true, true);
        PluginMonitoringReadinessAssessment ready =
            evaluator.Evaluate("1.0", descriptor, full);
        Assert.Equal(PluginMonitoringReadinessStatus.Ready, ready.Status);

        PluginMonitoringReadinessAssessment partial = evaluator.Evaluate("1.0",
            descriptor, full with { DocumentationReferenceAvailable = false });
        Assert.Equal(PluginMonitoringReadinessStatus.PartiallyReady, partial.Status);
        Assert.Contains("DocumentationEvidenceUnavailable", partial.ReasonCodes);

        PluginMonitoringReadinessAssessment unknown =
            evaluator.Evaluate("", descriptor, full);
        Assert.Equal(PluginMonitoringReadinessStatus.Unknown, unknown.Status);

        PluginMonitoringReadinessAssessment invalid = evaluator.Evaluate("1.0",
            descriptor with { IsReadOnly = false }, full);
        Assert.Equal(PluginMonitoringReadinessStatus.NotReady, invalid.Status);
        Assert.NotEqual(descriptor.Certification?.Status.ToString(),
            ready.Status.ToString());
    }

    [Fact]
    public void Monitoring_readiness_badge_is_deterministic_local_and_timestamp_free()
    {
        var evaluator = new PluginMonitoringReadinessEvaluator(
            new RuntimePluginCompatibilityMatrix());
        PluginMonitoringReadinessAssessment assessment = evaluator.Evaluate("1.0",
            new HelloCollectorPlugin().Describe(),
            new(true, true, true, true, true));
        PluginMonitoringReadinessBadge first = evaluator.GenerateBadge(assessment);
        PluginMonitoringReadinessBadge second = evaluator.GenerateBadge(assessment);
        Assert.Equal(first, second);
        Assert.Equal("Monitoring Ready", first.Label);
        Assert.Equal("`Monitoring Ready`", first.ToMarkdown());
        Assert.DoesNotContain(typeof(PluginMonitoringReadinessBadge).GetProperties(),
            x => x.Name.Contains("Time", StringComparison.Ordinal)
                || x.Name.Contains("Path", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Snapshot_and_assessments_have_bounded_local_performance()
    {
        using var monitoring = new ExecutionMonitoringSubscriber(
            new FixedTimeProvider(FixedNow));
        await monitoring.PublishAsync(Event(ExecutionEventType.ExecutionRunStarted, 1),
            CancellationToken.None);
        var calculator = new MonitoringHealthAssessmentCalculator(
            new FixedTimeProvider(FixedNow));
        var evaluator = new PluginMonitoringReadinessEvaluator(
            new RuntimePluginCompatibilityMatrix());
        CollectorPluginDescriptor descriptor = new HelloCollectorPlugin().Describe();
        var stopwatch = Stopwatch.StartNew();
        for (int index = 0; index < 10_000; index++)
        {
            _ = monitoring.GetCurrentSnapshot();
            _ = calculator.Evaluate(new(true, true, true, true, 0, 0, 0, []));
            _ = evaluator.Evaluate("1.0", descriptor,
                new(true, true, true, true, true));
        }
        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15),
            "Informational bounded-operation guard exceeded.");
        Assert.True(monitoring.Snapshot.Diagnostics.Count <= 32);
    }

    [Fact]
    public void Required_quality_documents_exist_and_preserve_boundaries()
    {
        string root = RepositoryRoot();
        string[] paths =
        [
            "workpackages/WP-008.7.Q.md",
            "docs/runtime/Monitoring-Query-Examples.md",
            "docs/runtime/Monitoring-Performance-Budgets.md",
            "docs/architecture/Execution-History-vs-Audit.md"
        ];
        Assert.All(paths, path => Assert.True(File.Exists(Path.Combine(root, path)), path));
        string boundary = File.ReadAllText(Path.Combine(root,
            "docs/architecture/Execution-History-vs-Audit.md"));
        Assert.Contains("WP-008.8 implements Execution History only", boundary,
            StringComparison.Ordinal);
        Assert.Contains("separately approved work package", boundary,
            StringComparison.Ordinal);
    }

    private static ExecutionEvent Event(ExecutionEventType type, long sequence) =>
        new(Guid.Parse($"00000000-0000-0000-0000-{sequence:D12}"),
            ExecutionEventSchemaVersion.Value, sequence, type,
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            Guid.Parse("30000000-0000-0000-0000-000000000003"),
            Guid.Parse("40000000-0000-0000-0000-000000000004"),
            "sample.strategy", "sample.plugin", 1, null,
            FixedNow.DateTime, TimeSpan.FromMilliseconds(1), "Completed",
            RuntimeFailureCategory.None, type.ToString(), "Safe synthetic event.",
            Guid.Parse("50000000-0000-0000-0000-000000000005"),
            Guid.Parse("60000000-0000-0000-0000-000000000006"),
            Guid.Parse("70000000-0000-0000-0000-000000000007"), 1);

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(
                   current.FullName, "PSMOperationsPlatform.sln")))
            current = current.Parent;
        return current?.FullName
            ?? throw new InvalidOperationException("Repository root unavailable.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
