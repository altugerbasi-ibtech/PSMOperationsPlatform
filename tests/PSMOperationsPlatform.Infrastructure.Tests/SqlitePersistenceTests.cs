using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PSMOperationsPlatform.Domain.Entities;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class SqlitePersistenceTests
{
    private static readonly DateTime Now = new(2026, 7, 26, 14, 0, 0);

    [Fact]
    public async Task RepositoryCreatesReadsAndUpdatesManagedServer()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        var repository = new Repository<ManagedServer>(database.Context);
        var server = new ManagedServer(Guid.NewGuid(), "WEB01.AE.LOCAL", Now);

        await repository.AddAsync(server);
        await repository.SaveChangesAsync();
        server.UpdateDetails("Web 01", "Production", Now.AddMinutes(1));
        await repository.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        ManagedServer? persisted = await repository.GetByIdAsync(server.Id);

        Assert.NotNull(persisted);
        Assert.Equal("web01.ae.local", persisted.Fqdn);
        Assert.Equal("Web 01", persisted.DisplayName);
    }

    [Fact]
    public async Task UniqueFqdnIsEnforced()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        database.Context.Add(new ManagedServer(Guid.NewGuid(), "app01.ae.local", Now));
        database.Context.Add(new ManagedServer(Guid.NewGuid(), "APP01.AE.LOCAL.", Now));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task ForeignKeysAreEnforced()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        database.Context.Add(
            new CollectorHeartbeat(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now,
                CollectorHealthStatus.Healthy));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task HistoricalRowsRestrictParentDeletion()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Guid collectorId = Guid.NewGuid();
        await database.InsertCollectorNodeAsync(collectorId);
        database.Context.Add(
            new CollectorHeartbeat(
                Guid.NewGuid(),
                collectorId,
                Now,
                CollectorHealthStatus.Healthy));
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<SqliteException>(
            () => database.Connection.ExecuteNonQueryAsync(
                $"DELETE FROM CollectorNode WHERE Id = '{collectorId.ToString().ToUpperInvariant()}'"));

        Assert.Equal(1, await database.Context.CollectorNodes.CountAsync());
    }

    [Fact]
    public async Task DuplicateSnapshotContractIsRejected()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Guid collectorId = Guid.NewGuid();
        Guid serverId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();
        await database.InsertCollectorNodeAsync(collectorId);
        database.Context.Add(new ManagedServer(serverId, "db01.ae.local", Now));
        database.Context.Add(
            new CollectorRun(runId, collectorId, serverId, CollectionType.Sql, Now));
        await database.Context.SaveChangesAsync();
        database.Context.AddRange(
            new InventorySnapshot(
                Guid.NewGuid(),
                runId,
                serverId,
                "Sql.Inventory.v1",
                1,
                Now,
                "{}"),
            new InventorySnapshot(
                Guid.NewGuid(),
                runId,
                serverId,
                "Sql.Inventory.v1",
                1,
                Now,
                "{}"));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task AuditRecordsCanBeAppendedAndQueried()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        Guid correlationId = Guid.NewGuid();
        database.Context.Add(
            new AuditLog(
                Guid.NewGuid(),
                Now,
                @"AE\operator",
                "ManagedServer.Registered",
                AuditOutcome.Succeeded,
                correlationId: correlationId,
                detailJson: """{"source":"test"}"""));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        AuditLog record = await database.Context.AuditLogs
            .AsNoTracking()
            .SingleAsync(log => log.CorrelationId == correlationId);

        Assert.Equal("ManagedServer.Registered", record.Action);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, OperationsDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }

        public OperationsDbContext Context { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
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
            await connection.ExecuteNonQueryAsync("PRAGMA foreign_keys = ON");

            var options = new DbContextOptionsBuilder<OperationsDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new SqliteOperationsDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async Task InsertCollectorNodeAsync(Guid collectorId)
        {
            await Connection.ExecuteNonQueryAsync(
                $"""
                INSERT INTO CollectorNode
                    (Id, Name, CollectorType, HostFqdn, InstanceKey, Version, IsEnabled, RegisteredAt, UpdatedAt, RowVersion)
                VALUES
                    ('{collectorId.ToString().ToUpperInvariant()}', 'Collector 1', 'Windows', 'collector01.ae.local', 'default', NULL, 1,
                     '2026-07-26 14:00:00', '2026-07-26 14:00:00', X'01')
                """);
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

internal static class SqliteConnectionExtensions
{
    public static async Task<int> ExecuteNonQueryAsync(
        this SqliteConnection connection,
        string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteNonQueryAsync();
    }
}
