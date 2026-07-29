using PSMOperationsPlatform.Domain.Common;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class ExecutionRunHistoryEntity : Entity
{
    public ExecutionRunHistoryEntity(Guid executionRunId, Guid executionPlanId,
        Guid managedServerId, int historySchemaVersion, DateTime queuedAt,
        DateTime? startedAt, DateTime completedAt, long durationTicks,
        DateTime recordedAt, string outcome, string terminalState,
        string failureCategory, string? reasonCode, int warningCount,
        int attemptCount, int retryCount, int stepCount, int completedSteps,
        int failedSteps, int timedOutSteps, int cancelledSteps, int skippedSteps,
        string strategyCode, int strategyVersion, string pluginId,
        int pluginVersion, string targetSdkVersion, string runtimeVersion,
        int planSchemaVersion, int stateSchemaVersion, int eventSchemaVersion,
        int monitoringSchemaVersion, string subject, bool isReadOnly,
        int fileCount, int objectCount, int metricCount, long byteCount,
        string projectionStatus, string projectionFailureCategory,
        string projectionReasonCode, Guid sourceDecisionPlanId,
        Guid sourceCapabilitySnapshotId, Guid sourceInventoryRunId,
        long sourceInventoryVersion) : base(executionRunId)
    {
        ExecutionRunId = executionRunId; ExecutionPlanId = executionPlanId;
        ManagedServerId = managedServerId; HistorySchemaVersion = historySchemaVersion;
        QueuedAt = queuedAt; StartedAt = startedAt; CompletedAt = completedAt;
        DurationTicks = durationTicks; RecordedAt = recordedAt; ExecutionOutcome = outcome;
        TerminalState = terminalState; FailureCategory = failureCategory;
        ReasonCode = reasonCode; WarningCount = warningCount; AttemptCount = attemptCount;
        RetryCount = retryCount; StepCount = stepCount; CompletedStepCount = completedSteps;
        FailedStepCount = failedSteps; TimedOutStepCount = timedOutSteps;
        CancelledStepCount = cancelledSteps; SkippedStepCount = skippedSteps;
        StrategyCode = strategyCode; StrategyVersion = strategyVersion;
        PluginId = pluginId; PluginVersion = pluginVersion;
        TargetSdkVersion = targetSdkVersion; RuntimeContractVersion = runtimeVersion;
        ExecutionPlanSchemaVersion = planSchemaVersion;
        ExecutionStateSchemaVersion = stateSchemaVersion;
        ExecutionEventSchemaVersion = eventSchemaVersion;
        ExecutionMonitoringSchemaVersion = monitoringSchemaVersion;
        Subject = subject; IsReadOnly = isReadOnly; ArtifactFileCount = fileCount;
        ArtifactObjectCount = objectCount; ArtifactMetricCount = metricCount;
        ArtifactByteCount = byteCount; ProjectionStatus = projectionStatus;
        ProjectionFailureCategory = projectionFailureCategory;
        ProjectionReasonCode = projectionReasonCode;
        SourceDecisionPlanId = sourceDecisionPlanId;
        SourceCapabilitySnapshotId = sourceCapabilitySnapshotId;
        SourceInventoryRunId = sourceInventoryRunId;
        SourceInventoryVersion = sourceInventoryVersion;
    }
    private ExecutionRunHistoryEntity()
    {
        ExecutionOutcome = TerminalState = FailureCategory = StrategyCode = PluginId =
            TargetSdkVersion = RuntimeContractVersion = Subject = ProjectionStatus =
            ProjectionFailureCategory = ProjectionReasonCode = null!;
    }

    public Guid ExecutionRunId { get; private set; }
    public Guid ExecutionPlanId { get; private set; }
    public Guid ManagedServerId { get; private set; }
    public Guid SourceDecisionPlanId { get; private set; }
    public Guid SourceCapabilitySnapshotId { get; private set; }
    public Guid SourceInventoryRunId { get; private set; }
    public long SourceInventoryVersion { get; private set; }
    public int HistorySchemaVersion { get; private set; }
    public DateTime QueuedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime CompletedAt { get; private set; }
    public long DurationTicks { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public string ExecutionOutcome { get; private set; }
    public string TerminalState { get; private set; }
    public string FailureCategory { get; private set; }
    public string? ReasonCode { get; private set; }
    public int WarningCount { get; private set; }
    public int AttemptCount { get; private set; }
    public int RetryCount { get; private set; }
    public int StepCount { get; private set; }
    public int CompletedStepCount { get; private set; }
    public int FailedStepCount { get; private set; }
    public int TimedOutStepCount { get; private set; }
    public int CancelledStepCount { get; private set; }
    public int SkippedStepCount { get; private set; }
    public string StrategyCode { get; private set; }
    public int StrategyVersion { get; private set; }
    public string PluginId { get; private set; }
    public int PluginVersion { get; private set; }
    public string TargetSdkVersion { get; private set; }
    public string RuntimeContractVersion { get; private set; }
    public int ExecutionPlanSchemaVersion { get; private set; }
    public int ExecutionStateSchemaVersion { get; private set; }
    public int ExecutionEventSchemaVersion { get; private set; }
    public int ExecutionMonitoringSchemaVersion { get; private set; }
    public string Subject { get; private set; }
    public bool IsReadOnly { get; private set; }
    public int ArtifactFileCount { get; private set; }
    public int ArtifactObjectCount { get; private set; }
    public int ArtifactMetricCount { get; private set; }
    public long ArtifactByteCount { get; private set; }
    public string ProjectionStatus { get; private set; }
    public string ProjectionFailureCategory { get; private set; }
    public string ProjectionReasonCode { get; private set; }
}

public sealed class ExecutionStepHistoryEntity : Entity
{
    public ExecutionStepHistoryEntity(Guid id, Guid executionRunId,
        Guid executionStepId, int historySchemaVersion, int stepOrdinal,
        int dependencyCount, string strategyCode, int strategyVersion,
        string pluginId, int pluginVersion, string subject, DateTime queuedAt,
        DateTime? startedAt, DateTime? completedAt, long queueTicks, long waitTicks,
        long executionTicks, string outcome, string failureCategory,
        string? reasonCode, int attemptCount, int retryCount, bool throttled,
        bool skipped, bool cancelled, bool timedOut, int fileCount,
        int objectCount, int metricCount, long byteCount, int warningCount) : base(id)
    {
        ExecutionRunId = executionRunId; ExecutionStepId = executionStepId;
        HistorySchemaVersion = historySchemaVersion; StepOrdinal = stepOrdinal;
        DependencyCount = dependencyCount; StrategyCode = strategyCode;
        StrategyVersion = strategyVersion; PluginId = pluginId;
        PluginVersion = pluginVersion; Subject = subject; QueuedAt = queuedAt;
        StartedAt = startedAt; CompletedAt = completedAt; QueueDurationTicks = queueTicks;
        WaitDurationTicks = waitTicks; ExecutionDurationTicks = executionTicks;
        StepOutcome = outcome; FailureCategory = failureCategory; ReasonCode = reasonCode;
        AttemptCount = attemptCount; RetryCount = retryCount; WasThrottled = throttled;
        WasSkipped = skipped; WasCancelled = cancelled; WasTimedOut = timedOut;
        ArtifactFileCount = fileCount; ArtifactObjectCount = objectCount;
        ArtifactMetricCount = metricCount; ArtifactByteCount = byteCount;
        WarningCount = warningCount;
    }
    private ExecutionStepHistoryEntity()
    {
        StrategyCode = PluginId = Subject = StepOutcome = FailureCategory = null!;
    }
    public Guid ExecutionRunId { get; private set; }
    public Guid ExecutionStepId { get; private set; }
    public int HistorySchemaVersion { get; private set; }
    public int StepOrdinal { get; private set; }
    public int DependencyCount { get; private set; }
    public string StrategyCode { get; private set; }
    public int StrategyVersion { get; private set; }
    public string PluginId { get; private set; }
    public int PluginVersion { get; private set; }
    public string Subject { get; private set; }
    public DateTime QueuedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public long QueueDurationTicks { get; private set; }
    public long WaitDurationTicks { get; private set; }
    public long ExecutionDurationTicks { get; private set; }
    public string StepOutcome { get; private set; }
    public string FailureCategory { get; private set; }
    public string? ReasonCode { get; private set; }
    public int AttemptCount { get; private set; }
    public int RetryCount { get; private set; }
    public bool WasThrottled { get; private set; }
    public bool WasSkipped { get; private set; }
    public bool WasCancelled { get; private set; }
    public bool WasTimedOut { get; private set; }
    public int ArtifactFileCount { get; private set; }
    public int ArtifactObjectCount { get; private set; }
    public int ArtifactMetricCount { get; private set; }
    public long ArtifactByteCount { get; private set; }
    public int WarningCount { get; private set; }
}

public sealed class ExecutionAttemptHistoryEntity : Entity
{
    public ExecutionAttemptHistoryEntity(Guid id, Guid executionRunId,
        Guid executionStepId, int historySchemaVersion, int attemptNumber,
        DateTime startedAt, DateTime? completedAt, long durationTicks,
        string outcome, string failureCategory, string? reasonCode,
        bool retryScheduled, long? retryDelayTicks, bool cancellationObserved,
        bool timeoutObserved, int warningCount) : base(id)
    {
        ExecutionRunId = executionRunId; ExecutionStepId = executionStepId;
        HistorySchemaVersion = historySchemaVersion; AttemptNumber = attemptNumber;
        StartedAt = startedAt; CompletedAt = completedAt; DurationTicks = durationTicks;
        AttemptOutcome = outcome; FailureCategory = failureCategory; ReasonCode = reasonCode;
        RetryScheduled = retryScheduled; RetryDelayTicks = retryDelayTicks;
        CancellationObserved = cancellationObserved; TimeoutObserved = timeoutObserved;
        WarningCount = warningCount;
    }
    private ExecutionAttemptHistoryEntity()
    {
        AttemptOutcome = FailureCategory = null!;
    }
    public Guid ExecutionRunId { get; private set; }
    public Guid ExecutionStepId { get; private set; }
    public int HistorySchemaVersion { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public long DurationTicks { get; private set; }
    public string AttemptOutcome { get; private set; }
    public string FailureCategory { get; private set; }
    public string? ReasonCode { get; private set; }
    public bool RetryScheduled { get; private set; }
    public long? RetryDelayTicks { get; private set; }
    public bool CancellationObserved { get; private set; }
    public bool TimeoutObserved { get; private set; }
    public int WarningCount { get; private set; }
}

public sealed class ExecutionStateTransitionHistoryEntity : Entity
{
    public ExecutionStateTransitionHistoryEntity(Guid id, Guid executionRunId,
        Guid? executionStepId, int historySchemaVersion, long sequence,
        string entityType, string? fromState, string toState,
        DateTime transitionedAt, string eventType, string reasonCode,
        string failureCategory, int eventSchemaVersion) : base(id)
    {
        ExecutionRunId = executionRunId; ExecutionStepId = executionStepId;
        HistorySchemaVersion = historySchemaVersion; TransitionSequence = sequence;
        EntityType = entityType; FromState = fromState; ToState = toState;
        TransitionedAt = transitionedAt; EventType = eventType;
        ReasonCode = reasonCode; FailureCategory = failureCategory;
        EventSchemaVersion = eventSchemaVersion;
    }
    private ExecutionStateTransitionHistoryEntity()
    {
        EntityType = ToState = EventType = ReasonCode = FailureCategory = null!;
    }
    public Guid ExecutionRunId { get; private set; }
    public Guid? ExecutionStepId { get; private set; }
    public int HistorySchemaVersion { get; private set; }
    public long TransitionSequence { get; private set; }
    public string EntityType { get; private set; }
    public string? FromState { get; private set; }
    public string ToState { get; private set; }
    public DateTime TransitionedAt { get; private set; }
    public string EventType { get; private set; }
    public string ReasonCode { get; private set; }
    public string FailureCategory { get; private set; }
    public int EventSchemaVersion { get; private set; }
}

public sealed class ExecutionArtifactHistoryEntity : Entity
{
    public ExecutionArtifactHistoryEntity(Guid id, Guid executionRunId,
        Guid executionStepId, int historySchemaVersion, string artifactId,
        int artifactSchemaVersion, string artifactType, string logicalName,
        string? contentType, long? objectCount, long? metricCount,
        long byteCount, DateTime createdAt) : base(id)
    {
        ExecutionRunId = executionRunId; ExecutionStepId = executionStepId;
        HistorySchemaVersion = historySchemaVersion; ArtifactId = artifactId;
        ArtifactSchemaVersion = artifactSchemaVersion; ArtifactType = artifactType;
        LogicalName = logicalName; ContentType = contentType; ObjectCount = objectCount;
        MetricCount = metricCount; ByteCount = byteCount; CreatedAt = createdAt;
    }
    private ExecutionArtifactHistoryEntity()
    {
        ArtifactId = ArtifactType = LogicalName = null!;
    }
    public Guid ExecutionRunId { get; private set; }
    public Guid ExecutionStepId { get; private set; }
    public int HistorySchemaVersion { get; private set; }
    public string ArtifactId { get; private set; }
    public int ArtifactSchemaVersion { get; private set; }
    public string ArtifactType { get; private set; }
    public string LogicalName { get; private set; }
    public string? ContentType { get; private set; }
    public long? ObjectCount { get; private set; }
    public long? MetricCount { get; private set; }
    public long ByteCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

public sealed class ExecutionPolicyHistoryEntity : Entity
{
    public ExecutionPolicyHistoryEntity(Guid id, Guid executionRunId,
        Guid executionStepId, int historySchemaVersion, string timeoutCode,
        int timeoutVersion, long timeoutTicks, string retryCode, int retryVersion,
        int maximumAttempts, string retryDelayClassification, string parallelCode,
        int parallelVersion, int parallelConcurrency, string throttlingCode,
        int throttlingVersion, int throttlingConcurrency, string batchingCode,
        int batchingVersion, bool batchingEnabled) : base(id)
    {
        ExecutionRunId = executionRunId; ExecutionStepId = executionStepId;
        HistorySchemaVersion = historySchemaVersion; TimeoutPolicyCode = timeoutCode;
        TimeoutPolicyVersion = timeoutVersion; TimeoutTicks = timeoutTicks;
        RetryPolicyCode = retryCode; RetryPolicyVersion = retryVersion;
        MaximumAttempts = maximumAttempts;
        RetryDelayClassification = retryDelayClassification;
        ParallelPolicyCode = parallelCode; ParallelPolicyVersion = parallelVersion;
        ParallelMaximumConcurrency = parallelConcurrency;
        ThrottlingPolicyCode = throttlingCode;
        ThrottlingPolicyVersion = throttlingVersion;
        ThrottlingMaximumConcurrency = throttlingConcurrency;
        BatchingPolicyCode = batchingCode; BatchingPolicyVersion = batchingVersion;
        BatchingEnabled = batchingEnabled;
    }
    private ExecutionPolicyHistoryEntity()
    {
        TimeoutPolicyCode = RetryPolicyCode = RetryDelayClassification =
            ParallelPolicyCode = ThrottlingPolicyCode = BatchingPolicyCode = null!;
    }
    public Guid ExecutionRunId { get; private set; }
    public Guid ExecutionStepId { get; private set; }
    public int HistorySchemaVersion { get; private set; }
    public string TimeoutPolicyCode { get; private set; }
    public int TimeoutPolicyVersion { get; private set; }
    public long TimeoutTicks { get; private set; }
    public string RetryPolicyCode { get; private set; }
    public int RetryPolicyVersion { get; private set; }
    public int MaximumAttempts { get; private set; }
    public string RetryDelayClassification { get; private set; }
    public string ParallelPolicyCode { get; private set; }
    public int ParallelPolicyVersion { get; private set; }
    public int ParallelMaximumConcurrency { get; private set; }
    public string ThrottlingPolicyCode { get; private set; }
    public int ThrottlingPolicyVersion { get; private set; }
    public int ThrottlingMaximumConcurrency { get; private set; }
    public string BatchingPolicyCode { get; private set; }
    public int BatchingPolicyVersion { get; private set; }
    public bool BatchingEnabled { get; private set; }
}
