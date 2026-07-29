using System.Collections.ObjectModel;
using PSMOperationsPlatform.CollectorSdk;

namespace PSMOperationsPlatform.Application.Runtime;

public static class ExecutionHistorySchemaVersion { public const int Value = 1; }

public enum ExecutionHistoryProjectionStatus
{
    Pending = 1, Completed = 2, Partial = 3, Failed = 4,
    Unsupported = 5, Unknown = 6
}

public enum ExecutionHistoryFailureCategory
{
    None = 0, HistoryInputInvalid = 1, HistorySchemaUnsupported = 2,
    HistoryProjectionFailure = 3, HistoryPersistenceFailure = 4,
    HistoryConcurrencyConflict = 5, HistoryDuplicateObserved = 6,
    HistorySequenceInvalid = 7, HistoryArtifactMetadataInvalid = 8,
    HistoryPolicyProvenanceInvalid = 9, HistoryQueryInvalid = 10,
    HistoryUnavailable = 11, Cancellation = 12, Unexpected = 13
}

public enum ExecutionHistoryWriteDisposition
{
    Created = 1, Duplicate = 2, Failed = 3
}

public sealed record ExecutionHistoryPolicyProvenance(
    Guid ExecutionStepId,
    string TimeoutPolicyCode,
    int TimeoutPolicyVersion,
    long TimeoutTicks,
    string RetryPolicyCode,
    int RetryPolicyVersion,
    int MaximumAttempts,
    string RetryDelayClassification,
    string ParallelPolicyCode,
    int ParallelPolicyVersion,
    int ParallelMaximumConcurrency,
    string ThrottlingPolicyCode,
    int ThrottlingPolicyVersion,
    int ThrottlingMaximumConcurrency,
    string BatchingPolicyCode,
    int BatchingPolicyVersion,
    bool BatchingEnabled);

public sealed record ExecutionArtifactHistoryItem(
    Guid ExecutionStepId,
    string ArtifactId,
    int ArtifactSchemaVersion,
    string ArtifactType,
    string LogicalName,
    string? ContentType,
    long? ObjectCount,
    long? MetricCount,
    long ByteCount,
    DateTime CreatedAt);

public sealed record ExecutionAttemptHistoryItem(
    Guid ExecutionStepId,
    int AttemptNumber,
    DateTime StartedAt,
    DateTime? CompletedAt,
    long DurationTicks,
    string AttemptOutcome,
    string FailureCategory,
    string? ReasonCode,
    bool RetryScheduled,
    long? RetryDelayTicks,
    bool CancellationObserved,
    bool TimeoutObserved,
    int WarningCount);

public sealed record ExecutionStepHistoryItem(
    Guid ExecutionStepId,
    int StepOrdinal,
    int DependencyCount,
    string StrategyCode,
    int StrategyVersion,
    string PluginId,
    int PluginVersion,
    string Subject,
    DateTime QueuedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long QueueDurationTicks,
    long WaitDurationTicks,
    long ExecutionDurationTicks,
    string StepOutcome,
    string FailureCategory,
    string? ReasonCode,
    int AttemptCount,
    int RetryCount,
    bool WasThrottled,
    bool WasSkipped,
    bool WasCancelled,
    bool WasTimedOut,
    int ArtifactFileCount,
    int ArtifactObjectCount,
    int ArtifactMetricCount,
    long ArtifactByteCount,
    int WarningCount);

public sealed record ExecutionStateTransitionHistoryItem(
    Guid? ExecutionStepId,
    long TransitionSequence,
    string EntityType,
    string? FromState,
    string ToState,
    DateTime TransitionedAt,
    string EventType,
    string ReasonCode,
    string FailureCategory,
    int EventSchemaVersion);

