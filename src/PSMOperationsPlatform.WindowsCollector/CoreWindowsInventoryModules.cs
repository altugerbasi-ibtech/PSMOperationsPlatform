using System.Globalization;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class CoreWindowsInventoryCommands
{
    internal static readonly WinRmCommandDefinition ComputerSystem = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_ComputerSystem",
            ["Property"] = new[] { "Name", "Domain", "DomainRole", "Manufacturer", "Model", "SystemType" },
        },
        ["Name", "Domain", "DomainRole", "Manufacturer", "Model", "SystemType"]);

    internal static readonly WinRmCommandDefinition ComputerSystemProduct = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_ComputerSystemProduct",
            ["Property"] = new[] { "UUID", "IdentifyingNumber" },
        },
        ["UUID", "IdentifyingNumber"]);

    internal static readonly WinRmCommandDefinition Bios = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_BIOS",
            ["Property"] = new[]
            {
                "Manufacturer", "SMBIOSBIOSVersion", "Version", "ReleaseDate",
                "SerialNumber", "SMBIOSMajorVersion", "SMBIOSMinorVersion",
            },
        },
        [
            "Manufacturer", "SMBIOSBIOSVersion", "Version", "ReleaseDate",
            "SerialNumber", "SMBIOSMajorVersion", "SMBIOSMinorVersion",
        ]);

    internal static readonly WinRmCommandDefinition OperatingSystem = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_OperatingSystem",
            ["Property"] = new[]
            {
                "Caption",
                "Version",
                "BuildNumber",
                "OSArchitecture",
                "ProductType",
                "OperatingSystemSKU",
                "InstallationType",
                "SystemDrive",
                "WindowsDirectory",
                "Locale",
                "CurrentTimeZone",
                "InstallDate",
                "LastBootUpTime",
            },
        },
        [
            "Caption",
            "Version",
            "BuildNumber",
            "OSArchitecture",
            "ProductType",
            "OperatingSystemSKU",
            "InstallationType",
            "SystemDrive",
            "WindowsDirectory",
            "Locale",
            "CurrentTimeZone",
            "InstallDate",
            "LastBootUpTime",
        ]);

    internal static readonly WinRmCommandDefinition Memory = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_ComputerSystem",
            ["Property"] = new[] { "TotalPhysicalMemory" },
        },
        ["TotalPhysicalMemory"]);
}

