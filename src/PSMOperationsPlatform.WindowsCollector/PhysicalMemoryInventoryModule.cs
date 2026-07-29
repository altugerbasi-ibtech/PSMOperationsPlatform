using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class PhysicalMemoryInventoryCommand
{
    internal static readonly WinRmCommandDefinition Definition = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_PhysicalMemory",
            ["Property"] = new[]
            {
                "DeviceLocator", "BankLabel", "Capacity", "Speed",
                "ConfiguredClockSpeed", "Manufacturer", "PartNumber",
                "SerialNumber", "FormFactor", "MemoryType",
            },
        },
        [
            "DeviceLocator", "BankLabel", "Capacity", "Speed",
            "ConfiguredClockSpeed", "Manufacturer", "PartNumber",
            "SerialNumber", "FormFactor", "MemoryType",
        ]);
}

internal static class PhysicalMemoryInventoryNormalizer
{
    private static readonly HashSet<string> SerialPlaceholders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "00000000",
            "FFFFFFFF",
            "Unknown",
            "Default string",
            "To Be Filled By O.E.M.",
            "System Serial Number",
            "Not Specified",
            "None",
        };

    internal static PhysicalMemoryInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var candidates = records.Select(record =>
        {
            string? device = Optional(record, "DeviceLocator", 255);
            string? bank = Optional(record, "BankLabel", 255);
            string? serial = Optional(record, "SerialNumber", 255);
            string? stableSerial = IsStableSerial(serial) ? serial : null;
            string? explicitKey = device is not null
                ? $"DEVICE:{device.ToUpperInvariant()}"
                : bank is not null
                    ? $"BANK:{bank.ToUpperInvariant()}"
                    : stableSerial is not null
                        ? $"SERIAL:{stableSerial.ToUpperInvariant()}"
                        : null;
            long capacity = WindowsInventoryRecordNormalizer.RequiredNonNegativeInt64(
                record, "Capacity");
            if (capacity <= 0)
            {
                throw new WindowsInventoryValidationException(
                    "Physical memory Capacity must be positive.");
            }

            int? speed = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                record, "Speed");
            int? configuredSpeed = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                record, "ConfiguredClockSpeed");
            string? manufacturer = Optional(record, "Manufacturer", 255);
            string? partNumber = Optional(record, "PartNumber", 255);
            int? formFactor = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                record, "FormFactor");
            int? memoryType = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                record, "MemoryType");
            string canonical = string.Join(
                "|",
                device, bank, capacity.ToString(CultureInfo.InvariantCulture),
                speed, configuredSpeed, manufacturer, partNumber, serial,
                formFactor, memoryType);
            var item = new PhysicalMemoryInventoryItem(
                explicitKey ?? string.Empty, device, bank, capacity, speed,
                configuredSpeed, manufacturer, partNumber, serial, formFactor,
                memoryType);
            return (item, explicitKey, canonical);
        }).ToArray();

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates.Where(candidate => candidate.explicitKey is not null))
        {
            if (!keys.Add(candidate.explicitKey!))
            {
                throw new WindowsInventoryValidationException(
                    "Physical memory inventory contains a duplicate ModuleKey.");
            }
        }

        var result = candidates
            .Where(candidate => candidate.explicitKey is not null)
            .Select(candidate => candidate.item with { ModuleKey = candidate.explicitKey! })
            .ToList();
        foreach (var group in candidates
            .Where(candidate => candidate.explicitKey is null)
            .GroupBy(candidate => Hash(candidate.canonical), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            int occurrence = 0;
            foreach (var candidate in group.OrderBy(candidate => candidate.canonical, StringComparer.Ordinal))
            {
                occurrence++;
                result.Add(candidate.item with
                {
                    ModuleKey = $"FALLBACK:{group.Key}:{occurrence:D2}",
                });
            }
        }

        return result
            .OrderBy(item => item.ModuleKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? Optional(
        WinRmCommandRecord record,
        string property,
        int maximumLength) =>
        WindowsInventoryRecordNormalizer.OptionalNormalizedString(
            record, property, maximumLength);

    private static bool IsStableSerial(string? value)
    {
        if (value is null || SerialPlaceholders.Contains(value))
        {
            return false;
        }
        string compact = value
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return compact.Length > 0
            && !compact.All(character => character is '0' or 'F' or 'f');
    }

    private static string Hash(string value) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24];
}

internal sealed class PhysicalMemoryInventoryModule
    : IInventoryModule<PhysicalMemoryInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.Memory;

    public async Task<InventoryModuleResult<PhysicalMemoryInventoryItem[]>> CollectAsync(
        InventoryModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        long startedAt = context.TimeProvider.GetTimestamp();
        int rawCount = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records = await context.Session.InvokeAsync(
                PhysicalMemoryInventoryCommand.Definition,
                cancellationToken);
            rawCount = records.Count;
            PhysicalMemoryInventoryItem[] items =
                PhysicalMemoryInventoryNormalizer.Normalize(records);
            return InventoryModuleResult<PhysicalMemoryInventoryItem[]>.Success(
                items, items.Length == 0, rawCount, items.Length,
                context.TimeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return InventoryModuleResult<PhysicalMemoryInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), rawCount,
                context.TimeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