public sealed record ExecutionRunHistoryItem(
    Guid ExecutionRunId,
    Guid ExecutionPlanId,
    Guid ManagedServerId,
    Guid SourceDecisionPlanId,
    Guid SourceCapabilitySnapshotId,
    Guid SourceInventoryRunId,
    long SourceInventoryVersion,
    DateTime QueuedAt,
    DateTime? StartedAt,
    DateTime CompletedAt,
    long DurationTicks,
    DateTime RecordedAt,
    string ExecutionOutcome,
    string TerminalState,
    string FailureCategory,
    string? ReasonCode,
    int WarningCount,
    int AttemptCount,
    int RetryCount,
    int StepCount,
    int CompletedStepCount,
    int FailedStepCount,
    int TimedOutStepCount,
    int CancelledStepCount,
    int SkippedStepCount,
    string StrategyCode,
    int StrategyVersion,
    string PluginId,
    int PluginVersion,
    string TargetSdkVersion,
    string RuntimeContractVersion,
    int ExecutionPlanSchemaVersion,
    int ExecutionStateSchemaVersion,
    int ExecutionEventSchemaVersion,
    int ExecutionMonitoringSchemaVersion,
    string Subject,
    bool IsReadOnly,
    int ArtifactFileCount,
    int ArtifactObjectCount,
    int ArtifactMetricCount,
    long ArtifactByteCount,
    ExecutionHistoryProjectionStatus ProjectionStatus,
    ExecutionHistoryFailureCategory ProjectionFailureCategory,
    string ProjectionReasonCode);

public sealed record ExecutionHistoryProjection(
    int HistorySchemaVersion,
    ExecutionRunHistoryItem Run,
    IReadOnlyList<ExecutionStepHistoryItem> Steps,
    IReadOnlyList<ExecutionAttemptHistoryItem> Attempts,
    IReadOnlyList<ExecutionStateTransitionHistoryItem> Transitions,
    IReadOnlyList<ExecutionArtifactHistoryItem> Artifacts,
    IReadOnlyList<ExecutionHistoryPolicyProvenance> Policies);

public sealed record ExecutionHistoryWriteResult(
    ExecutionHistoryWriteDisposition Disposition,
    ExecutionHistoryFailureCategory FailureCategory,
    string ReasonCode);

public sealed record ExecutionHistoryPageRequest(int PageNumber = 1, int PageSize = 50)
{
    public const int DefaultPageSize = 50;
    public const int MaximumPageSize = 200;

    public ExecutionHistoryPageRequest Validate()
    {
        if (PageNumber < 1 || PageSize < 1 || PageSize > MaximumPageSize)
            throw new ArgumentException("HistoryPageInvalid");
        return this;
    }
}

public sealed record ExecutionHistoryPageResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    long TotalCount,
    bool HasNextPage);

public sealed record ExecutionHistoryQuery(
    DateTime? CompletedFrom,
    DateTime? CompletedTo,
    Guid? ManagedServerId,
    string? StrategyCode,
    string? PluginId,
    string? Outcome,
    string? FailureCategory,
    string? ReasonCode,
    ExecutionHistoryPageRequest Page);

public sealed record ExecutionHistoryRetentionPolicy(
    int RunDays,
    int TransitionDays,
    int FailedProjectionDays,
    int BatchSize)
{
    public static ExecutionHistoryRetentionPolicy Version1 { get; } =
        new(180, 90, 90, 500);

    public ExecutionHistoryRetentionPolicy Validate()
    {
        if (RunDays < 1 || TransitionDays < 1 || FailedProjectionDays < 1
            || BatchSize < 1 || BatchSize > 5000)
            throw new ArgumentException("HistoryRetentionPolicyInvalid");
        return this;
    }
}

public sealed record ExecutionHistoryRetentionCutoffs(
    DateTime RunCutoff,
    DateTime TransitionCutoff,
    DateTime FailedProjectionCutoff);

public sealed record ExecutionHistoryRetentionResult(
    int RunsDeleted,
    int StepsDeleted,
    int AttemptsDeleted,
    int TransitionsDeleted,
    int ArtifactsDeleted,
    int PoliciesDeleted);

public interface IExecutionHistoryWriter
{
    Task<ExecutionHistoryWriteResult> WriteAsync(
        ExecutionHistoryProjection projection, CancellationToken cancellationToken);
}

