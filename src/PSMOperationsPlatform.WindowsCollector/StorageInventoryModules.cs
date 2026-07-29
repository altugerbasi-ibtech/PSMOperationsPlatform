using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class StorageInventoryCommands
{
    internal static readonly WinRmCommandDefinition Disk = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_DiskDrive",
            ["Property"] = new[]
            {
                "DeviceID", "Index", "Model", "Manufacturer", "SerialNumber",
                "FirmwareRevision", "InterfaceType", "MediaType", "Size",
                "BytesPerSector", "Partitions", "PNPDeviceID", "Status",
            },
        },
        [
            "DeviceID", "Index", "Model", "Manufacturer", "SerialNumber",
            "FirmwareRevision", "InterfaceType", "MediaType", "Size",
            "BytesPerSector", "Partitions", "PNPDeviceID", "Status",
        ]);

    internal static readonly WinRmCommandDefinition Volume = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_Volume",
            ["Property"] = new[]
            {
                "DeviceID", "DriveLetter", "Label", "FileSystem", "Capacity",
                "FreeSpace", "BlockSize", "DriveType", "BootVolume",
                "SystemVolume", "PageFilePresent", "DirtyBitSet", "SerialNumber",
            },
        },
        [
            "DeviceID", "DriveLetter", "Label", "FileSystem", "Capacity",
            "FreeSpace", "BlockSize", "DriveType", "BootVolume",
            "SystemVolume", "PageFilePresent", "DirtyBitSet", "SerialNumber",
        ]);
}

internal static class StorageInventoryIdentity
{
    internal const string Null = "<NULL>";
    internal const char Separator = '\u001F';

    internal static string Hash(params object?[] fields)
    {
        string canonical = string.Join(
            Separator,
            fields.Select(field => field switch
            {
                null => Null,
                IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                _ => field.ToString(),
            }));
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..24];
    }
}

internal static class PhysicalDiskInventoryNormalizer
{
    private static readonly HashSet<string> SerialPlaceholders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "00000000", "FFFFFFFF", "UNKNOWN", "DEFAULT STRING",
            "TO BE FILLED BY O.E.M.", "SYSTEM SERIAL NUMBER", "NOT SPECIFIED",
        };

    internal static DiskInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var candidates = records.Select(record =>
        {
            string? deviceId = Optional(record, "DeviceID", 260);
            int? index = NonNegativeInt(record, "Index");
            string? model = Optional(record, "Model", 255);
            string? manufacturer = Optional(record, "Manufacturer", 255);
            string? serial = Optional(record, "SerialNumber", 255);
            string? firmware = Optional(record, "FirmwareRevision", 100);
            string? interfaceType = Optional(record, "InterfaceType", 100);
            string? mediaType = Optional(record, "MediaType", 100);
            long? size = PositiveInt64(record, "Size");
            int? bytesPerSector = PositiveInt(record, "BytesPerSector");
            int? partitions = NonNegativeInt(record, "Partitions");
            string? pnp = Optional(record, "PNPDeviceID", 500);
            string? status = Optional(record, "Status", 100);
            string hash = StorageInventoryIdentity.Hash(
                deviceId?.ToUpperInvariant(), index, model?.ToUpperInvariant(),
                manufacturer?.ToUpperInvariant(), serial?.ToUpperInvariant(),
                firmware?.ToUpperInvariant(), interfaceType?.ToUpperInvariant(),
                mediaType?.ToUpperInvariant(), size, bytesPerSector, partitions,
                pnp?.ToUpperInvariant(), status?.ToUpperInvariant());
            string? key = StableSerial(serial)
                ? $"SERIAL:{serial!.ToUpperInvariant()}"
                : pnp is not null
                    ? $"PNP:{pnp.ToUpperInvariant()}"
                    : deviceId is not null
                        ? $"DEVICE:{deviceId.ToUpperInvariant()}"
                        : index.HasValue
                            ? $"INDEX:{index.Value}:{hash}"
                            : null;
            return (item: new DiskInventoryItem(
                key ?? string.Empty, deviceId, index, model, manufacturer, serial,
                firmware, interfaceType, mediaType, size, bytesPerSector,
                partitions, pnp, status), key, hash);
        }).OrderBy(candidate => candidate.hash, StringComparer.Ordinal)
          .ThenBy(candidate => candidate.key, StringComparer.Ordinal)
          .ToArray();

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates.Where(candidate => candidate.key is not null))
        {
            if (!keys.Add(candidate.key!))
            {
                throw new WindowsInventoryValidationException(
                    "Physical disk inventory contains an ambiguous duplicate DiskKey.");
            }
        }

        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        return candidates.Select(candidate =>
        {
            if (candidate.key is not null)
            {
                return candidate.item;
            }
            int occurrence = occurrences.GetValueOrDefault(candidate.hash) + 1;
            occurrences[candidate.hash] = occurrence;
            return candidate.item with
            {
                DiskKey = $"FALLBACK:{candidate.hash}:{occurrence:00}",
            };
        }).OrderBy(item => item.DiskKey, StringComparer.Ordinal).ToArray();
    }

    private static bool StableSerial(string? value) =>
        value is not null &&
        !SerialPlaceholders.Contains(value) &&
        !value.All(character => character is '0' or 'F' or 'f');

    private static string? Optional(
        WinRmCommandRecord record, string propertyName, int maxLength) =>
        WindowsInventoryRecordNormalizer.OptionalNormalizedString(
            record, propertyName, maxLength);

    private static int? NonNegativeInt(WinRmCommandRecord record, string name) =>
        WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(record, name);

    private static long? PositiveInt64(WinRmCommandRecord record, string name)
    {
        long? value = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt64(record, name);
        return value is 0
            ? throw new WindowsInventoryValidationException(
                $"Property '{name}' must be positive when supplied.")
            : value;
    }

    private static int? PositiveInt(WinRmCommandRecord record, string name)
    {
        int? value = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(record, name);
        return value is 0
            ? throw new WindowsInventoryValidationException(
                $"Property '{name}' must be positive when supplied.")
            : value;
    }
}

