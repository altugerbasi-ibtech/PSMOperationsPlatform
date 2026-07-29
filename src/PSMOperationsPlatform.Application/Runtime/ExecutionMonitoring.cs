using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace PSMOperationsPlatform.Application.Runtime;

public static class ExecutionMonitoringSchemaVersion { public const int Value = 1; }
public static class ExecutionMonitoringSnapshotSchemaVersion { public const int Value = 1; }
public static class MonitoringHealthAssessmentSchemaVersion { public const int Value = 1; }

public enum ExecutionMonitoringStatus { Healthy = 1, Degraded = 2, Unhealthy = 3, Unknown = 4 }
public enum MonitoringHealthRating { Healthy = 1, Degraded = 2, Unhealthy = 3, Unknown = 4 }
public enum MonitoringFailureCategory
{
    None = 0, MonitoringEventInvalid = 1, MonitoringSchemaUnsupported = 2,
    MetricRecordingFailure = 3, ActivityCreationFailure = 4,
    ActivityCorrelationFailure = 5, HealthProjectionFailure = 6,
    MonitoringSubscriberFailure = 7, MonitoringPipelineUnavailable = 8,
    DuplicateEventObserved = 9, EventSequenceInvalid = 10,
    Cancellation = 11, Unexpected = 12,
    MonitoringSnapshotGenerationFailure = 13,
    MonitoringHealthAssessmentFailure = 14,
    MonitoringReadinessAssessmentFailure = 15,
    MonitoringDocumentationValidationFailure = 16,
    MonitoringPerformanceMeasurementFailure = 17,
    MonitoringInputInvalid = 18
}
public enum MonitoringAlertSignal
{
    RepeatedRuntimeFailure = 1, ExcessiveTimeoutRate = 2,
    ExcessiveDispatchRejectionRate = 3, MonitoringSubscriberFailure = 4
}

public sealed record MonitoringDiagnostic(
    MonitoringFailureCategory Category, string ReasonCode, string Explanation);

public sealed record ExecutionMonitoringSnapshot(
    int MonitoringSchemaVersion,
    int SnapshotSchemaVersion,
    DateTime GeneratedAt,
    string InstrumentationName,
    string InstrumentationVersion,
    ExecutionMonitoringStatus Status,
    int? HealthScore,
    MonitoringHealthRating HealthRating,
    int ActiveRunCount,
    int ActiveStepCount,
    int WaitingStepCount,
    int ThrottledStepCount,
    int RecentSuccessfulRunCount,
    int RecentFailureCount,
    int RecentTimeoutCount,
    int RecentCancellationCount,
    int RecentDispatchRejectionCount,
    int RecentRetryCount,
    int RecentWarningCount,
    DateTime? LastEventAt,
    DateTime? LastSuccessfulRunAt,
    DateTime? LastFailedRunAt,
    DateTime? LastTimeoutAt,
    DateTime? LastCancellationAt,
    DateTime ObservationWindowStart,
    DateTime ObservationWindowEnd,
    bool MonitoringSubscriberHealthy,
    bool MetricPipelineHealthy,
    bool ActivityPipelineHealthy,
    bool EventPipelineHealthy,
    IReadOnlyList<MonitoringAlertSignal> AlertSignals,
    IReadOnlyList<string> WarningCodes,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<MonitoringDiagnostic> Diagnostics)
{
    public int SchemaVersion => MonitoringSchemaVersion;
}

public interface IExecutionMonitoringSnapshotProvider
{
    ExecutionMonitoringSnapshot GetCurrentSnapshot();
}

public interface IExecutionMonitoring : IExecutionMonitoringSnapshotProvider
{
    ExecutionMonitoringSnapshot Snapshot { get; }
}

public sealed record MonitoringHealthDimensionResult(
    string Code, int MaximumPoints, int AwardedPoints, string ReasonCode);

public sealed record MonitoringHealthAssessment(
    int HealthAssessmentSchemaVersion,
    int MonitoringSchemaVersion,
    int? Score,
    int MaximumScore,
    MonitoringHealthRating Rating,
    ExecutionMonitoringStatus Status,
    IReadOnlyList<MonitoringHealthDimensionResult> DimensionResults,
    IReadOnlyList<string> WarningCodes,
    IReadOnlyList<string> ReasonCodes,
    DateTime EvaluatedAt,
    DateTime ObservationWindowStart,
    DateTime ObservationWindowEnd);