public interface IExecutionHistoryQueryService
{
    Task<ExecutionRunHistoryItem?> GetRunAsync(
        Guid executionRunId, CancellationToken cancellationToken);
    Task<ExecutionHistoryPageResult<ExecutionRunHistoryItem>> ListRunsAsync(
        ExecutionHistoryQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionStepHistoryItem>> GetStepsAsync(
        Guid executionRunId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionAttemptHistoryItem>> GetAttemptsAsync(
        Guid executionRunId, Guid executionStepId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionStateTransitionHistoryItem>> GetTransitionsAsync(
        Guid executionRunId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionArtifactHistoryItem>> GetArtifactsAsync(
        Guid executionRunId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExecutionHistoryPolicyProvenance>> GetPoliciesAsync(
        Guid executionRunId, CancellationToken cancellationToken);
}

public interface IExecutionHistoryRetentionService
{
    ExecutionHistoryRetentionCutoffs GetCutoffs(ExecutionHistoryRetentionPolicy policy);
    Task<ExecutionHistoryRetentionResult> DeleteExpiredAsync(
        ExecutionHistoryRetentionPolicy policy, CancellationToken cancellationToken);
}

public static class ExecutionHistoryProjector
{
    private static readonly HashSet<ExecutionEventType> SupportedEvents =
        Enum.GetValues<ExecutionEventType>().ToHashSet();

    public static ExecutionHistoryProjection Project(
        PreparedExecutionDispatch dispatch,
        ExecutionRunState state,
        IEnumerable<ExecutionEvent> events,
        IReadOnlyDictionary<Guid, ExecutionArtifacts>? artifactsByStep,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (state.Id != dispatch.ExecutionRunId || state.ExecutionPlanId != dispatch.Plan.ExecutionPlanId
            || state.Status is ExecutionRunStatus.Created or ExecutionRunStatus.Queued
                or ExecutionRunStatus.Running || state.CompletedAt is null
            || dispatch.Steps.Count != state.Steps.Count)
            throw new ArgumentException("HistoryInputInvalid");

        ExecutionEvent[] suppliedEvents = events.ToArray();
        if (suppliedEvents.Zip(suppliedEvents.Skip(1),
                (a, b) => a.Sequence >= b.Sequence).Any(x => x))
            throw new ArgumentException("HistorySequenceInvalid");
        ExecutionEvent[] orderedEvents = suppliedEvents
            .OrderBy(x => x.Sequence).ToArray();
        if (orderedEvents.Any(x => x.EventSchemaVersion != ExecutionEventSchemaVersion.Value))
            throw new ArgumentException("HistorySchemaUnsupported");
        if (orderedEvents.Any(x => !SupportedEvents.Contains(x.EventType)))
            throw new ArgumentException("HistorySequenceInvalid");
        if (orderedEvents.Select(x => x.Sequence).Distinct().Count() != orderedEvents.Length
            || orderedEvents.Any(x => x.Sequence < 1)
            || orderedEvents.Zip(orderedEvents.Skip(1), (a, b) => a.Sequence >= b.Sequence).Any(x => x))
            throw new ArgumentException("HistorySequenceInvalid");

        var prepared = dispatch.Steps.ToDictionary(x => x.Step.ExecutionPlanStepId);
        var artifactMap = artifactsByStep ?? new Dictionary<Guid, ExecutionArtifacts>();
        var artifactItems = new List<ExecutionArtifactHistoryItem>();
        var stepItems = new List<ExecutionStepHistoryItem>();
        var attemptItems = new List<ExecutionAttemptHistoryItem>();
        var policyItems = new List<ExecutionHistoryPolicyProvenance>();

        foreach (ExecutionStepState step in state.Steps.OrderBy(x => x.QueueSequence))
        {
            if (!prepared.TryGetValue(step.ExecutionPlanStepId, out PreparedExecutionStep? item))
                throw new ArgumentException("HistoryInputInvalid");
            ExecutionArtifacts stepArtifacts = artifactMap.TryGetValue(
                step.ExecutionPlanStepId, out ExecutionArtifacts? found)
                ? found : ExecutionArtifacts.Empty;
            AddArtifacts(step.ExecutionPlanStepId, state.CompletedAt.Value,
                stepArtifacts, artifactItems);
            int fileCount = stepArtifacts.Files.Count;
            int objectCount = stepArtifacts.Objects.Count;
            int metricCount = stepArtifacts.Metrics.Count;
            long byteCount = CheckedSum(stepArtifacts.Files.Select(x => x.SizeBytes));
            stepItems.Add(new(step.ExecutionPlanStepId, step.QueueSequence,
                item.Step.DependencyStrategyCodes.Count, step.StrategyCode,
                step.StrategyVersion, item.Descriptor.PluginId,
                item.Descriptor.PluginVersion, item.Step.Subject.ToString(), step.QueuedAt,
                step.StartedAt, step.CompletedAt ?? step.CancelledAt ?? step.TimedOutAt,
                step.QueueDuration.Ticks, step.WaitDuration.Ticks,
                step.ExecutionDuration.Ticks, step.Status.ToString(),
                step.FailureCategory.ToString(), Safe(step.ReasonCode, 100),
                step.AttemptCount, step.RetryCount,
                step.WaitDuration > TimeSpan.Zero
                    && item.Step.ThrottlingClass != ExecutionPlanning.ThrottlingClass.Lightweight,
                step.Status == ExecutionStepStatus.Skipped,
                step.Status == ExecutionStepStatus.Cancelled,
                step.Status == ExecutionStepStatus.TimedOut,
                fileCount, objectCount, metricCount, byteCount,
                stepArtifacts.Warnings.Count));
            foreach (ExecutionAttemptState attempt in step.Attempts.OrderBy(x => x.AttemptNumber))
            {
                attemptItems.Add(new(step.ExecutionPlanStepId, attempt.AttemptNumber,
                    attempt.StartedAt, attempt.CompletedAt, attempt.Duration.Ticks,
                    attempt.Status.ToString(), attempt.FailureCategory.ToString(),
                    Safe(attempt.ReasonCode, 100), attempt.IsRetry,
                    RetryDelay(item.Policy, attempt.AttemptNumber),
                    attempt.Status == ExecutionAttemptStatus.Cancelled,
                    attempt.Status == ExecutionAttemptStatus.TimedOut, 0));
            }
            policyItems.Add(Policy(step.ExecutionPlanStepId, item.Policy));
        }

        ExecutionStateTransitionHistoryItem[] transitions = orderedEvents
            .Where(x => IsLifecycle(x.EventType))
            .Select(x => new ExecutionStateTransitionHistoryItem(
                x.ExecutionStepId, x.Sequence,
                x.ExecutionStepId is null ? "Run" : "Step",
                null, Safe(x.Status, 40) ?? "Unknown", x.OccurredAt,
                x.EventType.ToString(), Safe(x.ReasonCode, 100) ?? "Unspecified",
                x.FailureCategory.ToString(), x.EventSchemaVersion)).ToArray();

        bool partial = transitions.Length == 0
            || state.Steps.Any(x => x.AttemptCount > 0)
                && !orderedEvents.Any(x =>
                    x.EventType == ExecutionEventType.ExecutionStepAttemptCompleted)
            || artifactMap.Count < state.Steps.Count;
        PreparedExecutionStep first = dispatch.Steps
            .OrderBy(x => x.Step.StepSequence).First();
        int warningCount = artifactMap.Values.Sum(x => x.Warnings.Count);
        var run = new ExecutionRunHistoryItem(state.Id, state.ExecutionPlanId,
            state.ManagedServerId, state.SourceDecisionPlanId,
            state.SourceCapabilitySnapshotId, state.SourceInventoryRunId,
            state.SourceInventoryVersion, state.QueuedAt, state.StartedAt,
            state.CompletedAt.Value, state.TotalDuration.Ticks,
            timeProvider.GetLocalNow().DateTime, state.Status.ToString(),
            state.Status.ToString(), state.FailureCategory.ToString(),
            Safe(state.ReasonCode, 100), warningCount, state.AttemptCount,
            state.RetryCount, state.StepCount,
            state.Steps.Count(x => x.Status == ExecutionStepStatus.Completed),
            state.Steps.Count(x => x.Status == ExecutionStepStatus.Failed),
            state.Steps.Count(x => x.Status == ExecutionStepStatus.TimedOut),
            state.Steps.Count(x => x.Status == ExecutionStepStatus.Cancelled),
            state.Steps.Count(x => x.Status == ExecutionStepStatus.Skipped),
            first.Step.StrategyCode, first.Step.StrategyVersion,
            first.Descriptor.PluginId, first.Descriptor.PluginVersion,
            first.Descriptor.TargetSdkVersion.ToString(), state.RuntimeVersion,
            state.ExecutionPlanSchemaVersion, state.ExecutionStateSchemaVersion,
            ExecutionEventSchemaVersion.Value, ExecutionMonitoringSchemaVersion.Value,
            first.Step.Subject.ToString(), first.Descriptor.IsReadOnly,
            artifactItems.Count(x => x.ArtifactType == "File"),
            artifactItems.Count(x => x.ArtifactType == "Object"),
            artifactItems.Count(x => x.ArtifactType == "Metric"),
            CheckedSum(artifactItems.Select(x => x.ByteCount)),
            partial ? ExecutionHistoryProjectionStatus.Partial
                : ExecutionHistoryProjectionStatus.Completed,
            partial ? ExecutionHistoryFailureCategory.HistorySequenceInvalid
                : ExecutionHistoryFailureCategory.None,
            partial ? "HistoryFactsIncomplete" : "HistoryProjectionCompleted");

        return new(ExecutionHistorySchemaVersion.Value, run,
            ReadOnly(stepItems.OrderBy(x => x.StepOrdinal)),
            ReadOnly(attemptItems.OrderBy(x => x.ExecutionStepId)
                .ThenBy(x => x.AttemptNumber)),
            Array.AsReadOnly(transitions),
            ReadOnly(artifactItems.OrderBy(x => x.ExecutionStepId)
                .ThenBy(x => x.ArtifactId, StringComparer.Ordinal)),
            ReadOnly(policyItems.OrderBy(x => x.ExecutionStepId)));
    }

    private static ExecutionHistoryPolicyProvenance Policy(
        Guid stepId, ExecutionPolicy policy)
    {
        if (policy.PolicySchemaVersion < 1 || policy.Timeout.Version < 1
            || policy.Retry.Version < 1 || policy.Parallel.Version < 1
            || policy.Throttling.Version < 1 || policy.Batching.Version < 1)
            throw new ArgumentException("HistoryPolicyProvenanceInvalid");
        return new(stepId, policy.Timeout.Code, policy.Timeout.Version,
            policy.Timeout.Timeout.Ticks, policy.Retry.Code, policy.Retry.Version,
            policy.Retry.MaxAttempts,
            policy.Retry.DelaySchedule.Count == 0 ? "None" : "FixedSchedule",
            policy.Parallel.Code, policy.Parallel.Version,
            policy.Parallel.MaximumConcurrency, policy.Throttling.Code,
            policy.Throttling.Version, policy.Throttling.MaximumConcurrency,
            policy.Batching.Code, policy.Batching.Version, policy.Batching.Enabled);
    }

    private static long? RetryDelay(ExecutionPolicy policy, int attemptNumber) =>
        attemptNumber > 1 && attemptNumber - 2 < policy.Retry.DelaySchedule.Count
            ? policy.Retry.DelaySchedule[attemptNumber - 2].Ticks : null;

    private static void AddArtifacts(Guid stepId, DateTime createdAt,
        ExecutionArtifacts artifacts, List<ExecutionArtifactHistoryItem> destination)
    {
        foreach (CollectedFileArtifact value in artifacts.Files)
        {
            if (Unsafe(value.LogicalReference))
                throw new ArgumentException("HistoryArtifactMetadataInvalid");
            destination.Add(new(stepId, value.ArtifactId,
                artifacts.ArtifactSchemaVersion, "File", value.LogicalReference,
                value.ContentType, null, null, value.SizeBytes, createdAt));
        }
        foreach (CollectedObjectArtifact value in artifacts.Objects)
            destination.Add(new(stepId, value.ArtifactId,
                artifacts.ArtifactSchemaVersion, "Object", value.ObjectType,
                null, value.ObjectCount, null, 0, createdAt));
        foreach (GeneratedMetricArtifact value in artifacts.Metrics)
            destination.Add(new(stepId, value.ArtifactId,
                artifacts.ArtifactSchemaVersion, "Metric", value.MetricName,
                null, null, 1, 0, createdAt));
    }

    private static bool Unsafe(string value) =>
        Path.IsPathRooted(value) || value.Contains("://", StringComparison.Ordinal)
        || value.Contains("..", StringComparison.Ordinal)
        || value.Contains('@', StringComparison.Ordinal);
    private static bool IsLifecycle(ExecutionEventType value) =>
        value <= ExecutionEventType.ExecutionRunCancelled;
    private static string? Safe(string? value, int maximum) =>
        value is null ? null : value.Length <= maximum && !value.Contains('\r')
            && !value.Contains('\n') ? value : throw new ArgumentException("HistoryInputInvalid");
    private static long CheckedSum(IEnumerable<long> values)
    {
        long total = 0;
        foreach (long value in values)
        {
            if (value < 0) throw new ArgumentException("HistoryInputInvalid");
            total = checked(total + value);
        }
        return total;
    }
    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());
}