internal static class VolumeInventoryNormalizer
{
    internal static VolumeInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
        {
            throw new WindowsInventoryValidationException(
                "Volume inventory must contain at least one row.");
        }

        var candidates = records.Select(record =>
        {
            string? deviceId = Optional(record, "DeviceID", 260);
            string? driveLetter = NormalizeDriveLetter(Optional(record, "DriveLetter", 10));
            string? label = Optional(record, "Label", 255);
            string? fileSystem = Optional(record, "FileSystem", 50);
            long? capacity = PositiveInt64(record, "Capacity");
            long? free = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt64(
                record, "FreeSpace");
            if (capacity.HasValue && free > capacity)
            {
                throw new WindowsInventoryValidationException(
                    "Volume free space cannot exceed capacity.");
            }
            int? blockSize = PositiveInt(record, "BlockSize");
            int? driveType = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                record, "DriveType");
            if (driveType is > 6)
            {
                throw new WindowsInventoryValidationException(
                    "Volume DriveType contains an unsupported value.");
            }
            bool? boot = OptionalBoolean(record, "BootVolume");
            bool? system = OptionalBoolean(record, "SystemVolume");
            bool? pageFile = OptionalBoolean(record, "PageFilePresent");
            bool? dirty = OptionalBoolean(record, "DirtyBitSet");
            string? serial = Optional(record, "SerialNumber", 100);
            string hash = StorageInventoryIdentity.Hash(
                deviceId?.ToUpperInvariant(), driveLetter, label?.ToUpperInvariant(),
                fileSystem?.ToUpperInvariant(), capacity, blockSize, driveType,
                serial?.ToUpperInvariant());
            string? key = deviceId is not null
                ? $"DEVICE:{deviceId.ToUpperInvariant()}"
                : serial is not null
                    ? $"SERIAL:{serial.ToUpperInvariant()}:{fileSystem?.ToUpperInvariant() ?? StorageInventoryIdentity.Null}"
                    : driveLetter is not null
                        ? $"DRIVE:{driveLetter}"
                        : null;
            return (item: new VolumeInventoryItem(
                key ?? string.Empty, deviceId, driveLetter, label, fileSystem,
                capacity, free, blockSize, driveType, boot, system, pageFile,
                dirty, serial), key, hash);
        }).OrderBy(candidate => candidate.hash, StringComparer.Ordinal)
          .ThenBy(candidate => candidate.key, StringComparer.Ordinal)
          .ToArray();

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        return candidates.Select(candidate =>
        {
            string key = candidate.key ?? $"FALLBACK:{candidate.hash}:";
            if (candidate.key is null)
            {
                int occurrence = occurrences.GetValueOrDefault(candidate.hash) + 1;
                occurrences[candidate.hash] = occurrence;
                key += $"{occurrence:00}";
            }
            if (!keys.Add(key))
            {
                throw new WindowsInventoryValidationException(
                    "Volume inventory contains an ambiguous duplicate VolumeKey.");
            }
            return candidate.item with { VolumeKey = key };
        }).OrderBy(item => item.VolumeKey, StringComparer.Ordinal).ToArray();
    }

    private static string? NormalizeDriveLetter(string? value)
    {
        if (value is null)
        {
            return null;
        }
        string normalized = value.Trim().TrimEnd(':').ToUpperInvariant();
        return normalized.Length == 1 && char.IsLetter(normalized[0])
            ? $"{normalized}:"
            : throw new WindowsInventoryValidationException(
                "Volume DriveLetter is malformed.");
    }

    private static string? Optional(
        WinRmCommandRecord record, string propertyName, int maxLength) =>
        WindowsInventoryRecordNormalizer.OptionalNormalizedString(
            record, propertyName, maxLength);

    private static long? PositiveInt64(WinRmCommandRecord record, string name)
    {
        long? value = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt64(record, name);
        return value is 0
            ? throw new WindowsInventoryValidationException(
                $"Property '{name}' must be positive when supplied.")
            : value;
    }

    private static int? PositiveInt(WinRmCommandRecord record, string name)
    {
        int? value = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(record, name);
        return value is 0
            ? throw new WindowsInventoryValidationException(
                $"Property '{name}' must be positive when supplied.")
            : value;
    }

    private static bool? OptionalBoolean(WinRmCommandRecord record, string name)
    {
        if (!record.Properties.TryGetValue(name, out object? value))
        {
            throw new WindowsInventoryValidationException(
                $"Expected property '{name}' was not projected.");
        }
        return value switch
        {
            null => null,
            bool boolean => boolean,
            _ => throw new WindowsInventoryValidationException(
                $"Property '{name}' must be Boolean."),
        };
    }
}