public sealed record MonitoringHealthEvidence(
    bool HasObservedEvent,
    bool MonitoringSubscriberHealthy,
    bool MetricPipelineHealthy,
    bool ActivityPipelineHealthy,
    int RecentFailureCount,
    int RecentTimeoutCount,
    int RecentDispatchRejectionCount,
    IReadOnlyList<MonitoringDiagnostic> Diagnostics);

public sealed class MonitoringHealthAssessmentCalculator(TimeProvider timeProvider)
{
    public MonitoringHealthAssessment Evaluate(MonitoringHealthEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        DateTime evaluatedAt = timeProvider.GetLocalNow().DateTime;
        DateTime windowStart = evaluatedAt - TimeSpan.FromMinutes(15);
        if (!evidence.HasObservedEvent)
            return new(MonitoringHealthAssessmentSchemaVersion.Value,
                ExecutionMonitoringSchemaVersion.Value, null, 100,
                MonitoringHealthRating.Unknown, ExecutionMonitoringStatus.Unknown,
                Array.AsReadOnly(Array.Empty<MonitoringHealthDimensionResult>()),
                Array.AsReadOnly(Array.Empty<string>()),
                Array.AsReadOnly(["InsufficientObservationEvidence"]),
                evaluatedAt, windowStart, evaluatedAt);

        MonitoringDiagnostic[] diagnostics = evidence.Diagnostics.TakeLast(32).ToArray();
        int duplicateCount = diagnostics.Count(x =>
            x.Category == MonitoringFailureCategory.DuplicateEventObserved);
        int sequenceCount = diagnostics.Count(x =>
            x.Category == MonitoringFailureCategory.EventSequenceInvalid);
        int schemaCount = diagnostics.Count(x =>
            x.Category is MonitoringFailureCategory.MonitoringSchemaUnsupported
                or MonitoringFailureCategory.MonitoringEventInvalid);
        string[] warningCodes = diagnostics.Select(x => x.ReasonCode)
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal)
            .Take(32).ToArray();
        var dimensions = new[]
        {
            Dimension("MonitoringSubscriberHealth", 25,
                evidence.MonitoringSubscriberHealthy ? 0 : 25,
                evidence.MonitoringSubscriberHealthy ? "SubscriberHealthy" : "SubscriberUnavailable"),
            Dimension("MetricPipelineHealth", 20,
                evidence.MetricPipelineHealthy ? 0 : 20,
                evidence.MetricPipelineHealthy ? "MetricPipelineHealthy" : "MetricPipelineUnavailable"),
            Dimension("ActivityPipelineHealth", 15,
                evidence.ActivityPipelineHealthy ? 0 : 15,
                evidence.ActivityPipelineHealthy ? "ActivityPipelineHealthy" : "ActivityPipelineUnavailable"),
            Dimension("EventSequenceIntegrity", 10,
                Math.Min(10, schemaCount > 0 ? 10 : sequenceCount * 5 + duplicateCount * 2),
                schemaCount > 0 ? "EventSchemaInvalid" : sequenceCount > 0
                    ? "EventSequenceInvalid" : duplicateCount > 0
                        ? "DuplicateEventObserved" : "EventSequenceHealthy"),
            Dimension("RecentExecutionFailurePressure", 10,
                Math.Min(10, evidence.RecentFailureCount * 2),
                evidence.RecentFailureCount > 0 ? "RecentExecutionFailures" : "NoRecentExecutionFailures"),
            Dimension("RecentTimeoutPressure", 10,
                Math.Min(10, evidence.RecentTimeoutCount * 2),
                evidence.RecentTimeoutCount > 0 ? "RecentTimeouts" : "NoRecentTimeouts"),
            Dimension("RecentDispatchRejectionPressure", 5,
                Math.Min(5, evidence.RecentDispatchRejectionCount * 2),
                evidence.RecentDispatchRejectionCount > 0 ? "RecentDispatchRejections" : "NoRecentDispatchRejections"),
            Dimension("MonitoringWarningPressure", 5, Math.Min(5, warningCodes.Length),
                warningCodes.Length > 0 ? "MonitoringWarningsPresent" : "NoMonitoringWarnings")
        };
        int score = Math.Max(0, dimensions.Sum(x => x.AwardedPoints));
        MonitoringHealthRating rating = score >= 90 ? MonitoringHealthRating.Healthy
            : score >= 70 ? MonitoringHealthRating.Degraded
            : MonitoringHealthRating.Unhealthy;
        ExecutionMonitoringStatus status = rating switch
        {
            MonitoringHealthRating.Healthy => ExecutionMonitoringStatus.Healthy,
            MonitoringHealthRating.Degraded => ExecutionMonitoringStatus.Degraded,
            _ => ExecutionMonitoringStatus.Unhealthy
        };
        return new(MonitoringHealthAssessmentSchemaVersion.Value,
            ExecutionMonitoringSchemaVersion.Value, score, 100, rating, status,
            Array.AsReadOnly(dimensions.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(warningCodes),
            Array.AsReadOnly(dimensions.Select(x => x.ReasonCode)
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray()),
            evaluatedAt, windowStart, evaluatedAt);

        static MonitoringHealthDimensionResult Dimension(
            string code, int maximum, int penalty, string reason) =>
            new(code, maximum, Math.Max(0, maximum - penalty), reason);
    }
}

