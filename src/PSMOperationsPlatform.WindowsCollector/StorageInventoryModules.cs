using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class StorageInventoryCommands
{
    private const string StorageNamespace = "Root/Microsoft/Windows/Storage";

    internal static readonly WinRmCommandDefinition Disk = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["Namespace"] = StorageNamespace,
            ["ClassName"] = "MSFT_Disk",
            ["Property"] = new[]
            {
                "UniqueId",
                "Number",
                "FriendlyName",
                "SerialNumber",
                "Size",
                "BusType",
                "PartitionStyle",
            },
        },
        [
            "UniqueId",
            "Number",
            "FriendlyName",
            "SerialNumber",
            "Size",
            "BusType",
            "PartitionStyle",
        ]);

    internal static readonly WinRmCommandDefinition Volume = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["Namespace"] = StorageNamespace,
            ["ClassName"] = "MSFT_Volume",
            ["Property"] = new[]
            {
                "UniqueId",
                "DriveLetter",
                "FileSystem",
                "FileSystemLabel",
                "Size",
                "SizeRemaining",
            },
        },
        [
            "UniqueId",
            "DriveLetter",
            "FileSystem",
            "FileSystemLabel",
            "Size",
            "SizeRemaining",
        ]);
}

internal sealed class DiskInventoryModule(IDiskSnapshotStore store)
    : IWindowsInventoryModule
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.Disk;

    public async Task ExecuteAsync(WindowsInventoryExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<WinRmCommandRecord> records =
            await context.Session.InvokeAsync(
                StorageInventoryCommands.Disk,
                context.CancellationToken);
        var stableSourceKeys = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var items = new List<DiskInventoryItem>(records.Count);
        foreach (WinRmCommandRecord record in records)
        {
            string stableSourceKey = RequiredUniqueId(
                record,
                stableSourceKeys,
                "Disk");
            items.Add(
                new DiskInventoryItem(
                    stableSourceKey,
                    WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                        record,
                        "Number"),
                    WindowsInventoryRecordNormalizer.OptionalString(
                        record,
                        "FriendlyName",
                        255),
                    WindowsInventoryRecordNormalizer.OptionalString(
                        record,
                        "SerialNumber",
                        255),
                    WindowsInventoryRecordNormalizer.OptionalNonNegativeInt64(
                        record,
                        "Size"),
                    BusType(record),
                    PartitionStyle(record)));
        }

        await store.ReplaceAsync(
            context.ManagedServer.TargetId,
            items,
            context.CancellationToken);
    }

    private static string? BusType(WinRmCommandRecord record) =>
        WindowsInventoryRecordNormalizer.OptionalUInt16(record, "BusType") switch
        {
            null => null,
            0 => "Unknown",
            1 => "SCSI",
            2 => "ATAPI",
            3 => "ATA",
            4 => "1394",
            5 => "SSA",
            6 => "Fibre Channel",
            7 => "USB",
            8 => "RAID",
            9 => "iSCSI",
            10 => "SAS",
            11 => "SATA",
            12 => "SD",
            13 => "MMC",
            14 => "Virtual",
            15 => "File Backed Virtual",
            16 => "Storage Spaces",
            17 => "NVMe",
            _ => throw new WindowsInventoryValidationException(
                "Property 'BusType' contains an unsupported value."),
        };

    private static string? PartitionStyle(WinRmCommandRecord record) =>
        WindowsInventoryRecordNormalizer.OptionalUInt16(
            record,
            "PartitionStyle") switch
        {
            null => null,
            0 => "Unknown",
            1 => "MBR",
            2 => "GPT",
            _ => throw new WindowsInventoryValidationException(
                "Property 'PartitionStyle' contains an unsupported value."),
        };

    internal static string RequiredUniqueId(
        WinRmCommandRecord record,
        HashSet<string> stableSourceKeys,
        string inventoryName)
    {
        string stableSourceKey =
            WindowsInventoryRecordNormalizer.RequiredString(
                record,
                "UniqueId",
                260);
        if (!stableSourceKeys.Add(stableSourceKey))
        {
            throw new WindowsInventoryValidationException(
                $"{inventoryName} inventory contains a duplicate UniqueId.");
        }

        return stableSourceKey;
    }
}

internal sealed class VolumeInventoryModule(IVolumeSnapshotStore store)
    : IWindowsInventoryModule
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.Volume;

    public async Task ExecuteAsync(WindowsInventoryExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<WinRmCommandRecord> records =
            await context.Session.InvokeAsync(
                StorageInventoryCommands.Volume,
                context.CancellationToken);
        var stableSourceKeys = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var items = new List<VolumeInventoryItem>(records.Count);
        foreach (WinRmCommandRecord record in records)
        {
            string stableSourceKey = DiskInventoryModule.RequiredUniqueId(
                record,
                stableSourceKeys,
                "Volume");
            long? size = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt64(
                record,
                "Size");
            long? freeSpace =
                WindowsInventoryRecordNormalizer.OptionalNonNegativeInt64(
                    record,
                    "SizeRemaining");
            if (size.HasValue && freeSpace > size)
            {
                throw new WindowsInventoryValidationException(
                    "Volume free space cannot exceed total size.");
            }

            items.Add(
                new VolumeInventoryItem(
                    stableSourceKey,
                    WindowsInventoryRecordNormalizer.OptionalDriveLetter(
                        record,
                        "DriveLetter"),
                    WindowsInventoryRecordNormalizer.OptionalString(
                        record,
                        "FileSystemLabel",
                        255),
                    WindowsInventoryRecordNormalizer.OptionalString(
                        record,
                        "FileSystem",
                        50),
                    size,
                    freeSpace));
        }

        await store.ReplaceAsync(
            context.ManagedServer.TargetId,
            items,
            context.CancellationToken);
    }
}
