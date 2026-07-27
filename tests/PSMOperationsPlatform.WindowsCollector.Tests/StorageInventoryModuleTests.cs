using Microsoft.Extensions.Logging.Abstractions;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class StorageInventoryModuleTests
{
    [Fact]
    public async Task Disk_module_normalizes_complete_snapshot()
    {
        var store = new DiskStore();
        var session = new StorageSession(
            [
                DiskRecord("disk-unique-1", size: 1_000_000UL),
                DiskRecord("disk-unique-2", number: 1U, busType: 17, partitionStyle: 2),
            ]);

        await new DiskInventoryModule(store).ExecuteAsync(Context(session));

        Assert.Same(StorageInventoryCommands.Disk, session.Command);
        Assert.Collection(
            store.Items!,
            item =>
            {
                Assert.Equal("disk-unique-1", item.StableSourceKey);
                Assert.Equal(0, item.DiskNumber);
                Assert.Equal(1_000_000, item.SizeBytes);
                Assert.Equal("SATA", item.BusType);
                Assert.Equal("GPT", item.PartitionStyle);
            },
            item =>
            {
                Assert.Equal("disk-unique-2", item.StableSourceKey);
                Assert.Equal("NVMe", item.BusType);
            });
    }

    [Fact]
    public async Task Volume_module_normalizes_drive_letter_and_capacity()
    {
        var store = new VolumeStore();
        var session = new StorageSession(
            [
                VolumeRecord("volume-unique-1", 'C', 2_000UL, 500UL),
                VolumeRecord("volume-unique-2", char.MinValue, 1_000UL, 1_000UL),
            ]);

        await new VolumeInventoryModule(store).ExecuteAsync(Context(session));

        Assert.Same(StorageInventoryCommands.Volume, session.Command);
        Assert.Collection(
            store.Items!,
            item =>
            {
                Assert.Equal("volume-unique-1", item.StableSourceKey);
                Assert.Equal("C", item.DriveLetter);
                Assert.Equal("NTFS", item.FileSystem);
                Assert.Equal(2_000, item.SizeBytes);
                Assert.Equal(500, item.FreeSpaceBytes);
            },
            item => Assert.Null(item.DriveLetter));
    }

    [Fact]
    public async Task Successful_empty_snapshots_reach_each_explicit_store()
    {
        var diskStore = new DiskStore();
        var volumeStore = new VolumeStore();

        await new DiskInventoryModule(diskStore).ExecuteAsync(
            Context(new StorageSession([])));
        await new VolumeInventoryModule(volumeStore).ExecuteAsync(
            Context(new StorageSession([])));

        Assert.Empty(diskStore.Items!);
        Assert.Empty(volumeStore.Items!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Duplicate_unique_id_fails_before_store(bool disk)
    {
        if (disk)
        {
            var store = new DiskStore();
            await Assert.ThrowsAsync<WindowsInventoryValidationException>(
                () => new DiskInventoryModule(store).ExecuteAsync(
                    Context(
                        new StorageSession(
                            [
                                DiskRecord("same"),
                                DiskRecord("SAME"),
                            ]))));
            Assert.Null(store.Items);
        }
        else
        {
            var store = new VolumeStore();
            await Assert.ThrowsAsync<WindowsInventoryValidationException>(
                () => new VolumeInventoryModule(store).ExecuteAsync(
                    Context(
                        new StorageSession(
                            [
                                VolumeRecord("same"),
                                VolumeRecord("SAME"),
                            ]))));
            Assert.Null(store.Items);
        }
    }

    [Fact]
    public async Task Negative_disk_size_fails_before_store()
    {
        var store = new DiskStore();
        WinRmCommandRecord record = With(
            DiskRecord("disk-1"),
            "Size",
            -1L);

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new DiskInventoryModule(store).ExecuteAsync(
                Context(new StorageSession([record]))));

        Assert.Null(store.Items);
    }

    [Fact]
    public async Task Volume_free_space_above_size_fails_before_store()
    {
        var store = new VolumeStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new VolumeInventoryModule(store).ExecuteAsync(
                Context(
                    new StorageSession(
                        [VolumeRecord("volume-1", size: 100UL, free: 101UL)]))));

        Assert.Null(store.Items);
    }

    [Fact]
    public async Task Unsupported_disk_enum_fails_before_store()
    {
        var store = new DiskStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new DiskInventoryModule(store).ExecuteAsync(
                Context(
                    new StorageSession(
                        [DiskRecord("disk-1", busType: 999)]))));

        Assert.Null(store.Items);
    }

    [Fact]
    public async Task Cancellation_propagates_without_store_call()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new VolumeStore();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new VolumeInventoryModule(store).ExecuteAsync(
                Context(new StorageSession([]), cancellation.Token)));

        Assert.Null(store.Items);
    }

    [Fact]
    public void Projections_are_explicit_allowlisted_storage_cim_queries()
    {
        AssertCommand(
            StorageInventoryCommands.Disk,
            "MSFT_Disk",
            [
                "UniqueId",
                "Number",
                "FriendlyName",
                "SerialNumber",
                "Size",
                "BusType",
                "PartitionStyle",
            ]);
        AssertCommand(
            StorageInventoryCommands.Volume,
            "MSFT_Volume",
            [
                "UniqueId",
                "DriveLetter",
                "FileSystem",
                "FileSystemLabel",
                "Size",
                "SizeRemaining",
            ]);
    }

    private static void AssertCommand(
        WinRmCommandDefinition command,
        string className,
        string[] properties)
    {
        Assert.Equal("Get-CimInstance", command.CommandName);
        Assert.Equal("Root/Microsoft/Windows/Storage", command.Parameters["Namespace"]);
        Assert.Equal(className, command.Parameters["ClassName"]);
        Assert.Equal(properties, command.PropertyNames);
        Assert.Equal(
            command.PropertyNames,
            Assert.IsType<string[]>(command.Parameters["Property"]));
        Assert.DoesNotContain("*", command.PropertyNames);
        Assert.DoesNotContain("Path", command.PropertyNames);
        Assert.DoesNotContain("ObjectId", command.PropertyNames);
    }

    private static WindowsInventoryExecutionContext Context(
        IWinRmCommandSession session,
        CancellationToken cancellationToken = default) =>
        new(
            new WindowsTarget(
                Guid.NewGuid(),
                "storage.ae.local",
                WinRmTransportMode.Auto,
                5986,
                5985,
                TimeSpan.FromSeconds(10)),
            session,
            cancellationToken,
            TimeProvider.System,
            NullLogger.Instance,
            Guid.NewGuid());

    private static WinRmCommandRecord DiskRecord(
        string uniqueId,
        uint number = 0,
        ulong size = 10_000,
        ushort busType = 11,
        ushort partitionStyle = 2) =>
        Record(
            ("UniqueId", uniqueId),
            ("Number", number),
            ("FriendlyName", "Disk"),
            ("SerialNumber", "Serial"),
            ("Size", size),
            ("BusType", busType),
            ("PartitionStyle", partitionStyle));

    private static WinRmCommandRecord VolumeRecord(
        string uniqueId,
        char driveLetter = 'D',
        ulong size = 10_000,
        ulong free = 5_000) =>
        Record(
            ("UniqueId", uniqueId),
            ("DriveLetter", driveLetter),
            ("FileSystem", "NTFS"),
            ("FileSystemLabel", "Data"),
            ("Size", size),
            ("SizeRemaining", free));

    private static WinRmCommandRecord Record(
        params (string Name, object? Value)[] values) =>
        new(new Dictionary<string, object?>(
            values.ToDictionary(value => value.Name, value => value.Value),
            StringComparer.OrdinalIgnoreCase));

    private static WinRmCommandRecord With(
        WinRmCommandRecord record,
        string property,
        object? value)
    {
        var properties = new Dictionary<string, object?>(
            record.Properties,
            StringComparer.OrdinalIgnoreCase)
        {
            [property] = value,
        };
        return new WinRmCommandRecord(properties);
    }

    private sealed class StorageSession(
        IReadOnlyList<WinRmCommandRecord> records) : IWinRmCommandSession
    {
        internal WinRmCommandDefinition? Command { get; private set; }

        public bool IsUsable => true;

        public Task OpenAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Command = command;
            return Task.FromResult(records);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DiskStore : IDiskSnapshotStore
    {
        internal IReadOnlyList<DiskInventoryItem>? Items { get; private set; }

        public Task ReplaceAsync(
            Guid managedServerId,
            IReadOnlyList<DiskInventoryItem> items,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Items = items;
            return Task.CompletedTask;
        }
    }

    private sealed class VolumeStore : IVolumeSnapshotStore
    {
        internal IReadOnlyList<VolumeInventoryItem>? Items { get; private set; }

        public Task ReplaceAsync(
            Guid managedServerId,
            IReadOnlyList<VolumeInventoryItem> items,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Items = items;
            return Task.CompletedTask;
        }
    }
}
