using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PSMOperationsPlatform.Domain.Entities;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class AppendOnlyPersistenceTests
{
    private static readonly DateTime OccurredAt = new(2026, 7, 26, 15, 0, 0);

    [Fact]
    public void AuditLogAddedStateIsAcceptedBySaveChanges()
    {
        using TestDatabase database = TestDatabase.Create();
        database.Context.AuditLogs.Add(CreateAuditLog());

        int affectedRows = database.Context.SaveChanges();

        Assert.Equal(1, affectedRows);
    }

    [Fact]
    public void CollectorHeartbeatAddedStateIsAcceptedBySaveChanges()
    {
        using TestDatabase database = TestDatabase.Create();
        Guid collectorId = database.InsertCollectorNode();
        database.Context.CollectorHeartbeats.Add(CreateHeartbeat(collectorId));

        int affectedRows = database.Context.SaveChanges();

        Assert.Equal(1, affectedRows);
    }

    [Fact]
    public void AuditLogModifiedStateIsRejected()
    {
        using TestDatabase database = TestDatabase.Create();
        AuditLog auditLog = CreateAuditLog();
        database.Context.AuditLogs.Add(auditLog);
        database.Context.SaveChanges();
        database.Context.Entry(auditLog).Property(entry => entry.Action).CurrentValue =
            "Audit.Modified";

        PersistenceAppendOnlyViolationException exception =
            Assert.Throws<PersistenceAppendOnlyViolationException>(
                () => database.Context.SaveChanges());

        Assert.Contains(nameof(AuditLog), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EntityState.Modified), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditLogDeletedStateIsRejectedBySaveChangesAsync()
    {
        await using TestDatabase database = TestDatabase.Create();
        AuditLog auditLog = CreateAuditLog();
        database.Context.AuditLogs.Add(auditLog);
        await database.Context.SaveChangesAsync();
        database.Context.AuditLogs.Remove(auditLog);

        PersistenceAppendOnlyViolationException exception =
            await Assert.ThrowsAsync<PersistenceAppendOnlyViolationException>(
                () => database.Context.SaveChangesAsync());

        Assert.Contains(nameof(AuditLog), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EntityState.Deleted), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectorHeartbeatModifiedStateIsRejectedBySaveChangesAsync()
    {
        await using TestDatabase database = TestDatabase.Create();
        Guid collectorId = database.InsertCollectorNode();
        CollectorHeartbeat heartbeat = CreateHeartbeat(collectorId);
        database.Context.CollectorHeartbeats.Add(heartbeat);
        await database.Context.SaveChangesAsync();
        database.Context.Entry(heartbeat).Property(entry => entry.Status).CurrentValue =
            CollectorHealthStatus.Degraded;

        PersistenceAppendOnlyViolationException exception =
            await Assert.ThrowsAsync<PersistenceAppendOnlyViolationException>(
                () => database.Context.SaveChangesAsync());

        Assert.Contains(
            nameof(CollectorHeartbeat),
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains(nameof(EntityState.Modified), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectorHeartbeatDeletedStateIsRejected()
    {
        using TestDatabase database = TestDatabase.Create();
        Guid collectorId = database.InsertCollectorNode();
        CollectorHeartbeat heartbeat = CreateHeartbeat(collectorId);
        database.Context.CollectorHeartbeats.Add(heartbeat);
        database.Context.SaveChanges();
        database.Context.CollectorHeartbeats.Remove(heartbeat);

        PersistenceAppendOnlyViolationException exception =
            Assert.Throws<PersistenceAppendOnlyViolationException>(
                () => database.Context.SaveChanges());

        Assert.Contains(
            nameof(CollectorHeartbeat),
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains(nameof(EntityState.Deleted), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendOnlyViolationUsesStableErrorCode()
    {
        using TestDatabase database = TestDatabase.Create();
        AuditLog auditLog = CreateAuditLog();
        database.Context.AuditLogs.Add(auditLog);
        database.Context.SaveChanges();
        database.Context.AuditLogs.Remove(auditLog);

        PersistenceAppendOnlyViolationException exception =
            Assert.Throws<PersistenceAppendOnlyViolationException>(
                () => database.Context.SaveChanges());

        Assert.Equal("persistence.append_only_violation", exception.Code);
        Assert.Null(exception.InnerException);
    }

    private static AuditLog CreateAuditLog() =>
        new(
            Guid.NewGuid(),
            OccurredAt,
            @"AE\operator",
            "Audit.Created",
            AuditOutcome.Succeeded,
            detailJson: "{}");

    private static CollectorHeartbeat CreateHeartbeat(Guid collectorId) =>
        new(
            Guid.NewGuid(),
            collectorId,
            OccurredAt,
            CollectorHealthStatus.Healthy);

    private sealed class TestDatabase : IDisposable, IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, OperationsDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        private SqliteConnection Connection { get; }

        public OperationsDbContext Context { get; }

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
            var context = new SqliteOperationsDbContext(options);
            context.Database.EnsureCreated();
            return new TestDatabase(connection, context);
        }

        public Guid InsertCollectorNode()
        {
            Guid collectorId = Guid.NewGuid();
            using SqliteCommand command = Connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO CollectorNode
                    (Id, Name, CollectorType, HostFqdn, InstanceKey, Version,
                     IsEnabled, RegisteredAt, UpdatedAt, RowVersion)
                VALUES
                    ($id, 'Collector 1', 'Windows', 'collector01.ae.local',
                     $instanceKey, NULL, 1, '2026-07-26 15:00:00',
                     '2026-07-26 15:00:00', X'01')
                """;
            command.Parameters.AddWithValue("$id", collectorId);
            command.Parameters.AddWithValue("$instanceKey", collectorId.ToString("N"));
            command.ExecuteNonQuery();
            return collectorId;
        }

        public void Dispose()
        {
            Context.Dispose();
            Connection.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class SqliteOperationsDbContext(
        DbContextOptions<OperationsDbContext> options)
        : OperationsDbContext(options)
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
