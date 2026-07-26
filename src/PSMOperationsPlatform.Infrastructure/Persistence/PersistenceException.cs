namespace PSMOperationsPlatform.Infrastructure.Persistence;

public abstract class PersistenceException(
    string code,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

public sealed class PersistenceConcurrencyException(Exception innerException)
    : PersistenceException(
        "persistence.concurrency_conflict",
        "The record was changed by another operation.",
        innerException);

public sealed class PersistenceConflictException(Exception innerException)
    : PersistenceException(
        "persistence.unique_conflict",
        "A record with the same unique identity already exists.",
        innerException);

public sealed class PersistenceConstraintException(Exception innerException)
    : PersistenceException(
        "persistence.constraint_violation",
        "The requested change violates a database constraint.",
        innerException);

public sealed class PersistenceAppendOnlyViolationException(
    string entityType,
    string prohibitedState)
    : PersistenceException(
        "persistence.append_only_violation",
        $"Append-only entity '{entityType}' cannot be saved in '{prohibitedState}' state.");

public sealed class PersistenceUnavailableException(Exception innerException)
    : PersistenceException(
        "persistence.unavailable",
        "The persistence service is currently unavailable.",
        innerException);