internal static class WindowsInventoryRecordNormalizer
{
    internal static WinRmCommandRecord Single(
        IReadOnlyList<WinRmCommandRecord> records,
        string inventoryName)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count != 1)
        {
            throw new WindowsInventoryValidationException(
                $"{inventoryName} inventory requires exactly one result.");
        }

        return records[0];
    }

    internal static string RequiredString(
        WinRmCommandRecord record,
        string propertyName,
        int maximumLength)
    {
        object? value = Property(record, propertyName);
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' is required.");
        }

        return NormalizedLength(text, propertyName, maximumLength);
    }

    internal static string? OptionalString(
        WinRmCommandRecord record,
        string propertyName,
        int maximumLength)
    {
        object? value = Property(record, propertyName);
        if (value is null)
        {
            return null;
        }

        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be a non-empty string when present.");
        }

        return NormalizedLength(text, propertyName, maximumLength);
    }

    internal static string RequiredNormalizedString(
        WinRmCommandRecord record,
        string propertyName,
        int maximumLength)
    {
        object? value = Property(record, propertyName);
        if (value is not string text || string.IsNullOrWhiteSpace(text))
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' is required.");
        }
        return CollapseWhitespace(text, propertyName, maximumLength);
    }

    internal static string? OptionalNormalizedString(
        WinRmCommandRecord record,
        string propertyName,
        int maximumLength)
    {
        object? value = Property(record, propertyName);
        if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        if (value is not string stringValue)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be a string when present.");
        }
        return CollapseWhitespace(stringValue, propertyName, maximumLength);
    }

    internal static DateTime? OptionalDateTime(
        WinRmCommandRecord record,
        string propertyName,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        object? value = Property(record, propertyName);
        if (value is null)
        {
            return null;
        }

        DateTime result = value switch
        {
            DateTime { Kind: DateTimeKind.Utc } dateTime =>
                TimeZoneInfo.ConvertTimeFromUtc(
                    dateTime,
                    timeProvider.LocalTimeZone),
            DateTime { Kind: DateTimeKind.Local } dateTime =>
                TimeZoneInfo.ConvertTime(
                    dateTime,
                    timeProvider.LocalTimeZone),
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset =>
                TimeZoneInfo.ConvertTime(
                    dateTimeOffset,
                    timeProvider.LocalTimeZone).DateTime,
            _ => throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be a timestamp."),
        };
        if (result == DateTime.MinValue || result == DateTime.MaxValue)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' contains an invalid timestamp.");
        }

        return result;
    }

    internal static long RequiredNonNegativeInt64(
        WinRmCommandRecord record,
        string propertyName)
    {
        object? value = Property(record, propertyName);
        long result;
        try
        {
            result = value switch
            {
                byte number => number,
                short number => number,
                ushort number => number,
                int number => number,
                uint number => number,
                long number => number,
                ulong number when number <= long.MaxValue => (long)number,
                _ => throw new WindowsInventoryValidationException(
                    $"Property '{propertyName}' must be an integer."),
            };
        }
        catch (OverflowException)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' exceeds Int64 capacity.");
        }

        if (result < 0)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' cannot be negative.");
        }

        return result;
    }

    internal static int? OptionalPositiveInt32(
        WinRmCommandRecord record,
        string propertyName)
    {
        object? value = Property(record, propertyName);
        if (value is null)
        {
            return null;
        }

        long converted = value switch
        {
            byte number => number,
            short number => number,
            ushort number => number,
            int number => number,
            uint number => number,
            long number => number,
            ulong number when number <= long.MaxValue => (long)number,
            _ => throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be an integer."),
        };
        if (converted <= 0 || converted > int.MaxValue)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be a positive Int32 value.");
        }

        return (int)converted;
    }

    internal static int? OptionalNonNegativeInt32(
        WinRmCommandRecord record,
        string propertyName)
    {
        long? value = OptionalInteger(record, propertyName);
        if (!value.HasValue)
        {
            return null;
        }

        if (value < 0 || value > int.MaxValue)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be a non-negative Int32 value.");
        }

        return (int)value.Value;
    }

    internal static int? OptionalInt32(
        WinRmCommandRecord record,
        string propertyName)
    {
        long? value = OptionalInteger(record, propertyName);
        if (!value.HasValue)
        {
            return null;
        }
        if (value < int.MinValue || value > int.MaxValue)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be an Int32 value.");
        }
        return (int)value.Value;
    }

    internal static int RequiredNonNegativeInt32(
        WinRmCommandRecord record,
        string propertyName) =>
        OptionalNonNegativeInt32(record, propertyName)
        ?? throw new WindowsInventoryValidationException(
            $"Property '{propertyName}' is required.");

    internal static long? OptionalNonNegativeInt64(
        WinRmCommandRecord record,
        string propertyName)
    {
        long? value = OptionalInteger(record, propertyName);
        if (value < 0)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' cannot be negative.");
        }

        return value;
    }

    internal static ushort? OptionalUInt16(
        WinRmCommandRecord record,
        string propertyName)
    {
        long? value = OptionalInteger(record, propertyName);
        if (!value.HasValue)
        {
            return null;
        }

        if (value < ushort.MinValue || value > ushort.MaxValue)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be a UInt16 value.");
        }

        return (ushort)value.Value;
    }

    internal static string? OptionalDriveLetter(
        WinRmCommandRecord record,
        string propertyName)
    {
        object? value = Property(record, propertyName);
        if (value is null || value is char.MinValue)
        {
            return null;
        }

        char letter = value switch
        {
            char character => character,
            string text when text.Length == 1 => text[0],
            _ => throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be a drive-letter character."),
        };
        if (!char.IsAsciiLetterUpper(letter))
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be an uppercase ASCII drive letter.");
        }

        return letter.ToString();
    }

    private static object? Property(
        WinRmCommandRecord record,
        string propertyName)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!record.Properties.TryGetValue(propertyName, out object? value))
        {
            throw new WindowsInventoryValidationException(
                $"Expected property '{propertyName}' was not projected.");
        }

        return value;
    }

    private static long? OptionalInteger(
        WinRmCommandRecord record,
        string propertyName)
    {
        object? value = Property(record, propertyName);
        return value switch
        {
            null => null,
            byte number => number,
            short number => number,
            ushort number => number,
            int number => number,
            uint number => number,
            long number => number,
            ulong number when number <= long.MaxValue => (long)number,
            _ => throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' must be an Int64-compatible integer."),
        };
    }

    private static string NormalizedLength(
        string value,
        string propertyName,
        int maximumLength)
    {
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' contains leading or trailing whitespace.");
        }
        if (value.Length > maximumLength)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' exceeds maximum length {maximumLength.ToString(CultureInfo.InvariantCulture)}.");
        }

        return value;
    }

    private static string CollapseWhitespace(
        string value,
        string propertyName,
        int maximumLength)
    {
        string normalized = string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > maximumLength)
        {
            throw new WindowsInventoryValidationException(
                $"Property '{propertyName}' exceeds maximum length {maximumLength.ToString(CultureInfo.InvariantCulture)}.");
        }
        return normalized;
    }
}

