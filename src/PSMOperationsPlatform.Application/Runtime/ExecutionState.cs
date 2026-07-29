using System.Collections.ObjectModel;

namespace PSMOperationsPlatform.Application.Runtime;

public sealed class ExecutionAttemptState
{
    public ExecutionAttemptState(Guid id, int attemptNumber, bool isRetry, DateTime startedAt)
    {
        Id = id;
        AttemptNumber = attemptNumber > 0 ? attemptNumber : throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        IsRetry = isRetry;
        StartedAt = startedAt;
        Status = ExecutionAttemptStatus.Running;
    }

    public Guid Id { get; }
    public int AttemptNumber { get; }
    public bool IsRetry { get; }
    public ExecutionAttemptStatus Status { get; private set; }
    public DateTime StartedAt { get; }
    public DateTime? CompletedAt { get; private set; }
    public TimeSpan Duration { get; private set; }
    public long? BytesCollected { get; private set; }
    public long? ObjectsCollected { get; private set; }
    public RuntimeFailureCategory FailureCategory { get; private set; }
    public string? ReasonCode { get; private set; }
    public string? FailureSummary { get; private set; }

    public void Complete(ExecutionAttemptStatus status, DateTime completedAt,
        TimeSpan duration, RuntimeFailureCategory failureCategory, string reasonCode,
        string? failureSummary, long? bytes, long? objects)
    {
        if (Status != ExecutionAttemptStatus.Running
            || status == ExecutionAttemptStatus.Running || duration < TimeSpan.Zero
            || bytes < 0 || objects < 0)
            throw new InvalidOperationException("InvalidAttemptTransition");
        Status = status;
        CompletedAt = completedAt;
        Duration = duration;
        FailureCategory = failureCategory;
        ReasonCode = reasonCode;
        FailureSummary = failureSummary;
        BytesCollected = bytes;
        ObjectsCollected = objects;
    }
}

public sealed class ExecutionStepState
{
    private static readonly IReadOnlyDictionary<ExecutionStepStatus, ExecutionStepStatus[]> Allowed =
        new Dictionary<ExecutionStepStatus, ExecutionStepStatus[]>
        {
            [ExecutionStepStatus.Pending] = [ExecutionStepStatus.Queued, ExecutionStepStatus.Cancelled],
            [ExecutionStepStatus.Queued] = [ExecutionStepStatus.WaitingForDependency,
                ExecutionStepStatus.WaitingForThrottle, ExecutionStepStatus.Running,
                ExecutionStepStatus.Cancelled, ExecutionStepStatus.Skipped],
            [ExecutionStepStatus.WaitingForDependency] = [ExecutionStepStatus.WaitingForThrottle,
                ExecutionStepStatus.Running, ExecutionStepStatus.Cancelled, ExecutionStepStatus.Skipped],
            [ExecutionStepStatus.WaitingForThrottle] = [ExecutionStepStatus.Running,
                ExecutionStepStatus.Cancelled, ExecutionStepStatus.Skipped],
            [ExecutionStepStatus.Running] = [ExecutionStepStatus.Completed, ExecutionStepStatus.Failed,
                ExecutionStepStatus.Cancelled, ExecutionStepStatus.TimedOut]
        };
    private readonly List<ExecutionAttemptState> attempts = [];

    public ExecutionStepState(Guid id, CollectorRuntimeStep step, DateTime queuedAt)
    {
        Id = id;
        ExecutionPlanStepId = step.ExecutionPlanStepId;
        StrategyCode = step.StrategyCode;
        StrategyVersion = step.StrategyVersion;
        QueueSequence = step.StepSequence;
        QueuedAt = queuedAt;
        Status = ExecutionStepStatus.Pending;
    }

    public Guid Id { get; }
    public Guid ExecutionPlanStepId { get; }
    public string StrategyCode { get; }
    public int StrategyVersion { get; }
    public int? PluginVersion { get; internal set; }
    public ExecutionStepStatus Status { get; private set; }
    public int QueueSequence { get; }
    public DateTime QueuedAt { get; }
    public DateTime? EligibleAt { get; internal set; }
    public DateTime? StartedAt { get; internal set; }
    public DateTime? CompletedAt { get; internal set; }
    public DateTime? CancelledAt { get; internal set; }
    public DateTime? TimedOutAt { get; internal set; }
    public TimeSpan QueueDuration { get; internal set; }
    public TimeSpan WaitDuration { get; internal set; }
    public TimeSpan ExecutionDuration { get; internal set; }
    public TimeSpan TotalDuration { get; internal set; }
    public long BytesCollected { get; internal set; }
    public long ObjectsCollected { get; internal set; }
    public RuntimeFailureCategory FailureCategory { get; internal set; }
    public string? ReasonCode { get; internal set; }
    public string? FailureSummary { get; internal set; }
    public int AttemptCount => attempts.Count;
    public int RetryCount => Math.Max(0, attempts.Count - 1);
    public IReadOnlyList<ExecutionAttemptState> Attempts => attempts.AsReadOnly();

