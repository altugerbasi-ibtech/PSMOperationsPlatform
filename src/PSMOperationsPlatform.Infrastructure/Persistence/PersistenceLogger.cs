using Microsoft.Extensions.Logging;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

internal static class PersistenceLogger
{
    internal static void ContextCreated(ILogger? logger)
    {
        logger?.LogDebug(
            PersistenceLogEvents.ContextCreated,
            "Persistence context created.");
    }

    internal static void SaveStarted(
        ILogger? logger,
        string operationType,
        int affectedEntryCount)
    {
        logger?.LogDebug(
            PersistenceLogEvents.SaveStarted,
            "Persistence save started. OperationType={OperationType} AffectedEntryCount={AffectedEntryCount}",
            operationType,
            affectedEntryCount);
    }

    internal static void SaveSucceeded(
        ILogger? logger,
        string operationType,
        int affectedRowCount)
    {
        logger?.LogDebug(
            PersistenceLogEvents.SaveSucceeded,
            "Persistence save succeeded. OperationType={OperationType} AffectedRowCount={AffectedRowCount}",
            operationType,
            affectedRowCount);
    }

    internal static void SaveFailed(
        ILogger? logger,
        string operationType,
        Exception exception)
    {
        (LogLevel level, EventId eventId, string errorCode) = exception switch
        {
            PersistenceConcurrencyException concurrency =>
                (LogLevel.Warning, PersistenceLogEvents.ConcurrencyConflict, concurrency.Code),
            PersistenceConflictException conflict =>
                (LogLevel.Warning, PersistenceLogEvents.ConstraintViolation, conflict.Code),
            PersistenceConstraintException constraint =>
                (LogLevel.Warning, PersistenceLogEvents.ConstraintViolation, constraint.Code),
            PersistenceUnavailableException unavailable =>
                (LogLevel.Error, PersistenceLogEvents.Unavailable, unavailable.Code),
            PersistenceAppendOnlyViolationException appendOnly =>
                (LogLevel.Warning, PersistenceLogEvents.AppendOnlyViolation, appendOnly.Code),
            _ =>
                (LogLevel.Error, PersistenceLogEvents.UnexpectedFailure, "persistence.unexpected_failure")
        };

        logger?.Log(
            level,
            eventId,
            exception,
            "Persistence save failed. OperationType={OperationType} ErrorCode={ErrorCode} ExceptionType={ExceptionType}",
            operationType,
            errorCode,
            exception.GetType().Name);
    }
}