public enum ExecutionMetricInstrumentType { Counter = 1, Histogram = 2, ObservableGauge = 3 }
public sealed record ExecutionMetricDefinition(
    string Name,
    ExecutionMetricInstrumentType InstrumentType,
    string Unit,
    string Description,
    string Source,
    string RecordingCondition,
    IReadOnlyList<string> AllowedDimensions,
    IReadOnlyList<string> ProhibitedDimensions,
    string ExpectedCardinality,
    string DuplicateEventBehavior,
    string NegativeValueBehavior,
    string FailureIsolationBehavior,
    string InstrumentationVersion);

public static class ExecutionMetricCatalog
{
    public const string InstrumentationName = "PSMOperationsPlatform.Execution";
    public const string InstrumentationVersion = "1.0";
    public static readonly IReadOnlySet<string> AllowedTagNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "strategy.code", "plugin.id", "execution.outcome", "failure.category",
            "reason.code", "subject", "sdk.major_version", "runtime.contract_version",
            "certification.status"
        };
    public static readonly IReadOnlyList<string> ProhibitedTagNames = Array.AsReadOnly(
        new[] { "ManagedServerId", "ExecutionRunId", "ExecutionPlanId",
            "ExecutionPlanStepId", "TargetFqdn", "IpAddress", "MachineName",
            "ArtifactId", "ArtifactName", "FileName", "FilePath", "Url",
            "ExceptionMessage", "StackTrace", "UserName" }
        .OrderBy(x => x, StringComparer.Ordinal).ToArray());

    private static readonly string[] CounterNames =
    [
        "psm.execution.runs.started", "psm.execution.runs.completed",
        "psm.execution.runs.failed", "psm.execution.runs.cancelled",
        "psm.execution.steps.started", "psm.execution.steps.completed",
        "psm.execution.steps.failed", "psm.execution.steps.timed_out",
        "psm.execution.steps.cancelled", "psm.execution.steps.skipped",
        "psm.execution.attempts.started", "psm.execution.attempts.completed",
        "psm.execution.attempts.failed", "psm.execution.retries",
        "psm.execution.dispatch.rejected",
        "psm.execution.plugin.validation_failures",
        "psm.execution.sdk.compatibility_failures",
        "psm.execution.policy.compatibility_failures",
        "psm.execution.artifacts.files", "psm.execution.artifacts.objects",
        "psm.execution.artifacts.metrics", "psm.execution.warnings"
    ];
    private static readonly string[] HistogramNames =
    [
        "psm.execution.queue.duration", "psm.execution.wait.duration",
        "psm.execution.attempt.duration", "psm.execution.step.duration",
        "psm.execution.run.duration", "psm.execution.retry.delay",
        "psm.execution.artifact.bytes"
    ];
    private static readonly string[] GaugeNames =
    [
        "psm.execution.runs.active", "psm.execution.steps.active",
        "psm.execution.steps.waiting", "psm.execution.steps.throttled"
    ];

    public static IReadOnlyList<ExecutionMetricDefinition> Definitions { get; } =
        Array.AsReadOnly(CounterNames
            .Select(x => Define(x, ExecutionMetricInstrumentType.Counter, "count"))
            .Concat(HistogramNames.Select(x => Define(x,
                ExecutionMetricInstrumentType.Histogram,
                x.EndsWith("bytes", StringComparison.Ordinal) ? "By" : "s")))
            .Concat(GaugeNames.Select(x => Define(x,
                ExecutionMetricInstrumentType.ObservableGauge, "count")))
            .OrderBy(x => x.Name, StringComparer.Ordinal).ToArray());

    private static ExecutionMetricDefinition Define(
        string name, ExecutionMetricInstrumentType type, string unit) =>
        new(name, type, unit, $"Execution monitoring instrument {name}.",
            "Typed ExecutionEvent or bounded current projection",
            "Recorded only when the typed event or projection proves the value.",
            Array.AsReadOnly(AllowedTagNames.OrderBy(x => x, StringComparer.Ordinal).ToArray()),
            ProhibitedTagNames, "Bounded by explicit repository registration and enums.",
            "Duplicate events are suppressed while retained in the bounded observer set.",
            type == ExecutionMetricInstrumentType.Histogram
                ? "Negative measurements are ignored." : "Counters and gauges never record negatives.",
            "Recording failure is diagnosed and never changes execution.",
            InstrumentationVersion);
}

