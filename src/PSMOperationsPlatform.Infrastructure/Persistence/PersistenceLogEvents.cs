using Microsoft.Extensions.Logging;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

internal static class PersistenceLogEvents
{
    internal static readonly EventId ContextCreated =
        new(2100, "PersistenceContextCreated");

    internal static readonly EventId SaveStarted =
        new(2101, "PersistenceSaveStarted");

    internal static readonly EventId SaveSucceeded =
        new(2102, "PersistenceSaveSucceeded");

    internal static readonly EventId ConcurrencyConflict =
        new(2103, "PersistenceConcurrencyConflict");

    internal static readonly EventId ConstraintViolation =
        new(2104, "PersistenceConstraintViolation");

    internal static readonly EventId Unavailable =
        new(2105, "PersistenceUnavailable");

    internal static readonly EventId AppendOnlyViolation =
        new(2106, "PersistenceAppendOnlyViolation");

    internal static readonly EventId UnexpectedFailure =
        new(2107, "PersistenceUnexpectedFailure");
}
