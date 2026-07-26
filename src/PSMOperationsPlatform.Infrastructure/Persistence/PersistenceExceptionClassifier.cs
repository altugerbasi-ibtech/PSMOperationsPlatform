using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

internal static class PersistenceExceptionClassifier
{
    // These errors have unambiguous on-premises SQL Server connectivity,
    // database availability or timeout meanings. Authentication error 18456 is
    // included because the runtime identity cannot access persistence.
    private static readonly HashSet<int> UnavailableSqlErrorNumbers =
    [
        -2,
        2,
        53,
        64,
        233,
        4060,
        10053,
        10054,
        10060,
        11001,
        18456,
        40613
    ];

    internal static PersistenceException? Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            PersistenceException persistenceException => persistenceException,
            DbUpdateConcurrencyException concurrencyException =>
                new PersistenceConcurrencyException(concurrencyException),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } conflictException =>
                new PersistenceConflictException(conflictException),
            DbUpdateException { InnerException: SqlException { Number: 547 } } constraintException =>
                new PersistenceConstraintException(constraintException),
            _ when ContainsTimeout(exception) || ContainsUnavailableSqlException(exception) =>
                new PersistenceUnavailableException(exception),
            _ => null
        };
    }

    internal static bool IsUnavailableSqlErrorNumber(int errorNumber) =>
        UnavailableSqlErrorNumbers.Contains(errorNumber);

    private static bool ContainsTimeout(Exception exception) =>
        EnumerateExceptionChain(exception).Any(candidate => candidate is TimeoutException);

    private static bool ContainsUnavailableSqlException(Exception exception) =>
        EnumerateExceptionChain(exception)
            .OfType<SqlException>()
            .Any(sqlException => IsUnavailableSqlErrorNumber(sqlException.Number));

    private static IEnumerable<Exception> EnumerateExceptionChain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