    public void Transition(ExecutionStepStatus next)
    {
        if (!Allowed.TryGetValue(Status, out ExecutionStepStatus[]? nextStates)
            || !nextStates.Contains(next))
            throw new InvalidOperationException("InvalidStepStateTransition");
        Status = next;
    }

    internal void AddAttempt(ExecutionAttemptState attempt) => attempts.Add(attempt);
}

public sealed class ExecutionRunState
{
    private static readonly IReadOnlyDictionary<ExecutionRunStatus, ExecutionRunStatus[]> Allowed =
        new Dictionary<ExecutionRunStatus, ExecutionRunStatus[]>
        {
            [ExecutionRunStatus.Created] = [ExecutionRunStatus.Queued, ExecutionRunStatus.Cancelled, ExecutionRunStatus.Failed],
            [ExecutionRunStatus.Queued] = [ExecutionRunStatus.Running, ExecutionRunStatus.Cancelled, ExecutionRunStatus.Failed],
            [ExecutionRunStatus.Running] = [ExecutionRunStatus.Completed, ExecutionRunStatus.CompletedWithFailures,
                ExecutionRunStatus.Cancelled, ExecutionRunStatus.Failed]
        };
    private readonly List<ExecutionStepState> steps;

    public ExecutionRunState(Guid id, CollectorRuntimeInput input, DateTime createdAt)
    {
        Id = id;
        ManagedServerId = input.ManagedServerId;
        ExecutionPlanId = input.ExecutionPlanId;
        ExecutionPlanSchemaVersion = input.ExecutionPlanSchemaVersion;
        ExecutionStateSchemaVersion = CollectorRuntimeVersions.ExecutionStateSchemaVersion;
        SourceDecisionPlanId = input.SourceDecisionPlanId;
        SourceCapabilitySnapshotId = input.SourceCapabilitySnapshotId;
        SourceInventoryRunId = input.SourceInventoryRunId;
        SourceInventoryVersion = input.SourceInventoryVersion;
        RuntimeVersion = CollectorRuntimeVersions.RuntimeVersion;
        CreatedAt = createdAt;
        QueuedAt = createdAt;
        Status = ExecutionRunStatus.Created;
        steps = input.Steps.OrderBy(x => x.StepSequence)
            .Select(x => new ExecutionStepState(Guid.NewGuid(), x, createdAt)).ToList();
    }

    public Guid Id { get; }
    public Guid ManagedServerId { get; }
    public Guid ExecutionPlanId { get; }
    public int ExecutionPlanSchemaVersion { get; }
    public int ExecutionStateSchemaVersion { get; }
    public Guid SourceDecisionPlanId { get; }
    public Guid SourceCapabilitySnapshotId { get; }
    public Guid SourceInventoryRunId { get; }
    public long SourceInventoryVersion { get; }
    public string RuntimeVersion { get; }
    public ExecutionRunStatus Status { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime QueuedAt { get; }
    public DateTime? StartedAt { get; internal set; }
    public DateTime? CompletedAt { get; internal set; }
    public DateTime? CancelledAt { get; internal set; }
    public TimeSpan TotalDuration { get; internal set; }
    public RuntimeFailureCategory FailureCategory { get; internal set; }
    public string? ReasonCode { get; internal set; }
    public string? FailureSummary { get; internal set; }
    public IReadOnlyList<ExecutionStepState> Steps => new ReadOnlyCollection<ExecutionStepState>(steps);
    public int StepCount => steps.Count;
    public int AttemptCount => steps.Sum(x => x.AttemptCount);
    public int RetryCount => steps.Sum(x => x.RetryCount);
    public long BytesCollected => CheckedSum(steps.Where(x => x.Status == ExecutionStepStatus.Completed).Select(x => x.BytesCollected));
    public long ObjectsCollected => CheckedSum(steps.Where(x => x.Status == ExecutionStepStatus.Completed).Select(x => x.ObjectsCollected));

    public void Transition(ExecutionRunStatus next)
    {
        if (!Allowed.TryGetValue(Status, out ExecutionRunStatus[]? nextStates)
            || !nextStates.Contains(next))
            throw new InvalidOperationException("InvalidRunStateTransition");
        Status = next;
    }

    private static long CheckedSum(IEnumerable<long> values)
    {
        long total = 0;
        foreach (long value in values)
            total = checked(total + value);
        return total;
    }
}

public interface IExecutionStateStore
{
    Task CreateAsync(ExecutionRunState run, CancellationToken cancellationToken);
    Task SaveAsync(ExecutionRunState run, CancellationToken cancellationToken);
}

public sealed record CollectorRuntimeResult(
    ExecutionRunState Run,
    RuntimeFailureCategory EventFailureCategory,
    IReadOnlyDictionary<Guid, PSMOperationsPlatform.CollectorSdk.ExecutionArtifacts>?
        ArtifactMetadata = null);
