using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class ProcessorInventoryCommand
{
    internal static readonly WinRmCommandDefinition Definition = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_Processor",
            ["Property"] = new[]
            {
                "DeviceID", "Name", "Manufacturer", "Description",
                "SocketDesignation", "ProcessorId", "NumberOfCores",
                "NumberOfLogicalProcessors", "MaxClockSpeed",
                "CurrentClockSpeed", "AddressWidth", "DataWidth",
                "Architecture", "VirtualizationFirmwareEnabled",
                "SecondLevelAddressTranslationExtensions",
                "VMMonitorModeExtensions",
            },
        },
        [
            "DeviceID", "Name", "Manufacturer", "Description",
            "SocketDesignation", "ProcessorId", "NumberOfCores",
            "NumberOfLogicalProcessors", "MaxClockSpeed",
            "CurrentClockSpeed", "AddressWidth", "DataWidth",
            "Architecture", "VirtualizationFirmwareEnabled",
            "SecondLevelAddressTranslationExtensions",
            "VMMonitorModeExtensions",
        ]);
}

internal static class ProcessorInventoryNormalizer
{
    private static readonly HashSet<string> ProcessorIdPlaceholders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "0000000000000000",
            "FFFFFFFFFFFFFFFF",
            "00000000",
            "Unknown",
            "Default string",
            "To Be Filled By O.E.M.",
            "Not Specified",
        };

    internal static ProcessorInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0)
        {
            throw new WindowsInventoryValidationException(
                "Processor inventory cannot be empty.");
        }

        var candidates = records.Select(record =>
        {
            string? deviceId = Optional(record, "DeviceID", 100);
            string? socket = Optional(record, "SocketDesignation", 255);
            string? processorId = Optional(record, "ProcessorId", 100);
            string? validProcessorId = IsStableProcessorId(processorId)
                ? processorId
                : null;
            int? cores = WindowsInventoryRecordNormalizer.OptionalPositiveInt32(
                record, "NumberOfCores");
            int? logical = WindowsInventoryRecordNormalizer.OptionalPositiveInt32(
                record, "NumberOfLogicalProcessors");
            if (cores.HasValue && logical < cores)
            {
                throw new WindowsInventoryValidationException(
                    "Processor logical count cannot be less than core count.");
            }

            string? name = Optional(record, "Name", 255);
            string? manufacturer = Optional(record, "Manufacturer", 255);
            string? description = Optional(record, "Description", 255);
            int? maxClock = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                record, "MaxClockSpeed");
            int? currentClock = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                record, "CurrentClockSpeed");
            int? addressWidth = Width(record, "AddressWidth");
            int? dataWidth = Width(record, "DataWidth");
            int? architecture = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                record, "Architecture");
            bool? virtualizationFirmware = OptionalBoolean(
                record, "VirtualizationFirmwareEnabled");
            bool? slat = OptionalBoolean(
                record, "SecondLevelAddressTranslationExtensions");
            bool? vmMonitor = OptionalBoolean(record, "VMMonitorModeExtensions");
            string? explicitKey = deviceId is not null
                ? $"DEVICE:{deviceId.ToUpperInvariant()}"
                : socket is not null
                    ? $"SOCKET:{socket.ToUpperInvariant()}"
                    : validProcessorId is not null
                        ? $"PROCESSORID:{validProcessorId.ToUpperInvariant()}"
                        : null;
            string canonical = string.Join(
                "|",
                name, manufacturer, description, cores, logical, maxClock,
                currentClock, addressWidth, dataWidth, architecture,
                virtualizationFirmware, slat, vmMonitor);
            var item = new ProcessorInventoryItem(
                explicitKey ?? string.Empty, deviceId, name, manufacturer,
                description, socket, processorId, cores, logical, maxClock,
                currentClock, addressWidth, dataWidth, architecture,
                virtualizationFirmware, slat, vmMonitor);
            return (item, explicitKey, canonical);
        }).ToArray();

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates.Where(candidate => candidate.explicitKey is not null))
        {
            if (!keys.Add(candidate.explicitKey!))
            {
                throw new WindowsInventoryValidationException(
                    "Processor inventory contains a duplicate ProcessorKey.");
            }
        }

        var result = candidates
            .Where(candidate => candidate.explicitKey is not null)
            .Select(candidate => candidate.item with { ProcessorKey = candidate.explicitKey! })
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
                    ProcessorKey = $"FALLBACK:{group.Key}:{occurrence:D2}",
                });
            }
        }

        return result
            .OrderBy(item => item.ProcessorKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? Optional(
        WinRmCommandRecord record,
        string propertyName,
        int maximumLength) =>
        WindowsInventoryRecordNormalizer.OptionalNormalizedString(
            record, propertyName, maximumLength);

    private static bool IsStableProcessorId(string? value) =>
        value is not null && !ProcessorIdPlaceholders.Contains(value);

    private static int? Width(WinRmCommandRecord record, string propertyName)
    {
        int? value = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
            record, propertyName);
        return value is null or 32 or 64
            ? value
            : throw new WindowsInventoryValidationException(
                $"Processor {propertyName} must be 32 or 64 when supplied.");
    }

    private static bool? OptionalBoolean(
        WinRmCommandRecord record,
        string propertyName)
    {
        if (!record.Properties.TryGetValue(propertyName, out object? value))
        {
            throw new WindowsInventoryValidationException(
                $"Expected property '{propertyName}' was not projected.");
        }
        return value switch
        {
            null => null,
            bool boolean => boolean,
            _ => throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be Boolean."),
        };
    }

    private static string Hash(string value) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..24];
}

internal sealed class ProcessorInventoryModule
    : IInventoryModule<ProcessorInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.Processor;

    public async Task<InventoryModuleResult<ProcessorInventoryItem[]>> CollectAsync(
        InventoryModuleContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        long startedAt = context.TimeProvider.GetTimestamp();
        int rawCount = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records = await context.Session.InvokeAsync(
                ProcessorInventoryCommand.Definition,
                cancellationToken);
            rawCount = records.Count;
            ProcessorInventoryItem[] items =
                ProcessorInventoryNormalizer.Normalize(records);
            return InventoryModuleResult<ProcessorInventoryItem[]>.Success(
                items, false, rawCount, items.Length,
                context.TimeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return InventoryModuleResult<ProcessorInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), rawCount,
                context.TimeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