/// <summary>
/// Best-effort, non-durable observer. It never throws into execution and never
/// owns or mutates Execution State.
/// </summary>
public sealed class ExecutionMonitoringSubscriber : IExecutionEventSink, IExecutionMonitoring,
    IDisposable
{
    private const int WindowCapacity = 256;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private readonly object gate = new();
    private readonly TimeProvider timeProvider;
    private readonly Meter meter;
    private readonly ActivitySource activitySource;
    private readonly Dictionary<string, Counter<long>> counters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Histogram<double>> histograms = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> observedEvents = [];
    private readonly Dictionary<Guid, long> lastSequenceByRun = [];
    private readonly Queue<DateTime> failures = [];
    private readonly Queue<DateTime> timeouts = [];
    private readonly Queue<DateTime> cancellations = [];
    private readonly Queue<DateTime> rejections = [];
    private readonly Queue<DateTime> successes = [];
    private readonly Queue<DateTime> retries = [];
    private readonly List<MonitoringDiagnostic> diagnostics = [];
    private int activeRuns;
    private int activeSteps;
    private int waitingSteps;
    private int throttledSteps;
    private DateTime? lastEventAt;
    private DateTime? lastSuccessfulRunAt;
    private DateTime? lastFailedRunAt;
    private DateTime? lastTimeoutAt;
    private DateTime? lastCancellationAt;
    private bool subscriberHealthy = true;
    private bool metricsHealthy = true;
    private bool activitiesHealthy = true;
    private bool hasObservedEvent;

    public ExecutionMonitoringSubscriber(
        TimeProvider timeProvider, Meter? meter = null, ActivitySource? activitySource = null)
    {
        this.timeProvider = timeProvider;
        this.meter = meter ?? new(ExecutionMetricCatalog.InstrumentationName,
            ExecutionMetricCatalog.InstrumentationVersion);
        this.activitySource = activitySource ?? new(ExecutionMetricCatalog.InstrumentationName,
            ExecutionMetricCatalog.InstrumentationVersion);
        CreateInstruments();
    }

    public ExecutionMonitoringSnapshot Snapshot
    {
        get
        {
            lock (gate)
            {
                DateTime now = timeProvider.GetLocalNow().DateTime;
                Prune(now);
                MonitoringAlertSignal[] alerts = Alerts();
                MonitoringDiagnostic[] safeDiagnostics = diagnostics.TakeLast(32).ToArray();
                MonitoringHealthAssessment assessment =
                    new MonitoringHealthAssessmentCalculator(timeProvider).Evaluate(new(
                        hasObservedEvent, subscriberHealthy, metricsHealthy, activitiesHealthy,
                        failures.Count, timeouts.Count, rejections.Count,
                        Array.AsReadOnly(safeDiagnostics)));
                string[] warningCodes = safeDiagnostics.Select(x => x.ReasonCode)
                    .Concat(alerts.Select(x => x.ToString()))
                    .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal)
                    .Take(32).ToArray();
                ExecutionMonitoringStatus monitoringStatus = !hasObservedEvent
                    ? ExecutionMonitoringStatus.Unknown
                    : !subscriberHealthy || !metricsHealthy || !activitiesHealthy
                        ? ExecutionMonitoringStatus.Unhealthy
                        : failures.Count + timeouts.Count + cancellations.Count
                            + rejections.Count > 0
                            ? ExecutionMonitoringStatus.Degraded
                            : ExecutionMonitoringStatus.Healthy;
                return new(ExecutionMonitoringSchemaVersion.Value,
                    ExecutionMonitoringSnapshotSchemaVersion.Value, now,
                    ExecutionMetricCatalog.InstrumentationName,
                    ExecutionMetricCatalog.InstrumentationVersion,
                    monitoringStatus, assessment.Score, assessment.Rating,
                    activeRuns, activeSteps, waitingSteps, throttledSteps,
                    successes.Count,
                    failures.Count, timeouts.Count, cancellations.Count, rejections.Count,
                    retries.Count, warningCodes.Length,
                    lastEventAt, lastSuccessfulRunAt, lastFailedRunAt, lastTimeoutAt,
                    lastCancellationAt, now - Window, now,
                    subscriberHealthy, metricsHealthy, activitiesHealthy,
                    !safeDiagnostics.Any(x => x.Category is
                        MonitoringFailureCategory.MonitoringSchemaUnsupported or
                        MonitoringFailureCategory.MonitoringEventInvalid or
                        MonitoringFailureCategory.EventSequenceInvalid),
                    Array.AsReadOnly(alerts),
                    Array.AsReadOnly(warningCodes),
                    assessment.ReasonCodes,
                    Array.AsReadOnly(safeDiagnostics));
            }
        }
    }

    public ExecutionMonitoringSnapshot GetCurrentSnapshot() => Snapshot;

    public Task PublishAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Process(executionEvent);
        }
        catch (Exception)
        {
            lock (gate)
            {
                subscriberHealthy = false;
                AddDiagnostic(MonitoringFailureCategory.MonitoringSubscriberFailure,
                    "MonitoringSubscriberFailure",
                    "The monitoring event could not be projected safely.");
            }
        }
        return Task.CompletedTask;
    }

    private void Process(ExecutionEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (gate)
        {
            if (value.EventSchemaVersion != ExecutionEventSchemaVersion.Value)
            {
                AddDiagnostic(MonitoringFailureCategory.MonitoringSchemaUnsupported,
                    "MonitoringSchemaUnsupported",
                    "The execution event schema is not supported.");
                return;
            }
            if (value.EventId == Guid.Empty || value.ExecutionRunId == Guid.Empty
                || value.Sequence < 1 || string.IsNullOrWhiteSpace(value.ReasonCode)
                || !Enum.IsDefined(value.EventType))
            {
                AddDiagnostic(MonitoringFailureCategory.MonitoringEventInvalid,
                    "MonitoringEventInvalid", "The execution event is invalid.");
                return;
            }
            if (!observedEvents.Add(value.EventId))
            {
                AddDiagnostic(MonitoringFailureCategory.DuplicateEventObserved,
                    "DuplicateEventObserved", "A duplicate execution event was ignored.");
                return;
            }
            if (lastSequenceByRun.TryGetValue(value.ExecutionRunId, out long sequence)
                && value.Sequence <= sequence)
            {
                AddDiagnostic(MonitoringFailureCategory.EventSequenceInvalid,
                    "EventSequenceInvalid", "An out-of-order execution event was ignored.");
                return;
            }
            lastSequenceByRun[value.ExecutionRunId] = value.Sequence;
            hasObservedEvent = true;
            lastEventAt = value.OccurredAt;
            DateTime now = value.OccurredAt;
            Prune(now);
            UpdateHealth(value, now);
        }
        // Diagnostic listeners are external callbacks. Never invoke them while
        // holding the projection lock.
        RecordActivity(value);
        RecordMetric(value);
    }

    private void RecordActivity(ExecutionEvent value)
    {
        try
        {
            using Activity? activity = activitySource.StartActivity(ActivityName(value.EventType));
            if (activity is null) return;
            activity.SetTag("execution.monitoring.schema_version",
                ExecutionMonitoringSchemaVersion.Value);
            activity.SetTag("execution.event.schema_version", value.EventSchemaVersion);
            if (value.StrategyCode is not null)
                activity.SetTag("strategy.code", NormalizeDimension(value.StrategyCode));
            if (value.PluginId is not null)
                activity.SetTag("plugin.id", NormalizeDimension(value.PluginId));
            activity.SetTag("execution.outcome", NormalizeDimension(value.Status));
            activity.SetTag("failure.category", value.FailureCategory.ToString());
            activity.SetTag("reason.code", NormalizeDimension(value.ReasonCode));
            activity.SetStatus(IsFailure(value.EventType) ? ActivityStatusCode.Error
                : ActivityStatusCode.Ok);
        }
        catch
        {
            lock (gate)
            {
                activitiesHealthy = false;
                AddDiagnostic(MonitoringFailureCategory.ActivityCreationFailure,
                    "ActivityCreationFailure", "An execution Activity could not be recorded.");
            }
        }
    }

    private void RecordMetric(ExecutionEvent value)
    {
        try
        {
            string? counter = CounterName(value.EventType);
            TagList tags = SafeTags(value);
            if (counter is not null) counters[counter].Add(1, tags);
            if (value.EventType == ExecutionEventType.ExecutionStepAttemptCompleted
                && value.FailureCategory != RuntimeFailureCategory.None)
                counters["psm.execution.attempts.failed"].Add(1, tags);
            if (value.EventType == ExecutionEventType.ExecutionDispatchRejected)
            {
                if (value.ReasonCode.Contains("Validation", StringComparison.Ordinal))
                    counters["psm.execution.plugin.validation_failures"].Add(1, tags);
                if (value.ReasonCode.Contains("Sdk", StringComparison.Ordinal))
                    counters["psm.execution.sdk.compatibility_failures"].Add(1, tags);
                if (value.ReasonCode.Contains("Policy", StringComparison.Ordinal)
                    || value.ReasonCode.Contains("Unsupported", StringComparison.Ordinal))
                    counters["psm.execution.policy.compatibility_failures"].Add(1, tags);
            }
            if (value.Duration is { } duration)
            {
                string? histogram = value.EventType switch
                {
                    ExecutionEventType.ExecutionStepAttemptCompleted =>
                        "psm.execution.attempt.duration",
                    ExecutionEventType.ExecutionStepCompleted or
                    ExecutionEventType.ExecutionStepFailed or
                    ExecutionEventType.ExecutionStepTimedOut or
                    ExecutionEventType.ExecutionStepCancelled =>
                        "psm.execution.step.duration",
                    ExecutionEventType.ExecutionRunCompleted or
                    ExecutionEventType.ExecutionRunFailed or
                    ExecutionEventType.ExecutionRunCancelled =>
                        "psm.execution.run.duration",
                    ExecutionEventType.ExecutionStepRetryScheduled =>
                        "psm.execution.retry.delay",
                    _ => null
                };
                if (histogram is not null && duration >= TimeSpan.Zero)
                    histograms[histogram].Record(duration.TotalSeconds, tags);
            }
        }
        catch
        {
            lock (gate)
            {
                metricsHealthy = false;
                AddDiagnostic(MonitoringFailureCategory.MetricRecordingFailure,
                    "MetricRecordingFailure", "An execution metric could not be recorded.");
            }
        }
    }

    private void UpdateHealth(ExecutionEvent value, DateTime now)
    {
        switch (value.EventType)
        {
            case ExecutionEventType.ExecutionRunStarted: activeRuns++; break;
            case ExecutionEventType.ExecutionRunCompleted:
                activeRuns = Math.Max(0, activeRuns - 1); lastSuccessfulRunAt = now;
                AddBounded(successes, now); break;
            case ExecutionEventType.ExecutionRunFailed:
                activeRuns = Math.Max(0, activeRuns - 1); lastFailedRunAt = now;
                AddBounded(failures, now); break;
            case ExecutionEventType.ExecutionRunCancelled:
                activeRuns = Math.Max(0, activeRuns - 1); lastCancellationAt = now;
                AddBounded(cancellations, now); break;
            case ExecutionEventType.ExecutionStepStarted:
                activeSteps++; waitingSteps = Math.Max(0, waitingSteps - 1); break;
            case ExecutionEventType.ExecutionStepWaiting:
                waitingSteps++;
                if (value.ReasonCode.Contains("Throttle", StringComparison.Ordinal))
                    throttledSteps++;
                break;
            case ExecutionEventType.ExecutionStepCompleted:
            case ExecutionEventType.ExecutionStepSkipped:
                activeSteps = Math.Max(0, activeSteps - 1);
                throttledSteps = Math.Max(0, throttledSteps - 1);
                break;
            case ExecutionEventType.ExecutionStepFailed:
                activeSteps = Math.Max(0, activeSteps - 1);
                throttledSteps = Math.Max(0, throttledSteps - 1);
                AddBounded(failures, now); break;
            case ExecutionEventType.ExecutionStepTimedOut:
                activeSteps = Math.Max(0, activeSteps - 1);
                throttledSteps = Math.Max(0, throttledSteps - 1);
                lastTimeoutAt = now;
                AddBounded(timeouts, now); break;
            case ExecutionEventType.ExecutionStepCancelled:
                activeSteps = Math.Max(0, activeSteps - 1);
                throttledSteps = Math.Max(0, throttledSteps - 1);
                lastCancellationAt = now;
                AddBounded(cancellations, now); break;
            case ExecutionEventType.ExecutionDispatchRejected:
                AddBounded(rejections, now); break;
            case ExecutionEventType.ExecutionStepRetryScheduled:
                AddBounded(retries, now); break;
        }
    }

    private void CreateInstruments()
    {
        foreach (ExecutionMetricDefinition definition in ExecutionMetricCatalog.Definitions
            .Where(x => x.InstrumentType == ExecutionMetricInstrumentType.Counter))
            counters.Add(definition.Name,
                meter.CreateCounter<long>(definition.Name, definition.Unit));
        foreach (ExecutionMetricDefinition definition in ExecutionMetricCatalog.Definitions
            .Where(x => x.InstrumentType == ExecutionMetricInstrumentType.Histogram))
            histograms.Add(definition.Name,
                meter.CreateHistogram<double>(definition.Name, definition.Unit));
        meter.CreateObservableGauge("psm.execution.runs.active", () => Snapshot.ActiveRunCount,
            "count");
        meter.CreateObservableGauge("psm.execution.steps.active", () => Snapshot.ActiveStepCount,
            "count");
        meter.CreateObservableGauge("psm.execution.steps.waiting", () => Snapshot.WaitingStepCount,
            "count");
        meter.CreateObservableGauge("psm.execution.steps.throttled",
            () => Snapshot.ThrottledStepCount, "count");
    }

    private static string? CounterName(ExecutionEventType type) => type switch
    {
        ExecutionEventType.ExecutionRunStarted => "psm.execution.runs.started",
        ExecutionEventType.ExecutionRunCompleted => "psm.execution.runs.completed",
        ExecutionEventType.ExecutionRunFailed => "psm.execution.runs.failed",
        ExecutionEventType.ExecutionRunCancelled => "psm.execution.runs.cancelled",
        ExecutionEventType.ExecutionStepStarted => "psm.execution.steps.started",
        ExecutionEventType.ExecutionStepCompleted => "psm.execution.steps.completed",
        ExecutionEventType.ExecutionStepFailed => "psm.execution.steps.failed",
        ExecutionEventType.ExecutionStepTimedOut => "psm.execution.steps.timed_out",
        ExecutionEventType.ExecutionStepCancelled => "psm.execution.steps.cancelled",
        ExecutionEventType.ExecutionStepSkipped => "psm.execution.steps.skipped",
        ExecutionEventType.ExecutionStepAttemptStarted => "psm.execution.attempts.started",
        ExecutionEventType.ExecutionStepAttemptCompleted => "psm.execution.attempts.completed",
        ExecutionEventType.ExecutionStepRetryScheduled => "psm.execution.retries",
        ExecutionEventType.ExecutionDispatchRejected => "psm.execution.dispatch.rejected",
        _ => null
    };

    private static string ActivityName(ExecutionEventType type) => type switch
    {
        >= ExecutionEventType.ExecutionDispatchRequested and
            <= ExecutionEventType.ExecutionDispatchSubmitted => "execution.dispatch",
        ExecutionEventType.ExecutionStepAttemptStarted or
            ExecutionEventType.ExecutionStepAttemptCompleted => "execution.attempt",
        ExecutionEventType.ExecutionStepRetryScheduled => "execution.retry_delay",
        >= ExecutionEventType.ExecutionStepQueued and
            <= ExecutionEventType.ExecutionStepCancelled => "execution.step",
        _ => "execution.run"
    };

    private static bool IsFailure(ExecutionEventType type) => type is
        ExecutionEventType.ExecutionRunFailed or ExecutionEventType.ExecutionStepFailed or
        ExecutionEventType.ExecutionStepTimedOut or ExecutionEventType.ExecutionStepCancelled or
        ExecutionEventType.ExecutionRunCancelled or ExecutionEventType.ExecutionDispatchRejected;

    private static TagList SafeTags(ExecutionEvent value)
    {
        var tags = new TagList
        {
            { "execution.outcome", NormalizeDimension(value.Status) },
            { "failure.category", value.FailureCategory.ToString() },
            { "reason.code", NormalizeDimension(value.ReasonCode) }
        };
        if (value.StrategyCode is not null)
            tags.Add("strategy.code", NormalizeDimension(value.StrategyCode));
        if (value.PluginId is not null)
            tags.Add("plugin.id", NormalizeDimension(value.PluginId));
        return tags;
    }

    private static string NormalizeDimension(string value)
    {
        string normalized = value.Trim();
        return normalized.Length is > 0 and <= 128
            && normalized.All(x => char.IsLetterOrDigit(x) || x is '.' or '_' or '-')
            ? normalized : "Other";
    }

    private void AddDiagnostic(
        MonitoringFailureCategory category, string reason, string explanation)
    {
        if (diagnostics.Count == 32) diagnostics.RemoveAt(0);
        diagnostics.Add(new(category, reason, explanation));
    }

    private void AddBounded(Queue<DateTime> values, DateTime value)
    {
        while (values.Count >= WindowCapacity) values.Dequeue();
        values.Enqueue(value);
    }

    private void Prune(DateTime now)
    {
        DateTime threshold = now - Window;
        foreach (Queue<DateTime> values in new[] { failures, timeouts, cancellations,
                     rejections, successes, retries })
            while (values.TryPeek(out DateTime value) && value < threshold) values.Dequeue();
        if (observedEvents.Count > 2048)
        {
            observedEvents.Clear();
            lastSequenceByRun.Clear();
        }
    }

    private MonitoringAlertSignal[] Alerts()
    {
        var alerts = new List<MonitoringAlertSignal>(4);
        if (failures.Count >= 3) alerts.Add(MonitoringAlertSignal.RepeatedRuntimeFailure);
        if (timeouts.Count >= 3) alerts.Add(MonitoringAlertSignal.ExcessiveTimeoutRate);
        if (rejections.Count >= 3)
            alerts.Add(MonitoringAlertSignal.ExcessiveDispatchRejectionRate);
        if (!subscriberHealthy)
            alerts.Add(MonitoringAlertSignal.MonitoringSubscriberFailure);
        return alerts.OrderBy(x => x).ToArray();
    }

    public void Dispose()
    {
        meter.Dispose();
        activitySource.Dispose();
    }
}
