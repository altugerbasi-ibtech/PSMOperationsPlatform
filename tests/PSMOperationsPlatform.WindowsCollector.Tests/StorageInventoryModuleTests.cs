using Microsoft.Extensions.Logging.Abstractions;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class StorageInventoryModuleTests
{
    [Fact]
    public void Disk_prefers_serial_and_rejects_ambiguous_duplicate_key()
    {
        var item = Assert.Single(PhysicalDiskInventoryNormalizer.Normalize(
            [Disk(("SerialNumber", " SN-1 "))]));
        Assert.Equal("SERIAL:SN-1", item.DiskKey);
        Assert.Throws<WindowsInventoryValidationException>(() =>
            PhysicalDiskInventoryNormalizer.Normalize(
                [Disk(("SerialNumber", "SN-1")), Disk(("SerialNumber", "sn-1"))]));
    }

    [Theory]
    [InlineData("00000000")]
    [InlineData("Unknown")]
    [InlineData("To Be Filled By O.E.M.")]
    public void Disk_placeholder_serial_uses_PnpDeviceId(string serial)
    {
        var item = Assert.Single(PhysicalDiskInventoryNormalizer.Normalize(
            [Disk(("SerialNumber", serial), ("PNPDeviceID", "PCI\\DISK1"))]));
        Assert.Equal("PNP:PCI\\DISK1", item.DiskKey);
    }

    [Fact]
    public void Disk_fallback_is_deterministic_under_reordering()
    {
        WinRmCommandRecord a = Disk(("Model", "A"), ("Index", null));
        WinRmCommandRecord b = Disk(("Model", "B"), ("Index", null));
        string[] first = PhysicalDiskInventoryNormalizer.Normalize([a, b]).Select(x => x.DiskKey).ToArray();
        string[] second = PhysicalDiskInventoryNormalizer.Normalize([b, a]).Select(x => x.DiskKey).ToArray();
        Assert.Equal(first, second);
        Assert.All(first, key => Assert.StartsWith("FALLBACK:", key));
    }

    [Fact]
    public void Disk_valid_empty_and_numeric_validation_are_explicit()
    {
        Assert.Empty(PhysicalDiskInventoryNormalizer.Normalize([]));
        Assert.Throws<WindowsInventoryValidationException>(() =>
            PhysicalDiskInventoryNormalizer.Normalize([Disk(("Size", 0L))]));
        Assert.Throws<WindowsInventoryValidationException>(() =>
            PhysicalDiskInventoryNormalizer.Normalize([Disk(("Partitions", -1))]));
    }

    [Fact]
    public void Volume_prefers_device_guid_and_normalizes_drive_letter()
    {
        var item = Assert.Single(VolumeInventoryNormalizer.Normalize(
            [Volume(("DeviceID", @"\\?\Volume{abc}\"), ("DriveLetter", "c"))]));
        Assert.Equal(@"DEVICE:\\?\VOLUME{ABC}\", item.VolumeKey);
        Assert.Equal("C:", item.DriveLetter);
    }

    [Fact]
    public void Volume_mount_point_only_fallback_is_stable()
    {
        WinRmCommandRecord record = Volume(
            ("DeviceID", null), ("DriveLetter", null), ("Label", "Mounted"));
        string first = Assert.Single(VolumeInventoryNormalizer.Normalize([record])).VolumeKey;
        string second = Assert.Single(VolumeInventoryNormalizer.Normalize([record])).VolumeKey;
        Assert.Equal(first, second);
        Assert.StartsWith("FALLBACK:", first);
    }

    [Fact]
    public void Volume_empty_and_invalid_capacity_are_rejected()
    {
        Assert.Throws<WindowsInventoryValidationException>(
            () => VolumeInventoryNormalizer.Normalize([]));
        Assert.Throws<WindowsInventoryValidationException>(() =>
            VolumeInventoryNormalizer.Normalize(
                [Volume(("Capacity", 100L), ("FreeSpace", 101L))]));
    }

    [Fact]
    public async Task Modules_use_the_supplied_shared_session_and_explicit_CIM_projection()
    {
        var diskSession = new TestSession([Disk()]);
        var diskContext = Context(diskSession);
        var diskResult = await new PhysicalDiskInventoryModule().CollectAsync(diskContext, default);
        Assert.True(diskResult.IsSuccessful);
        Assert.Equal("Win32_DiskDrive", diskSession.Command!.Parameters["ClassName"]);

        var volumeSession = new TestSession([Volume()]);
        var volumeContext = Context(volumeSession);
        var volumeResult = await new VolumeInventoryModule().CollectAsync(volumeContext, default);
        Assert.True(volumeResult.IsSuccessful);
        Assert.Equal("Win32_Volume", volumeSession.Command!.Parameters["ClassName"]);
        Assert.DoesNotContain("*", volumeSession.Command.PropertyNames);
    }

    private static InventoryModuleContext Context(IWinRmCommandSession session) =>
        new(Guid.NewGuid(), "storage.ae.local", Guid.NewGuid(), session,
            TimeProvider.System, NullLogger.Instance);

    private static WinRmCommandRecord Disk(params (string Name, object? Value)[] overrides) =>
        With(new Dictionary<string, object?>
        {
            ["DeviceID"] = null, ["Index"] = 0, ["Model"] = "Disk",
            ["Manufacturer"] = null, ["SerialNumber"] = null,
            ["FirmwareRevision"] = null, ["InterfaceType"] = "SCSI",
            ["MediaType"] = null, ["Size"] = 1000L, ["BytesPerSector"] = 512,
            ["Partitions"] = 1, ["PNPDeviceID"] = null, ["Status"] = "OK",
        }, overrides);

    private static WinRmCommandRecord Volume(params (string Name, object? Value)[] overrides) =>
        With(new Dictionary<string, object?>
        {
            ["DeviceID"] = null, ["DriveLetter"] = null, ["Label"] = null,
            ["FileSystem"] = "NTFS", ["Capacity"] = 1000L, ["FreeSpace"] = 500L,
            ["BlockSize"] = 4096, ["DriveType"] = 3, ["BootVolume"] = false,
            ["SystemVolume"] = false, ["PageFilePresent"] = false,
            ["DirtyBitSet"] = false, ["SerialNumber"] = null,
        }, overrides);

    private static WinRmCommandRecord With(
        Dictionary<string, object?> values,
        params (string Name, object? Value)[] overrides)
    {
        foreach (var (name, value) in overrides)
        {
            values[name] = value;
        }
        return new(values);
    }

    private sealed class TestSession(IReadOnlyList<WinRmCommandRecord> records)
        : IWinRmCommandSession
    {
        internal WinRmCommandDefinition? Command { get; private set; }
        public bool IsUsable => true;
        public Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command, CancellationToken cancellationToken)
        {
            Command = command;
            return Task.FromResult(records);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