internal sealed class PhysicalDiskInventoryModule
    : IInventoryModule<DiskInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.Disk;

    public async Task<InventoryModuleResult<DiskInventoryItem[]>> CollectAsync(
        InventoryModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        long started = context.TimeProvider.GetTimestamp();
        int rawCount = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records =
                await context.Session.InvokeAsync(StorageInventoryCommands.Disk, cancellationToken);
            rawCount = records.Count;
            DiskInventoryItem[] items = PhysicalDiskInventoryNormalizer.Normalize(records);
            return InventoryModuleResult<DiskInventoryItem[]>.Success(
                items, items.Length == 0, rawCount, items.Length,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return InventoryModuleResult<DiskInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), rawCount,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

internal sealed class VolumeInventoryModule
    : IInventoryModule<VolumeInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.Volume;

    public async Task<InventoryModuleResult<VolumeInventoryItem[]>> CollectAsync(
        InventoryModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        long started = context.TimeProvider.GetTimestamp();
        int rawCount = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records =
                await context.Session.InvokeAsync(StorageInventoryCommands.Volume, cancellationToken);
            rawCount = records.Count;
            VolumeInventoryItem[] items = VolumeInventoryNormalizer.Normalize(records);
            return InventoryModuleResult<VolumeInventoryItem[]>.Success(
                items, false, rawCount, items.Length,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return InventoryModuleResult<VolumeInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), rawCount,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
