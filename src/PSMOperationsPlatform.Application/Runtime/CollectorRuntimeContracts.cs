using System.Collections.ObjectModel;
using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.CollectorSdk;

namespace PSMOperationsPlatform.Application.Runtime;

public static class CollectorRuntimeVersions
{
    public const int ExecutionStateSchemaVersion = 1;
    public const int DescriptorSchemaVersion = 1;
    public const string RuntimeVersion = "1.0";
}

public static class ExecutionEventSchemaVersion { public const int Value = 1; }
public static class ExecutionArtifactSchemaVersion { public const int Value = 1; }

public enum ExecutionRunStatus
{
    Created = 1, Queued = 2, Running = 3, Completed = 4,
    CompletedWithFailures = 5, Failed = 6, Cancelled = 7
}

public enum ExecutionStepStatus
{
    Pending = 1, Queued = 2, WaitingForDependency = 3,
    WaitingForThrottle = 4, Running = 5, Completed = 6,
    Failed = 7, Cancelled = 8, TimedOut = 9, Skipped = 10
}

public enum ExecutionAttemptStatus { Running = 1, Completed = 2, Failed = 3, Cancelled = 4, TimedOut = 5 }
public enum ExecutionDispatchDisposition { Submitted = 1, Rejected = 2, Cancelled = 3, Failed = 4 }

public enum RuntimeFailureCategory
{
    None = 0, ExecutionPlanUnavailable = 1, ExecutionPlanInvalid = 2,
    UnsupportedPlanSchema = 3, RuntimeInputInvalid = 4,
    ExecutionPolicyNotFound = 5, ExecutionPolicyVersionUnsupported = 6,
    HandlerNotFound = 7, HandlerDescriptorInvalid = 8,
    HandlerSubjectMismatch = 9, HandlerReadOnlyViolation = 10,
    HandlerContractFailure = 11, HandlerExecutionFailure = 12,
    Timeout = 13, DependencyFailure = 14, ThrottlingFailure = 15,
    StatePersistenceFailure = 16, EventPublicationFailure = 17,
    Cancellation = 18, Unexpected = 19
}

public enum DispatchFailureCategory
{
    None = 0, DispatchRequestInvalid = 1, ExecutionPlanStepInvalid = 2,
    HandlerNotFound = 3, DuplicateHandlerRegistration = 4,
    HandlerDescriptorInvalid = 5, HandlerSubjectMismatch = 6,
    HandlerReadOnlyViolation = 7, PluginCapabilityMismatch = 8,
    TimeoutCapabilityUnsupported = 9, RetryCapabilityUnsupported = 10,
    ParallelCapabilityUnsupported = 11, BatchCapabilityUnsupported = 12,
    CancellationCapabilityUnsupported = 13, ExecutionPolicyNotFound = 14,
    ExecutionPolicyVersionUnsupported = 15, ExecutionPolicyInvalid = 16,
    DispatchPreparationFailure = 17, DispatchSubmissionFailure = 18,
    EventPublicationFailure = 19, Cancellation = 20, Unexpected = 21,
    PluginSdkVersionInvalid = 22, PluginSdkVersionUnsupported = 23,
    RuntimePluginCompatibilityFailure = 24, PluginValidationFailure = 25,
    ArtifactContractFailure = 26
}

public static class CollectorRuntimeReasonCodes
{
    public const string RunCreated = nameof(RunCreated);
    public const string RunStarted = nameof(RunStarted);
    public const string RunCompleted = nameof(RunCompleted);
    public const string RunCompletedWithFailures = nameof(RunCompletedWithFailures);
    public const string RunCancelled = nameof(RunCancelled);
    public const string StepQueued = nameof(StepQueued);
    public const string StepStarted = nameof(StepStarted);
    public const string StepCompleted = nameof(StepCompleted);
    public const string StepFailed = nameof(StepFailed);
    public const string StepTimedOut = nameof(StepTimedOut);
    public const string StepCancelled = nameof(StepCancelled);
    public const string DependencyFailed = nameof(DependencyFailed);
    public const string RetryScheduled = nameof(RetryScheduled);
    public const string InvalidRuntimeInput = nameof(InvalidRuntimeInput);
    public const string InvalidHandlerResult = nameof(InvalidHandlerResult);
}

public sealed record CollectorRuntimeStep(
    Guid ExecutionPlanStepId,
    string StrategyCode,
    int StrategyVersion,
    DecisionSubject Subject,
    int StepSequence,
    int Priority,
    int ExecutionOrder,
    string ParallelGroupCode,
    string TimeoutPolicyCode,
    int TimeoutPolicyVersion,
    string RetryPolicyCode,
    int RetryPolicyVersion,
    ThrottlingClass ThrottlingClass,
    string? BatchGroupCode,
    bool IsReadOnly,
    bool RequiresManualApproval,
    IReadOnlyList<string> DependencyStrategyCodes);

public sealed record CollectorRuntimeExclusion(string StrategyCode, PlanningDisposition Disposition, string ReasonCode);

public sealed record CollectorRuntimeInput(
    Guid ManagedServerId,
    string? TargetFqdn,
    Guid ExecutionPlanId,
    int ExecutionPlanSchemaVersion,
    ExecutionPlanStatus PlanStatus,
    Guid SourceDecisionPlanId,
    Guid SourceCapabilitySnapshotId,
    Guid SourceInventoryRunId,
    long SourceInventoryVersion,
    DateTime PlanCreatedAt,
    DateTime RequestedAt,
    string? CorrelationId,
    IReadOnlyList<CollectorRuntimeStep> Steps,
    IReadOnlyList<CollectorRuntimeExclusion> Exclusions);

public enum ExecutionEventType
{
    ExecutionRunCreated = 1, ExecutionRunStarted = 2,
    ExecutionStepQueued = 3, ExecutionStepWaiting = 4,
    ExecutionStepStarted = 5, ExecutionStepAttemptStarted = 6,
    ExecutionStepAttemptCompleted = 7, ExecutionStepRetryScheduled = 8,
    ExecutionStepCompleted = 9, ExecutionStepSkipped = 10,
    ExecutionStepFailed = 11, ExecutionStepTimedOut = 12,
    ExecutionStepCancelled = 13, ExecutionRunCompleted = 14,
    ExecutionRunFailed = 15, ExecutionRunCancelled = 16,
    ExecutionDispatchRequested = 17, ExecutionHandlerResolved = 18,
    ExecutionPolicyResolved = 19, ExecutionDispatchPrepared = 20,
    ExecutionDispatchRejected = 21, ExecutionDispatchSubmitted = 22
}

public sealed record ExecutionEvent(
    Guid EventId,
    int EventSchemaVersion,
    long Sequence,
    ExecutionEventType EventType,
    Guid ManagedServerId,
    Guid ExecutionPlanId,
    Guid ExecutionRunId,
    Guid? ExecutionStepId,
    string? StrategyCode,
    string? PluginId,
    int? PluginVersion,
    int? AttemptNumber,
    DateTime OccurredAt,
    TimeSpan? Duration,
    string Status,
    RuntimeFailureCategory FailureCategory,
    string ReasonCode,
    string Message,
    Guid SourceDecisionPlanId,
    Guid SourceCapabilitySnapshotId,
    Guid SourceInventoryRunId,
    long SourceInventoryVersion);

public interface IExecutionEventSink
{
    Task PublishAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken);
}

public sealed class NullExecutionEventSink : IExecutionEventSink
{
    public Task PublishAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
