using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using PSMOperationsPlatform.Domain.Entities;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class WindowsInventoryPersistenceTests
{
    private static readonly DateTime CapturedAt = new(2026, 7, 27, 18, 30, 0, DateTimeKind.Unspecified);

    [Fact]
    public async Task Single_state_store_inserts_updates_and_uses_application_local_time()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid serverId = await database.AddServerAsync("computer.ae.local");
        var store = new ComputerInventoryStore(database.Context, new FixedTimeProvider(CapturedAt));

        await store.UpsertAsync(serverId, new ComputerInventoryState("OLD", null, null, null, null, null), default);
        await store.UpsertAsync(serverId, new ComputerInventoryState("NEW", "new.ae.local", null, null, null, null), default);

        WindowsComputerInventory inventory = await database.Context.WindowsComputerInventories.SingleAsync();
        Assert.Equal("NEW", inventory.ComputerName);
        Assert.Equal(CapturedAt, inventory.CapturedAt);
        Assert.Equal(DateTimeKind.Unspecified, inventory.CapturedAt.Kind);
    }

    [Fact]
    public async Task Operating_system_store_rejects_utc_source_timestamps()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid serverId = await database.AddServerAsync("os-time.ae.local");
        var store = new OperatingSystemInventoryStore(
            database.Context,
            new FixedTimeProvider(CapturedAt));

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.UpsertAsync(
                serverId,
                new OperatingSystemInventoryState(
                    "Windows",
                    "10.0",
                    "1",
                    "64-bit",
                    InstallDate: new DateTime(
                        2026,
                        7,
                        27,
                        10,
                        0,
                        0,
                        DateTimeKind.Utc)),
                default));

        Assert.Empty(database.Context.WindowsOperatingSystemInventories);
    }

    [Fact]
    public async Task Collection_store_replaces_clears_and_isolates_targets()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid first = await database.AddServerAsync("first.ae.local");
        Guid second = await database.AddServerAsync("second.ae.local");
        var store = new ProcessorSnapshotStore(database.Context, new FixedTimeProvider(CapturedAt));

        await store.ReplaceAsync(first, [new("cpu-0", CoreCount: 4)], default);
        await store.ReplaceAsync(second, [new("cpu-0", CoreCount: 8)], default);
        await store.ReplaceAsync(first, [new("cpu-1", CoreCount: 16)], default);

        Assert.Equal("cpu-1", (await database.Context.WindowsProcessorInventories.SingleAsync(x => x.ManagedServerId == first)).ProcessorKey);
        Assert.Equal(8, (await database.Context.WindowsProcessorInventories.SingleAsync(x => x.ManagedServerId == second)).CoreCount);

        await store.ReplaceAsync(first, [], default);
        Assert.False(await database.Context.WindowsProcessorInventories.AnyAsync(x => x.ManagedServerId == first));
        Assert.True(await database.Context.WindowsProcessorInventories.AnyAsync(x => x.ManagedServerId == second));
    }

    [Fact]
    public async Task Collection_store_rejects_duplicate_keys_before_deleting_snapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid serverId = await database.AddServerAsync("duplicate.ae.local");
        var store = new ProcessorSnapshotStore(database.Context, new FixedTimeProvider(CapturedAt));
        await store.ReplaceAsync(serverId, [new("cpu-0")], default);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.ReplaceAsync(serverId, [new("cpu-1"), new(" CPU-1 ")], default));

        Assert.Equal("cpu-0", (await database.Context.WindowsProcessorInventories.SingleAsync()).ProcessorKey);
    }

    [Fact]
    public async Task Network_snapshot_is_atomic_related_and_ipv4_only()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid serverId = await database.AddServerAsync("network.ae.local");
        var store = new NetworkSnapshotStore(database.Context, new FixedTimeProvider(CapturedAt));

        await store.ReplaceAsync(
            serverId,
            new NetworkInventorySnapshot(
                [new("adapter-1", Name: "Ethernet")],
                [new("adapter-1", "192.0.2.10", 24)]),
            default);

        WindowsIpv4AddressInventory address = await database.Context.WindowsIpv4AddressInventories.SingleAsync();
        WindowsNetworkAdapterInventory adapter = await database.Context.WindowsNetworkAdapterInventories.SingleAsync();
        Assert.Equal(adapter.Id, address.NetworkAdapterInventoryId);
        Assert.Equal("192.0.2.10", address.Address);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.ReplaceAsync(
                serverId,
                new NetworkInventorySnapshot([new("adapter-2")], [new("adapter-2", "::ffff:192.0.2.1", 24)]),
                default));

        Assert.Equal("adapter-1", (await database.Context.WindowsNetworkAdapterInventories.SingleAsync()).AdapterKey);
        Assert.Equal("192.0.2.10", (await database.Context.WindowsIpv4AddressInventories.SingleAsync()).Address);
    }

    [Fact]
    public async Task Network_adapter_cannot_be_deleted_while_ipv4_dependents_exist()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid serverId = await database.AddServerAsync("network-fk.ae.local");
        var store = new NetworkSnapshotStore(
            database.Context,
            new FixedTimeProvider(CapturedAt));
        await store.ReplaceAsync(
            serverId,
            new NetworkInventorySnapshot(
                [new("adapter-1")],
                [new("adapter-1", "192.0.2.10", 24)]),
            default);
        database.Context.ChangeTracker.Clear();
        WindowsNetworkAdapterInventory adapter =
            await database.Context.WindowsNetworkAdapterInventories
                .AsNoTracking()
                .SingleAsync();

        database.Context.WindowsNetworkAdapterInventories.Remove(adapter);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
        database.Context.ChangeTracker.Clear();
        Assert.Single(database.Context.WindowsNetworkAdapterInventories);
        Assert.Single(database.Context.WindowsIpv4AddressInventories);
    }

    [Fact]
    public async Task Network_empty_failure_and_cancellation_preserve_atomic_semantics()
    {
        var interceptor = new FailingSaveChangesInterceptor();
        await using var database = await TestDatabase.CreateAsync(interceptor);
        Guid serverId = await database.AddServerAsync("network-atomic.ae.local");
        var store = new NetworkSnapshotStore(
            database.Context,
            new FixedTimeProvider(CapturedAt));
        var oldSnapshot = new NetworkInventorySnapshot(
            [new("adapter-old")],
            [new("adapter-old", "192.0.2.20", 24)]);
        await store.ReplaceAsync(serverId, oldSnapshot, default);

        interceptor.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ReplaceAsync(
                serverId,
                new NetworkInventorySnapshot(
                    [new("adapter-new")],
                    [new("adapter-new", "198.51.100.20", 24)]),
                default));
        interceptor.Fail = false;
        Assert.Equal(
            "adapter-old",
            (await database.Context.WindowsNetworkAdapterInventories
                .AsNoTracking()
                .SingleAsync()).AdapterKey);
        Assert.Equal(
            "192.0.2.20",
            (await database.Context.WindowsIpv4AddressInventories
                .AsNoTracking()
                .SingleAsync()).Address);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.ReplaceAsync(
                serverId,
                new NetworkInventorySnapshot([], []),
                cancellation.Token));
        Assert.Single(database.Context.WindowsNetworkAdapterInventories);
        Assert.Single(database.Context.WindowsIpv4AddressInventories);

        await store.ReplaceAsync(
            serverId,
            new NetworkInventorySnapshot([], []),
            default);
        Assert.Empty(database.Context.WindowsNetworkAdapterInventories);
        Assert.Empty(database.Context.WindowsIpv4AddressInventories);
    }

    [Fact]
    public async Task Failed_replace_rolls_back_and_preserves_previous_snapshot()
    {
        var interceptor = new FailingSaveChangesInterceptor();
        await using var database = await TestDatabase.CreateAsync(interceptor);
        Guid serverId = await database.AddServerAsync("rollback.ae.local");
        var store = new ProcessorSnapshotStore(database.Context, new FixedTimeProvider(CapturedAt));
        await store.ReplaceAsync(serverId, [new("cpu-old")], default);
        interceptor.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ReplaceAsync(serverId, [new("cpu-new")], default));
        interceptor.Fail = false;

        Assert.Equal("cpu-old", (await database.Context.WindowsProcessorInventories.AsNoTracking().SingleAsync()).ProcessorKey);
    }

    [Fact]
    public async Task Cancellation_and_missing_target_do_not_change_existing_snapshot()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid serverId = await database.AddServerAsync("cancel.ae.local");
        var store = new ProcessorSnapshotStore(database.Context, new FixedTimeProvider(CapturedAt));
        await store.ReplaceAsync(serverId, [new("cpu-old")], default);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.ReplaceAsync(serverId, [new("cpu-new")], cancellation.Token));
        await Assert.ThrowsAsync<InventoryTargetNotFoundException>(() =>
            store.ReplaceAsync(Guid.NewGuid(), [new("cpu-new")], default));

        Assert.Equal("cpu-old", (await database.Context.WindowsProcessorInventories.AsNoTracking().SingleAsync()).ProcessorKey);
    }

    [Fact]
    public async Task Disk_and_volume_use_independent_replace_all_snapshots()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid serverId = await database.AddServerAsync("storage.ae.local");
        var diskStore = new DiskSnapshotStore(
            database.Context,
            new FixedTimeProvider(CapturedAt));
        var volumeStore = new VolumeSnapshotStore(
            database.Context,
            new FixedTimeProvider(CapturedAt));

        await diskStore.ReplaceAsync(
            serverId,
            [new("disk-old", Index: 0, SizeBytes: 1_000)],
            default);
        await volumeStore.ReplaceAsync(
            serverId,
            [new("volume-1", DriveLetter: "C", CapacityBytes: 500, FreeSpaceBytes: 100)],
            default);
        await diskStore.ReplaceAsync(
            serverId,
            [new("disk-new", Index: 1, SizeBytes: 2_000)],
            default);

        Assert.Equal(
            "disk-new",
            (await database.Context.WindowsDiskInventories.SingleAsync())
                .StableSourceKey);
        Assert.Equal(
            "volume-1",
            (await database.Context.WindowsVolumeInventories.SingleAsync())
                .StableSourceKey);

        await volumeStore.ReplaceAsync(serverId, [], default);
        Assert.Empty(database.Context.WindowsVolumeInventories);
        Assert.Single(database.Context.WindowsDiskInventories);
    }

    [Fact]
    public async Task Disk_replace_failure_and_volume_cancellation_preserve_old_snapshots()
    {
        var interceptor = new FailingSaveChangesInterceptor();
        await using var database = await TestDatabase.CreateAsync(interceptor);
        Guid serverId = await database.AddServerAsync("storage-rollback.ae.local");
        var diskStore = new DiskSnapshotStore(
            database.Context,
            new FixedTimeProvider(CapturedAt));
        var volumeStore = new VolumeSnapshotStore(
            database.Context,
            new FixedTimeProvider(CapturedAt));
        await diskStore.ReplaceAsync(serverId, [new("disk-old")], default);
        await volumeStore.ReplaceAsync(serverId, [new("volume-old")], default);

        interceptor.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => diskStore.ReplaceAsync(serverId, [new("disk-new")], default));
        interceptor.Fail = false;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => volumeStore.ReplaceAsync(
                serverId,
                [new("volume-new")],
                cancellation.Token));

        Assert.Equal(
            "disk-old",
            (await database.Context.WindowsDiskInventories
                .AsNoTracking()
                .SingleAsync()).StableSourceKey);
        Assert.Equal(
            "volume-old",
            (await database.Context.WindowsVolumeInventories
                .AsNoTracking()
                .SingleAsync()).StableSourceKey);
    }

    [Fact]
    public async Task Core_store_commits_one_capture_and_inventory_schedule_atomically()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid serverId = await database.AddServerAsync("core-atomic.ae.local");
        DateTime nextAttempt = CapturedAt.AddHours(6);
        var store = new CoreWindowsInventoryStore(
            database.Context,
            NullLogger<CoreWindowsInventoryStore>.Instance);
        var snapshot = new CoreWindowsInventorySnapshot(
            new("CORE", "core-atomic.ae.local", "ae.local", "Contoso", "Model", null),
            new("Windows Server 2022", "10.0", "20348", "64-bit"),
            new("Contoso", "1.2.3", "BIOS", null, "SERIAL", 3, 5),
            [new("CPU0", CoreCount: 4)],
            [new("DEVICE:DIMM0", "DIMM0", null, 1024, null, null, null, null, null, 8, 26)],
            [],
            [new("volume-1", DriveLetter: "C", CapacityBytes: 100, FreeSpaceBytes: 50)],
            new([], []));

        Guid inventoryRunId = Guid.NewGuid();
        await store.ReplaceAsync(
            serverId, snapshot, inventoryRunId, CapturedAt, nextAttempt, default);

        Assert.Equal(
            CapturedAt,
            (await database.Context.WindowsComputerInventories.SingleAsync()).CapturedAt);
        Assert.Equal(
            CapturedAt,
            (await database.Context.WindowsOperatingSystemInventories.SingleAsync()).CapturedAt);
        Assert.Equal(
            "SERIAL",
            (await database.Context.WindowsBiosInventories.SingleAsync()).SerialNumber);
        Assert.Equal(
            CapturedAt,
            (await database.Context.WindowsProcessorInventories.SingleAsync()).CapturedAt);
        Assert.Equal(
            CapturedAt,
            (await database.Context.WindowsVolumeInventories.SingleAsync()).CapturedAt);
        Assert.All(
            new[]
            {
                (await database.Context.WindowsComputerInventories.SingleAsync()).InventoryRunId,
                (await database.Context.WindowsOperatingSystemInventories.SingleAsync()).InventoryRunId,
                (await database.Context.WindowsBiosInventories.SingleAsync()).InventoryRunId,
                (await database.Context.WindowsProcessorInventories.SingleAsync()).InventoryRunId,
                (await database.Context.WindowsMemoryInventories.SingleAsync()).InventoryRunId,
                (await database.Context.WindowsVolumeInventories.SingleAsync()).InventoryRunId,
            },
            value => Assert.Equal(inventoryRunId, value));
        ManagedServer target = await database.Context.ManagedServers.SingleAsync();
        Assert.Equal(CapturedAt, target.LastInventoryAttemptAt);
        Assert.Equal(CapturedAt, target.LastInventorySuccessAt);
        Assert.Equal(nextAttempt, target.NextInventoryAttemptAt);
        Assert.Equal(0, target.ConsecutiveInventoryFailures);
        Assert.Equal(1, target.InventoryVersion);

        await store.ReplaceAsync(
            serverId,
            snapshot with { MemoryModules = [] },
            Guid.NewGuid(),
            CapturedAt.AddHours(6),
            CapturedAt.AddHours(12),
            default);
        Assert.Equal(
            2,
            (await database.Context.ManagedServers.SingleAsync()).InventoryVersion);
        Assert.Equal(1, await database.Context.WindowsComputerInventories.CountAsync());
        Assert.Equal(1, await database.Context.WindowsOperatingSystemInventories.CountAsync());
        Assert.Equal(1, await database.Context.WindowsBiosInventories.CountAsync());
        Assert.Empty(await database.Context.WindowsMemoryInventories.ToListAsync());
    }

    [Fact]
    public async Task Inventory_failure_schedule_uses_approved_bounded_backoff()
    {
        await using var database = await TestDatabase.CreateAsync();
        Guid serverId = await database.AddServerAsync("inventory-backoff.ae.local");
        var store = new InventoryScheduleStore(
            database.Context,
            NullLogger<InventoryScheduleStore>.Instance);
        TimeSpan[] expected =
        [
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(60),
            TimeSpan.FromMinutes(60),
        ];

        for (int index = 0; index < expected.Length; index++)
        {
            DateTime attempt = CapturedAt.AddHours(index);
            await store.RecordFailureAsync(
                serverId, attempt, "CollectionFailure", default);
            ManagedServer target = await database.Context.ManagedServers.SingleAsync();
            Assert.Equal(index + 1, target.ConsecutiveInventoryFailures);
            Assert.Equal(attempt, target.LastInventoryAttemptAt);
            Assert.Equal(attempt.Add(expected[index]), target.NextInventoryAttemptAt);
            Assert.Equal("CollectionFailure", target.LastInventoryFailureCategory);
            Assert.Equal(0, target.InventoryVersion);
        }
    }

    [Fact]
    public async Task Core_persistence_failure_rolls_back_every_core_table()
    {
        var interceptor = new FailingSaveChangesInterceptor();
        await using var database = await TestDatabase.CreateAsync(interceptor);
        Guid serverId = await database.AddServerAsync("core-rollback.ae.local");
        await new ProcessorSnapshotStore(
            database.Context,
            new FixedTimeProvider(CapturedAt))
            .ReplaceAsync(serverId, [new("cpu-old")], default);
        await new VolumeSnapshotStore(
            database.Context,
            new FixedTimeProvider(CapturedAt))
            .ReplaceAsync(serverId, [new("volume-old")], default);
        Guid processorRunBefore = (await database.Context.WindowsProcessorInventories
            .AsNoTracking().SingleAsync()).InventoryRunId;
        Guid volumeRunBefore = (await database.Context.WindowsVolumeInventories
            .AsNoTracking().SingleAsync()).InventoryRunId;
        var store = new CoreWindowsInventoryStore(
            database.Context,
            NullLogger<CoreWindowsInventoryStore>.Instance);
        var snapshot = new CoreWindowsInventorySnapshot(
            new("NEW", "core-rollback.ae.local", null, null, null, null),
            new("Windows Server 2022", "10.0", "20348", "64-bit"),
            new(null, null, null, null, null, null, null),
            [new("cpu-new")],
            [],
            [],
            [new("volume-new")],
            new([], []));
        interceptor.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ReplaceAsync(
                serverId,
                snapshot,
                Guid.NewGuid(),
                CapturedAt.AddHours(1),
                CapturedAt.AddHours(7),
                default));
        interceptor.Fail = false;

        Assert.False(await database.Context.WindowsComputerInventories.AnyAsync());
        Assert.Equal(
            "cpu-old",
            (await database.Context.WindowsProcessorInventories.AsNoTracking().SingleAsync())
                .ProcessorKey);
        Assert.Equal(
            "volume-old",
            (await database.Context.WindowsVolumeInventories.AsNoTracking().SingleAsync())
                .StableSourceKey);
        Assert.Equal(
            processorRunBefore,
            (await database.Context.WindowsProcessorInventories.AsNoTracking().SingleAsync())
                .InventoryRunId);
        Assert.Equal(
            volumeRunBefore,
            (await database.Context.WindowsVolumeInventories.AsNoTracking().SingleAsync())
                .InventoryRunId);
        Assert.Equal(
            0,
            (await database.Context.ManagedServers.AsNoTracking().SingleAsync())
                .InventoryVersion);
    }

    [Fact]
    public void Runtime_model_matches_the_controlled_migration_snapshot()
    {
        using var context = new OperationsDbContext(
            new DbContextOptionsBuilder<OperationsDbContext>()
                .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Wp0053Model;Trusted_Connection=True")
                .Options);

        ModelSnapshot snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!;
        var snapshotModel = context.GetService<IModelRuntimeInitializer>()
            .Initialize(snapshot.Model, designTime: true);
        var differences = context.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshotModel.GetRelationalModel(),
            context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.True(
            differences.Count == 0,
            string.Join(
                Environment.NewLine,
                differences.Select(operation => operation switch
                {
                    DropColumnOperation column => $"{operation.GetType().Name}: {column.Table}.{column.Name}",
                    ColumnOperation column => $"{operation.GetType().Name}: {column.Table}.{column.Name}",
                    TableOperation table => $"{operation.GetType().Name}: {table.Name}",
                    _ => operation.GetType().Name
                })));
    }

    [Fact]
    public void Controlled_migration_generates_expected_up_and_down_sql()
    {
        using var context = new OperationsDbContext(
            new DbContextOptionsBuilder<OperationsDbContext>()
                .UseSqlServer(
                    "Server=(localdb)\\MSSQLLocalDB;Database=Wp0053Script;Trusted_Connection=True")
                .Options);
        IMigrator migrator = context.GetService<IMigrator>();

        string up = migrator.GenerateScript(
            "20260727190000_AddManagedServerConnectivityState",
            "20260727230000_AddWindowsInventoryCurrentState");
        string down = migrator.GenerateScript(
            "20260727230000_AddWindowsInventoryCurrentState",
            "20260727190000_AddManagedServerConnectivityState");

        Assert.Contains(
            "CREATE TABLE [inventory].[WindowsComputerInventory]",
            up,
            StringComparison.Ordinal);
        Assert.Contains(
            "CREATE TABLE [inventory].[WindowsIpv4AddressInventory]",
            up,
            StringComparison.Ordinal);
        Assert.DoesNotContain("nvarchar(max)", up, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ON DELETE CASCADE", up, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            down.IndexOf(
                "DROP TABLE [inventory].[WindowsIpv4AddressInventory]",
                StringComparison.Ordinal)
            < down.IndexOf(
                "DROP TABLE [inventory].[WindowsNetworkAdapterInventory]",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Wp0071_forward_migration_is_narrow_and_enforces_composite_network_integrity()
    {
        using var context = new OperationsDbContext(
            new DbContextOptionsBuilder<OperationsDbContext>()
                .UseSqlServer(
                    "Server=(localdb)\\MSSQLLocalDB;Database=Wp0071Script;Trusted_Connection=True")
                .Options);
        string sql = context.GetService<IMigrator>().GenerateScript(
            "20260727230000_AddWindowsInventoryCurrentState",
            "20260728093000_WP0071CoreInventoryReliability");

        Assert.Contains("ALTER TABLE [configuration].[ManagedServer]", sql, StringComparison.Ordinal);
        Assert.Contains("[NextInventoryAttemptAt] datetime2(7)", sql, StringComparison.Ordinal);
        Assert.Contains("[InventoryVersion] bigint", sql, StringComparison.Ordinal);
        Assert.Contains("UX_WindowsMemoryInventory_ManagedServer_ModuleKey", sql, StringComparison.Ordinal);
        Assert.Contains(
            "FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId_ManagedServerId",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CREATE TABLE [configuration].[ManagedServer]",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixedTimeProvider(DateTime localTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(localTime, TimeSpan.FromHours(3)).ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone =>
            TimeZoneInfo.CreateCustomTimeZone("Türkiye Test", TimeSpan.FromHours(3), "Türkiye Test", "Türkiye Test");
    }

    private sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
    {
        internal bool Fail { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            Fail
                ? ValueTask.FromException<InterceptionResult<int>>(
                    new InvalidOperationException("Injected persistence failure."))
                : ValueTask.FromResult(result);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private TestDatabase(SqliteConnection connection, OperationsDbContext context)
        {
            this.connection = connection;
            Context = context;
        }

        internal OperationsDbContext Context { get; }

        internal static async Task<TestDatabase> CreateAsync(params IInterceptor[] interceptors)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateFunction("ISJSON", (string? value) => value is null ? 0 : 1);
            DbContextOptions<OperationsDbContext> options =
                new DbContextOptionsBuilder<OperationsDbContext>()
                    .UseSqlite(connection)
                    .AddInterceptors(interceptors)
                    .Options;
            var context = new SqliteOperationsDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        internal async Task<Guid> AddServerAsync(string fqdn)
        {
            Guid id = Guid.NewGuid();
            Context.ManagedServers.Add(new ManagedServer(id, fqdn, CapturedAt));
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            return id;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
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

            modelBuilder.Entity<ManagedServer>()
                .Property(entity => entity.RowVersion)
                .HasDefaultValueSql("randomblob(8)");
            modelBuilder.Entity<WindowsProcessorInventory>()
                .Property(entity => entity.RowVersion)
                .HasDefaultValueSql("randomblob(8)");
            modelBuilder.Entity<WindowsMemoryInventory>()
                .Property(entity => entity.RowVersion)
                .HasDefaultValueSql("randomblob(8)");
            modelBuilder.Entity<WindowsNetworkAdapterInventory>()
                .Property(entity => entity.RowVersion)
                .HasDefaultValueSql("randomblob(8)");
            modelBuilder.Entity<WindowsIpv4AddressInventory>()
                .Property(entity => entity.RowVersion)
                .HasDefaultValueSql("randomblob(8)");
        }
    }
}
