using Microsoft.Extensions.Logging;

namespace PSMOperationsPlatform.WindowsCollector;

internal static partial class WindowsCollectorLog
{
    internal const int CollectorStartedId = 2300;
    internal const int CollectorStoppingId = 2301;
    internal const int PollingCycleStartedId = 2310;
    internal const int PollingCycleCompletedId = 2311;
    internal const int PollingCycleFailedId = 2312;
    internal const int EligibleTargetsLoadedId = 2320;
    internal const int TargetLoadFailedId = 2321;
    internal const int TargetProbeSucceededId = 2330;
    internal const int TargetProbeFailedId = 2331;
    internal const int TargetProbeUnexpectedId = 2332;
    internal const int TargetProbeCycleSummaryId = 2333;
    internal const int ConnectivityResultAppliedId = 2340;
    internal const int ConnectivityResultSkippedId = 2341;
    internal const int ConnectivityConcurrencyConflictId = 2342;
    internal const int ConnectivityResultPersistFailedId = 2343;
    internal const int ConnectivityPersistenceSummaryId = 2344;
    internal const int ConnectivityPersistenceFailuresId = 2345;

    [LoggerMessage(
        EventId = CollectorStartedId,
        EventName = "WindowsCollectorStarted",
        Level = LogLevel.Information,
        Message = "Windows Collector started.")]
    internal static partial void CollectorStarted(ILogger logger);

    [LoggerMessage(
        EventId = CollectorStoppingId,
        EventName = "WindowsCollectorStopping",
        Level = LogLevel.Information,
        Message = "Windows Collector stopping.")]
    internal static partial void CollectorStopping(ILogger logger);

    [LoggerMessage(
        EventId = PollingCycleStartedId,
        EventName = "PollingCycleStarted",
        Level = LogLevel.Debug,
        Message = "Polling cycle started at {StartedAt}.")]
    internal static partial void PollingCycleStarted(
        ILogger logger,
        DateTimeOffset startedAt);

    [LoggerMessage(
        EventId = PollingCycleCompletedId,
        EventName = "PollingCycleCompleted",
        Level = LogLevel.Debug,
        Message = "Polling cycle completed.")]
    internal static partial void PollingCycleCompleted(ILogger logger);

    [LoggerMessage(
        EventId = PollingCycleFailedId,
        EventName = "PollingCycleFailed",
        Level = LogLevel.Warning,
        Message = "Polling cycle failed safely. ExceptionType={ExceptionType}")]
    internal static partial void PollingCycleFailed(
        ILogger logger,
        string exceptionType);

    [LoggerMessage(
        EventId = EligibleTargetsLoadedId,
        EventName = "EligibleTargetsLoaded",
        Level = LogLevel.Debug,
        Message = "Eligible Windows targets loaded. Count={EligibleTargetCount} EvaluationTime={CurrentEvaluationTime} DurationMilliseconds={DurationMilliseconds}")]
    internal static partial void EligibleTargetsLoaded(
        ILogger logger,
        int eligibleTargetCount,
        DateTimeOffset currentEvaluationTime,
        double durationMilliseconds);

    [LoggerMessage(
        EventId = TargetLoadFailedId,
        EventName = "TargetLoadFailed",
        Level = LogLevel.Warning,
        Message = "Eligible Windows target loading failed safely. ExceptionType={ExceptionType}")]
    internal static partial void TargetLoadFailed(
        ILogger logger,
        string exceptionType);

    [LoggerMessage(
        EventId = TargetProbeSucceededId,
        EventName = "WindowsTargetProbeSucceeded",
        Level = LogLevel.Debug,
        Message = "Windows target WinRM probe succeeded. TargetId={TargetId} TransportMode={TransportMode} SuccessfulTransport={SuccessfulTransport} DurationMilliseconds={DurationMilliseconds} AttemptCount={AttemptCount}")]
    internal static partial void TargetProbeSucceeded(
        ILogger logger,
        Guid targetId,
        string transportMode,
        string successfulTransport,
        double durationMilliseconds,
        int attemptCount);