internal sealed class WindowsInventoryValidationException(string message)
    : InvalidOperationException(message);

internal sealed class ComputerInventoryModule(IComputerInventoryStore store)
    : IWindowsInventoryModule
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.Computer;

    public async Task ExecuteAsync(WindowsInventoryExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<WinRmCommandRecord> computerRecords =
            await context.Session.InvokeAsync(
                CoreWindowsInventoryCommands.ComputerSystem,
                context.CancellationToken);
        IReadOnlyList<WinRmCommandRecord> productRecords =
            await context.Session.InvokeAsync(
                CoreWindowsInventoryCommands.ComputerSystemProduct,
                context.CancellationToken);

        WinRmCommandRecord computer = WindowsInventoryRecordNormalizer.Single(
            computerRecords,
            "Computer");
        WinRmCommandRecord product = WindowsInventoryRecordNormalizer.Single(
            productRecords,
            "Computer System Product");
        var state = new ComputerInventoryState(
            WindowsInventoryRecordNormalizer.RequiredString(computer, "Name", 255),
            null,
            WindowsInventoryRecordNormalizer.OptionalString(computer, "Domain", 255),
            WindowsInventoryRecordNormalizer.OptionalString(computer, "Manufacturer", 255),
            WindowsInventoryRecordNormalizer.OptionalString(computer, "Model", 255),
            InventoryPlaceholderNormalizer.Serial(
                WindowsInventoryRecordNormalizer.OptionalString(product, "IdentifyingNumber", 255)),
            WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(computer, "DomainRole"),
            WindowsInventoryRecordNormalizer.OptionalString(computer, "SystemType", 100),
            null,
            InventoryPlaceholderNormalizer.Uuid(
                WindowsInventoryRecordNormalizer.OptionalString(product, "UUID", 50)));

        await store.UpsertAsync(
            context.ManagedServer.TargetId,
            state,
            context.CancellationToken);
    }
}

internal sealed class OperatingSystemInventoryModule(
    IOperatingSystemInventoryStore store) : IWindowsInventoryModule
{
    public WindowsInventoryModuleKind Kind =>
        WindowsInventoryModuleKind.OperatingSystem;

    public async Task ExecuteAsync(WindowsInventoryExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        WinRmCommandRecord record = WindowsInventoryRecordNormalizer.Single(
            await context.Session.InvokeAsync(
                CoreWindowsInventoryCommands.OperatingSystem,
                context.CancellationToken),
            "Operating System");
        var state = new OperatingSystemInventoryState(
            WindowsInventoryRecordNormalizer.RequiredString(record, "Caption", 255),
            WindowsInventoryRecordNormalizer.RequiredString(record, "Version", 100),
            WindowsInventoryRecordNormalizer.RequiredString(record, "BuildNumber", 50),
            WindowsInventoryRecordNormalizer.RequiredString(record, "OSArchitecture", 50),
            InstallDate: WindowsInventoryRecordNormalizer.OptionalDateTime(
                record,
                "InstallDate",
                context.TimeProvider),
            LastBootTime: WindowsInventoryRecordNormalizer.OptionalDateTime(
                record,
                "LastBootUpTime",
                context.TimeProvider));

        await store.UpsertAsync(
            context.ManagedServer.TargetId,
            state,
            context.CancellationToken);
    }
}

internal sealed class MemoryInventoryModule(IMemoryInventoryStore store)
    : IWindowsInventoryModule
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.Memory;

    public async Task ExecuteAsync(WindowsInventoryExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        WinRmCommandRecord record = WindowsInventoryRecordNormalizer.Single(
            await context.Session.InvokeAsync(
                CoreWindowsInventoryCommands.Memory,
                context.CancellationToken),
            "Memory");
        var state = new MemoryInventoryState(
            WindowsInventoryRecordNormalizer.RequiredNonNegativeInt64(
                record,
                "TotalPhysicalMemory"));

        await store.UpsertAsync(
            context.ManagedServer.TargetId,
            state,
            context.CancellationToken);
    }
}
