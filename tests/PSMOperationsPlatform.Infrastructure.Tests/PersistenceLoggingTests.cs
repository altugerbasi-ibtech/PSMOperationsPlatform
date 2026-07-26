using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PSMOperationsPlatform.Domain.Entities;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class PersistenceLoggingTests
{
    private static readonly DateTime OccurredAt = new(2026, 7, 26, 16, 0, 0);

    [Fact]
    public void SaveChangesSuccessProducesStableEvents()
    {
        using TestDatabase database = TestDatabase.Create();
        database.Context.AuditLogs.Add(CreateAuditLog("{}"));

        database.Context.SaveChanges();

        Assert.Contains(database.Logger.Entries, entry => entry.EventId.Id == 2100);
        Assert.Contains(database.Logger.Entries, entry => entry.EventId.Id == 2101);
        Assert.Contains(database.Logger.Entries, entry => entry.EventId.Id == 2102);
    }

    [Fact]
    public void ConcurrencyConflictProducesWarningEvent()
    {
        var logger = new TestLogger<OperationsDbContext>();
        PersistenceException mapped = Assert.IsType<PersistenceConcurrencyException>(
            PersistenceExceptionClassifier.Map(new DbUpdateConcurrencyException()));

        PersistenceLogger.SaveFailed(logger, "Async", mapped);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(2103, entry.EventId.Id);
        Assert.Contains("persistence.concurrency_conflict", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConstraintViolationProducesWarningEvent()
    {
        var logger = new TestLogger<OperationsDbContext>();
        var exception = new PersistenceConstraintException(new InvalidOperationException());

        PersistenceLogger.SaveFailed(logger, "Sync", exception);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(2104, entry.EventId.Id);
        Assert.Contains("persistence.constraint_violation", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendOnlyViolationProducesWarningEvent()
    {
        using TestDatabase database = TestDatabase.Create();
        AuditLog auditLog = CreateAuditLog("{}");
        database.Context.Add(auditLog);
        database.Context.SaveChanges();
        database.Context.Remove(auditLog);

        Assert.Throws<PersistenceAppendOnlyViolationException>(
            () => database.Context.SaveChanges());

        LogEntry entry = Assert.Single(
            database.Logger.Entries,
            candidate => candidate.EventId.Id == 2106);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public void UnavailableClassificationProducesErrorEventAndPreservesInnerException()
    {
        var logger = new TestLogger<OperationsDbContext>();
        var timeout = new TimeoutException("Synthetic timeout.");
        PersistenceUnavailableException mapped =
            Assert.IsType<PersistenceUnavailableException>(
                PersistenceExceptionClassifier.Map(timeout));

        PersistenceLogger.SaveFailed(logger, "Async", mapped);

        LogEntry entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(2105, entry.EventId.Id);
        Assert.Same(timeout, mapped.InnerException);
        Assert.Equal("persistence.unavailable", mapped.Code);
    }

    [Fact]
    public void PersistenceLogsDoNotContainConnectionStringOrSensitivePayload()
    {
        const string sensitivePayload = """{"secret-marker":"do-not-log"}""";
        const string connectionMarker = "Data Source=:memory:";
        using TestDatabase database = TestDatabase.Create();
        AuditLog auditLog = CreateAuditLog(sensitivePayload);
        database.Context.Add(auditLog);
        database.Context.SaveChanges();
        database.Context.Remove(auditLog);

        Assert.Throws<PersistenceAppendOnlyViolationException>(
            () => database.Context.SaveChanges());

        string combinedLogs = string.Join(
            Environment.NewLine,
            database.Logger.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain(sensitivePayload, combinedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(connectionMarker, combinedLogs, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(53)]
    [InlineData(64)]
    [InlineData(233)]
    [InlineData(4060)]
    [InlineData(10053)]
    [InlineData(10054)]
    [InlineData(10060)]
    [InlineData(11001)]
    [InlineData(18456)]
    [InlineData(40613)]
    public void KnownSqlConnectivityErrorsAreUnavailable(int errorNumber)
    {
        Assert.True(
            PersistenceExceptionClassifier.IsUnavailableSqlErrorNumber(errorNumber));
    }

    [Theory]
    [InlineData(2601)]
    [InlineData(2627)]
    [InlineData(547)]
    [InlineData(50000)]
    public void ConstraintAndUnknownSqlErrorsAreNotUnavailable(int errorNumber)
    {
        Assert.False(
            PersistenceExceptionClassifier.IsUnavailableSqlErrorNumber(errorNumber));
    }

    private static AuditLog CreateAuditLog(string detailJson) =>
        new(
            Guid.NewGuid(),
            OccurredAt,
            @"AE\operator",
            "Persistence.Logging.Test",
            AuditOutcome.Succeeded,
            detailJson: detailJson);

    private sealed class TestDatabase : IDisposable
    {
        private TestDatabase(
            SqliteConnection connection,
            OperationsDbContext context,
            TestLogger<OperationsDbContext> logger)
        {
            Connection = connection;
            Context = context;
            Logger = logger;
        }

        private SqliteConnection Connection { get; }

        public OperationsDbContext Context { get; }

        public TestLogger<OperationsDbContext> Logger { get; }

        public static TestDatabase Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            connection.CreateFunction(
                "ISJSON",
                (string? value) =>
                {
                    if (value is null)
                    {
                        return 0;
                    }

                    try
                    {
                        using JsonDocument _ = JsonDocument.Parse(value);
                        return 1;
                    }
                    catch (JsonException)
                    {
                        return 0;
                    }
                });

            var options = new DbContextOptionsBuilder<OperationsDbContext>()
                .UseSqlite(connection)
                .Options;
            var logger = new TestLogger<OperationsDbContext>();
            var context = new SqliteOperationsDbContext(options, logger);
            context.Database.EnsureCreated();
            return new TestDatabase(connection, context, logger);
        }

        public void Dispose()
        {
            Context.Dispose();
            Connection.Dispose();
        }
    }

    private sealed class SqliteOperationsDbContext(
        DbContextOptions<OperationsDbContext> options,
        ILogger<OperationsDbContext> logger)
        : OperationsDbContext(options, logger)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(entityType => entityType.GetProperties()))
            {
                if (property.GetColumnType() == "nvarchar(max)")
                {
                    property.SetColumnType("TEXT");
                }
            }
        }
    }
}

internal sealed record LogEntry(
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception);

internal sealed class TestLogger<TCategory> : ILogger<TCategory>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), exception));
    }
}