    [LoggerMessage(
        EventId = TargetProbeFailedId,
        EventName = "WindowsTargetProbeFailed",
        Level = LogLevel.Debug,
        Message = "Windows target WinRM probe failed safely. TargetId={TargetId} TransportMode={TransportMode} FailureCategory={FailureCategory} DurationMilliseconds={DurationMilliseconds} AttemptCount={AttemptCount}")]
    internal static partial void TargetProbeFailed(
        ILogger logger,
        Guid targetId,
        string transportMode,
        string failureCategory,
        double durationMilliseconds,
        int attemptCount);

    [LoggerMessage(
        EventId = TargetProbeUnexpectedId,
        EventName = "WindowsTargetProbeUnexpected",
        Level = LogLevel.Error,
        Message = "Windows target probe implementation failed safely. TargetId={TargetId} TransportMode={TransportMode} ExceptionType={ExceptionType}")]
    internal static partial void TargetProbeUnexpected(
        ILogger logger,
        Guid targetId,
        string transportMode,
        string exceptionType);

    [LoggerMessage(
        EventId = TargetProbeCycleSummaryId,
        EventName = "WindowsTargetProbeCycleSummary",
        Level = LogLevel.Debug,
        Message = "Windows target probe cycle summary. ResultCount={ResultCount} ReachableCount={ReachableCount} UnreachableCount={UnreachableCount}")]
    internal static partial void TargetProbeCycleSummary(
        ILogger logger,
        int resultCount,
        int reachableCount,
        int unreachableCount);

    [LoggerMessage(
        EventId = ConnectivityResultAppliedId,
        EventName = "ConnectivityResultApplied",
        Level = LogLevel.Debug,
        Message = "Connectivity result applied. TargetId={TargetId} PersistenceOutcome={PersistenceOutcome} ConnectivityState={ConnectivityState} FailureCount={FailureCount} NextEligibleAttemptAt={NextEligibleAttemptAt}")]
    internal static partial void ConnectivityResultApplied(
        ILogger logger,
        Guid targetId,
        string persistenceOutcome,
        string connectivityState,
        int failureCount,
        DateTime nextEligibleAttemptAt);

    [LoggerMessage(
        EventId = ConnectivityResultSkippedId,
        EventName = "ConnectivityResultSkipped",
        Level = LogLevel.Debug,
        Message = "Connectivity result skipped safely. TargetId={TargetId} PersistenceOutcome={PersistenceOutcome}")]
    internal static partial void ConnectivityResultSkipped(
        ILogger logger,
        Guid targetId,
        string persistenceOutcome);

    [LoggerMessage(
        EventId = ConnectivityConcurrencyConflictId,
        EventName = "ConnectivityConcurrencyConflict",
        Level = LogLevel.Warning,
        Message = "Connectivity result concurrency retry was exhausted. TargetId={TargetId}")]
    internal static partial void ConnectivityConcurrencyConflict(
        ILogger logger,
        Guid targetId);

    [LoggerMessage(
        EventId = ConnectivityResultPersistFailedId,
        EventName = "ConnectivityResultPersistFailed",
        Level = LogLevel.Debug,
        Message = "Connectivity result persistence failed safely. TargetId={TargetId}")]
    internal static partial void ConnectivityResultPersistFailed(
        ILogger logger,
        Guid targetId);

    [LoggerMessage(
        EventId = ConnectivityPersistenceSummaryId,
        EventName = "ConnectivityCyclePersistenceSummary",
        Level = LogLevel.Debug,
        Message = "Connectivity persistence cycle summary. AppliedCount={AppliedCount} SkippedCount={SkippedCount} FailedCount={FailedCount}")]
    internal static partial void ConnectivityPersistenceSummary(
        ILogger logger,
        int appliedCount,
        int skippedCount,
        int failedCount);

    [LoggerMessage(
        EventId = ConnectivityPersistenceFailuresId,
        EventName = "ConnectivityCyclePersistenceFailures",
        Level = LogLevel.Error,
        Message = "One or more connectivity results could not be persisted. FailedCount={FailedCount}")]
    internal static partial void ConnectivityPersistenceFailures(
        ILogger logger,
        int failedCount);
}
