using PSMOperationsPlatform.Domain.Common;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class ExecutionRunStateEntity : Entity
{
    public ExecutionRunStateEntity(Guid id, Guid managedServerId, Guid executionPlanId,
        int executionPlanSchemaVersion, int executionStateSchemaVersion,
        Guid sourceDecisionPlanId, Guid sourceCapabilitySnapshotId,
        Guid sourceInventoryRunId, long sourceInventoryVersion, string runtimeVersion,
        string status, DateTime createdAt) : base(id)
    {
        ManagedServerId = Required(managedServerId);
        ExecutionPlanId = Required(executionPlanId);
        ExecutionPlanSchemaVersion = Positive(executionPlanSchemaVersion);
        ExecutionStateSchemaVersion = Positive(executionStateSchemaVersion);
        SourceDecisionPlanId = Required(sourceDecisionPlanId);
        SourceCapabilitySnapshotId = Required(sourceCapabilitySnapshotId);
        SourceInventoryRunId = Required(sourceInventoryRunId);
        SourceInventoryVersion = Positive(sourceInventoryVersion);
        RuntimeVersion = Text(runtimeVersion, 20);
        Status = Text(status, 40);
        CreatedAt = createdAt;
        QueuedAt = createdAt;
    }
    private ExecutionRunStateEntity() { RuntimeVersion = Status = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid ExecutionPlanId { get; private set; }
    public int ExecutionPlanSchemaVersion { get; private set; }
    public int ExecutionStateSchemaVersion { get; private set; }
    public Guid SourceDecisionPlanId { get; private set; }
    public Guid SourceCapabilitySnapshotId { get; private set; }
    public Guid SourceInventoryRunId { get; private set; }
    public long SourceInventoryVersion { get; private set; }
    public string RuntimeVersion { get; private set; }
    public string Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime QueuedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public long TotalDurationTicks { get; private set; }
    public int StepCount { get; private set; }
    public int AttemptCount { get; private set; }
    public int RetryCount { get; private set; }
    public long BytesCollected { get; private set; }
    public long ObjectsCollected { get; private set; }
    public string FailureCategory { get; private set; } = "None";
    public string? ReasonCode { get; private set; }
    public string? FailureSummary { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public ICollection<ExecutionStepStateEntity> Steps { get; private set; } = [];

    public void Update(string status, DateTime? startedAt, DateTime? completedAt,
        DateTime? cancelledAt, long totalDurationTicks, int stepCount, int attemptCount,
        int retryCount, long bytesCollected, long objectsCollected, string failureCategory,
        string? reasonCode, string? failureSummary)
    {
        Status = Text(status, 40); StartedAt = startedAt; CompletedAt = completedAt;
        CancelledAt = cancelledAt; TotalDurationTicks = NonNegative(totalDurationTicks);
        StepCount = NonNegative(stepCount); AttemptCount = NonNegative(attemptCount);
        RetryCount = NonNegative(retryCount); BytesCollected = NonNegative(bytesCollected);
        ObjectsCollected = NonNegative(objectsCollected);
        FailureCategory = Text(failureCategory, 60); ReasonCode = Optional(reasonCode, 100);
        FailureSummary = Optional(failureSummary, 500);
    }
    private static Guid Required(Guid value) => value == Guid.Empty ? throw new ArgumentException(nameof(value)) : value;
    private static int Positive(int value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static long Positive(long value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static int NonNegative(int value) => value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static long NonNegative(long value) => value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static string Text(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Length > max ? throw new ArgumentException(nameof(value)) : value;
    private static string? Optional(string? value, int max) => value is null ? null : Text(value, max);
}

public sealed class ExecutionStepStateEntity : Entity
{
    public ExecutionStepStateEntity(Guid id, Guid executionRunId, Guid executionPlanStepId,
        string strategyCode, int strategyVersion, int queueSequence, DateTime queuedAt) : base(id)
    {
        ExecutionRunId = executionRunId; ExecutionPlanStepId = executionPlanStepId;
        StrategyCode = strategyCode; StrategyVersion = strategyVersion;
        QueueSequence = queueSequence; QueuedAt = queuedAt; Status = "Pending";
    }
    private ExecutionStepStateEntity() { StrategyCode = Status = null!; }
    public Guid ExecutionRunId { get; private set; }
    public Guid ExecutionPlanStepId { get; private set; }
    public string StrategyCode { get; private set; }
    public int StrategyVersion { get; private set; }
    public int? PluginVersion { get; private set; }
    public string Status { get; private set; }
    public int QueueSequence { get; private set; }
    public DateTime QueuedAt { get; private set; }
    public DateTime? EligibleAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public DateTime? TimedOutAt { get; private set; }
    public long QueueDurationTicks { get; private set; }
    public long WaitDurationTicks { get; private set; }
    public long ExecutionDurationTicks { get; private set; }
    public long TotalDurationTicks { get; private set; }
    public int AttemptCount { get; private set; }
    public int RetryCount { get; private set; }
    public long BytesCollected { get; private set; }
    public long ObjectsCollected { get; private set; }
    public string FailureCategory { get; private set; } = "None";
    public string? ReasonCode { get; private set; }
    public string? FailureSummary { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public ICollection<ExecutionAttemptStateEntity> Attempts { get; private set; } = [];

    public void Update(int? pluginVersion, string status, DateTime? eligibleAt,
        DateTime? startedAt, DateTime? completedAt, DateTime? cancelledAt,
        DateTime? timedOutAt, long queueTicks, long waitTicks, long executionTicks,
        long totalTicks, int attempts, int retries, long bytes, long objects,
        string failureCategory, string? reasonCode, string? failureSummary)
    {
        PluginVersion = pluginVersion; Status = status; EligibleAt = eligibleAt;
        StartedAt = startedAt; CompletedAt = completedAt; CancelledAt = cancelledAt;
        TimedOutAt = timedOutAt; QueueDurationTicks = queueTicks; WaitDurationTicks = waitTicks;
        ExecutionDurationTicks = executionTicks; TotalDurationTicks = totalTicks;
        AttemptCount = attempts; RetryCount = retries; BytesCollected = bytes;
        ObjectsCollected = objects; FailureCategory = failureCategory;
        ReasonCode = reasonCode; FailureSummary = failureSummary;
    }
}

public sealed class ExecutionAttemptStateEntity : Entity
{
    public ExecutionAttemptStateEntity(Guid id, Guid executionStepStateId,
        int attemptNumber, bool isRetry, DateTime startedAt) : base(id)
    {
        ExecutionStepStateId = executionStepStateId; AttemptNumber = attemptNumber;
        IsRetry = isRetry; StartedAt = startedAt; Status = "Running";
    }
    private ExecutionAttemptStateEntity() { Status = null!; }
    public Guid ExecutionStepStateId { get; private set; }
    public int AttemptNumber { get; private set; }
    public bool IsRetry { get; private set; }
    public string Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public long DurationTicks { get; private set; }
    public long? BytesCollected { get; private set; }
    public long? ObjectsCollected { get; private set; }
    public string FailureCategory { get; private set; } = "None";
    public string? ReasonCode { get; private set; }
    public string? FailureSummary { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public void Update(string status, DateTime? completedAt, long durationTicks,
        long? bytes, long? objects, string failureCategory, string? reasonCode,
        string? failureSummary)
    {
        Status = status; CompletedAt = completedAt; DurationTicks = durationTicks;
        BytesCollected = bytes; ObjectsCollected = objects;
        FailureCategory = failureCategory; ReasonCode = reasonCode;
        FailureSummary = failureSummary;
    }
}
