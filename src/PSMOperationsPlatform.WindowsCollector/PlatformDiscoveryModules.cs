using System.Globalization;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class PlatformDiscoveryCommands
{
    internal static readonly WinRmCommandDefinition WindowsRoles = new(
        "Get-WindowsFeature", new Dictionary<string, object?>(),
        ["Name", "DisplayName", "Installed", "Parent", "FeatureType"]);

    internal static readonly WinRmCommandDefinition WindowsFeatures = new(
        "Get-WindowsFeature", new Dictionary<string, object?>(),
        ["Name", "DisplayName", "Installed", "Parent", "RestartNeeded", "FeatureType"]);

    internal static readonly WinRmCommandDefinition Iis = new(
        "Get-ItemProperty",
        new Dictionary<string, object?>
        {
            ["LiteralPath"] = @"HKLM:\SOFTWARE\Microsoft\InetStp",
            ["ErrorAction"] = "SilentlyContinue",
        },
        ["Install", "VersionString", "MajorVersion", "MinorVersion"]);

    internal static readonly WinRmCommandDefinition DotNet = new(
        "Get-ItemProperty",
        new Dictionary<string, object?>
        {
            ["Path"] = new[]
            {
                @"HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
                @"HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
                @"HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",
            },
            ["ErrorAction"] = "SilentlyContinue",
        },
        ["DisplayName", "DisplayVersion", "Version", "Release"]);

    internal static readonly WinRmCommandDefinition PowerShell = new(
        "Get-Command",
        new Dictionary<string, object?>
        {
            ["Name"] = new[] { "powershell.exe", "pwsh.exe" },
            ["ErrorAction"] = "SilentlyContinue",
        },
        ["Name", "Source", "Version"]);
}

internal static class PlatformDiscoveryValue
{
    internal static string Required(
        WinRmCommandRecord record, string property, int maxLength) =>
        WindowsInventoryRecordNormalizer.RequiredString(record, property, maxLength)
            .Trim();

    internal static string? Optional(
        WinRmCommandRecord record, string property, int maxLength) =>
        WindowsInventoryRecordNormalizer.OptionalNormalizedString(
            record, property, maxLength);

    internal static bool OptionalBoolean(
        WinRmCommandRecord record, string property, bool defaultValue = false)
    {
        if (!record.Properties.TryGetValue(property, out object? value))
        {
            throw new WindowsInventoryValidationException(
                $"Expected property '{property}' was not projected.");
        }
        return value switch
        {
            null => defaultValue,
            bool boolean => boolean,
            _ => throw new WindowsInventoryValidationException(
                $"Property '{property}' must be Boolean."),
        };
    }

    internal static int? OptionalInt32(
        WinRmCommandRecord record, string property)
    {
        if (!record.Properties.TryGetValue(property, out object? value))
        {
            throw new WindowsInventoryValidationException(
                $"Expected property '{property}' was not projected.");
        }
        return value switch
        {
            null => null,
            int number when number >= 0 => number,
            uint number when number <= int.MaxValue => (int)number,
            long number when number is >= 0 and <= int.MaxValue => (int)number,
            string text when int.TryParse(
                text, NumberStyles.None, CultureInfo.InvariantCulture,
                out int parsed) && parsed >= 0 => parsed,
            _ => throw new WindowsInventoryValidationException(
                $"Property '{property}' must be a non-negative Int32."),
        };
    }

    internal static string Key(string prefix, string value) =>
        $"{prefix}:{value.Trim().ToUpperInvariant()}";

    internal static void EnsureUnique(IEnumerable<string> keys, string module)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (keys.Any(key => !unique.Add(key)))
        {
            throw new WindowsInventoryValidationException(
                $"{module} discovery contains a duplicate deterministic key.");
        }
    }
}

internal static class WindowsRoleDiscoveryNormalizer
{
    internal static WindowsRoleInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        WindowsRoleInventoryItem[] items = records
            .Where(record =>
                PlatformDiscoveryValue.OptionalBoolean(record, "Installed") &&
                string.Equals(
                    PlatformDiscoveryValue.Optional(record, "FeatureType", 50),
                    "Role", StringComparison.OrdinalIgnoreCase))
            .Select(record =>
            {
                string name = PlatformDiscoveryValue.Required(record, "Name", 200);
                return new WindowsRoleInventoryItem(
                    PlatformDiscoveryValue.Key("ROLE", name), name,
                    PlatformDiscoveryValue.Optional(record, "DisplayName", 255),
                    PlatformDiscoveryValue.Optional(record, "Parent", 200),
                    PlatformDiscoveryValue.Optional(record, "FeatureType", 50));
            }).OrderBy(item => item.RoleKey, StringComparer.Ordinal).ToArray();
        PlatformDiscoveryValue.EnsureUnique(items.Select(item => item.RoleKey), "Role");
        return items;
    }
}

internal static class WindowsFeatureDiscoveryNormalizer
{
    internal static WindowsFeatureInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        WindowsFeatureInventoryItem[] items = records
            .Where(record =>
                PlatformDiscoveryValue.OptionalBoolean(record, "Installed") &&
                !string.Equals(
                    PlatformDiscoveryValue.Optional(record, "FeatureType", 50),
                    "Role", StringComparison.OrdinalIgnoreCase))
            .Select(record =>
            {
                string name = PlatformDiscoveryValue.Required(record, "Name", 200);
                return new WindowsFeatureInventoryItem(
                    PlatformDiscoveryValue.Key("FEATURE", name), name,
                    PlatformDiscoveryValue.Optional(record, "DisplayName", 255),
                    PlatformDiscoveryValue.Optional(record, "Parent", 200),
                    PlatformDiscoveryValue.Optional(record, "RestartNeeded", 50),
                    PlatformDiscoveryValue.Optional(record, "FeatureType", 50));
            }).OrderBy(item => item.FeatureKey, StringComparer.Ordinal).ToArray();
        PlatformDiscoveryValue.EnsureUnique(
            items.Select(item => item.FeatureKey), "Feature");
        return items;
    }
}

internal static class IisPlatformDiscoveryNormalizer
{
    internal static IisPlatformInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        if (records.Count == 0)
        {
            return [];
        }
        if (records.Count != 1)
        {
            throw new WindowsInventoryValidationException(
                "IIS platform discovery returned multiple capability records.");
        }
        WinRmCommandRecord record = records[0];
        bool installed = PlatformDiscoveryValue.OptionalInt32(record, "Install") == 1;
        string? version = PlatformDiscoveryValue.Optional(
            record, "VersionString", 100);
        if (installed && version is null)
        {
            int? major = PlatformDiscoveryValue.OptionalInt32(record, "MajorVersion");
            int? minor = PlatformDiscoveryValue.OptionalInt32(record, "MinorVersion");
            version = major.HasValue
                ? $"{major.Value}.{minor.GetValueOrDefault()}"
                : null;
        }
        return [new("IIS:PLATFORM", installed, version)];
    }
}

internal static class DotNetPlatformDiscoveryNormalizer
{
    internal static DotNetPlatformInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        var items = new List<DotNetPlatformInventoryItem>();
        foreach (WinRmCommandRecord record in records)
        {
            string? displayName = PlatformDiscoveryValue.Optional(
                record, "DisplayName", 255);
            int? release = PlatformDiscoveryValue.OptionalInt32(record, "Release");
            string? version = PlatformDiscoveryValue.Optional(
                record, "DisplayVersion", 100) ??
                PlatformDiscoveryValue.Optional(record, "Version", 100);
            string? category = Category(displayName, release);
            if (category is null)
            {
                continue;
            }
            string name = displayName ?? ".NET Framework 4";
            string identity = $"{category}|{name}|{version ?? "<NULL>"}|{release?.ToString(CultureInfo.InvariantCulture) ?? "<NULL>"}";
            items.Add(new(
                PlatformDiscoveryValue.Key("DOTNET", identity), category,
                name, version, release));
        }
        DotNetPlatformInventoryItem[] result = items
            .OrderBy(item => item.DotNetKey, StringComparer.Ordinal).ToArray();
        PlatformDiscoveryValue.EnsureUnique(
            result.Select(item => item.DotNetKey), ".NET");
        return result;
    }

    private static string? Category(string? name, int? release)
    {
        if (release.HasValue)
        {
            return "Framework";
        }
        if (name is null || !name.Contains(".NET", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (name.Contains("Hosting Bundle", StringComparison.OrdinalIgnoreCase))
        {
            return "HostingBundle";
        }
        if (name.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase))
        {
            return "AspNetRuntime";
        }
        if (name.Contains("SDK", StringComparison.OrdinalIgnoreCase))
        {
            return "Sdk";
        }
        if (name.Contains("Runtime", StringComparison.OrdinalIgnoreCase))
        {
            return "Runtime";
        }
        return name.Contains("Framework", StringComparison.OrdinalIgnoreCase)
            ? "Framework"
            : null;
    }
}

internal static class PowerShellPlatformDiscoveryNormalizer
{
    internal static PowerShellPlatformInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        PowerShellPlatformInventoryItem[] items = records.Select(record =>
        {
            string name = PlatformDiscoveryValue.Required(record, "Name", 100);
            string path = PlatformDiscoveryValue.Required(record, "Source", 500);
            string edition = name.Equals(
                "pwsh.exe", StringComparison.OrdinalIgnoreCase)
                ? "Core"
                : name.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase)
                    ? "Desktop"
                    : throw new WindowsInventoryValidationException(
                        "PowerShell discovery returned an unexpected executable.");
            return new PowerShellPlatformInventoryItem(
                PlatformDiscoveryValue.Key("POWERSHELL", edition), edition,
                PlatformDiscoveryValue.Optional(record, "Version", 100), path);
        }).OrderBy(item => item.PowerShellKey, StringComparer.Ordinal).ToArray();
        PlatformDiscoveryValue.EnsureUnique(
            items.Select(item => item.PowerShellKey), "PowerShell");
        return items;
    }
}

internal sealed class WindowsRoleDiscoveryModule
    : IInventoryModule<WindowsRoleInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.WindowsRole;
    public async Task<InventoryModuleResult<WindowsRoleInventoryItem[]>> CollectAsync(
        InventoryModuleContext context, CancellationToken cancellationToken)
    {
        long started = context.TimeProvider.GetTimestamp();
        int raw = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records = await context.Session.InvokeAsync(
                PlatformDiscoveryCommands.WindowsRoles, cancellationToken);
            raw = records.Count;
            WindowsRoleInventoryItem[] items = WindowsRoleDiscoveryNormalizer.Normalize(records);
            return InventoryModuleResult<WindowsRoleInventoryItem[]>.Success(
                items, items.Length == 0, raw, items.Length,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            return InventoryModuleResult<WindowsRoleInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), raw,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

internal sealed class WindowsFeatureDiscoveryModule
    : IInventoryModule<WindowsFeatureInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.WindowsFeature;
    public async Task<InventoryModuleResult<WindowsFeatureInventoryItem[]>> CollectAsync(
        InventoryModuleContext context, CancellationToken cancellationToken)
    {
        long started = context.TimeProvider.GetTimestamp();
        int raw = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records = await context.Session.InvokeAsync(
                PlatformDiscoveryCommands.WindowsFeatures, cancellationToken);
            raw = records.Count;
            WindowsFeatureInventoryItem[] items =
                WindowsFeatureDiscoveryNormalizer.Normalize(records);
            return InventoryModuleResult<WindowsFeatureInventoryItem[]>.Success(
                items, items.Length == 0, raw, items.Length,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            return InventoryModuleResult<WindowsFeatureInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), raw,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

internal sealed class IisPlatformDiscoveryModule
    : IInventoryModule<IisPlatformInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.IisPlatform;
    public async Task<InventoryModuleResult<IisPlatformInventoryItem[]>> CollectAsync(
        InventoryModuleContext context, CancellationToken cancellationToken)
    {
        long started = context.TimeProvider.GetTimestamp();
        int raw = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records = await context.Session.InvokeAsync(
                PlatformDiscoveryCommands.Iis, cancellationToken);
            raw = records.Count;
            IisPlatformInventoryItem[] items = IisPlatformDiscoveryNormalizer.Normalize(records);
            return InventoryModuleResult<IisPlatformInventoryItem[]>.Success(
                items, items.Length == 0, raw, items.Length,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            return InventoryModuleResult<IisPlatformInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), raw,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

internal sealed class DotNetPlatformDiscoveryModule
    : IInventoryModule<DotNetPlatformInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.DotNetPlatform;
    public async Task<InventoryModuleResult<DotNetPlatformInventoryItem[]>> CollectAsync(
        InventoryModuleContext context, CancellationToken cancellationToken)
    {
        long started = context.TimeProvider.GetTimestamp();
        int raw = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records = await context.Session.InvokeAsync(
                PlatformDiscoveryCommands.DotNet, cancellationToken);
            raw = records.Count;
            DotNetPlatformInventoryItem[] items =
                DotNetPlatformDiscoveryNormalizer.Normalize(records);
            return InventoryModuleResult<DotNetPlatformInventoryItem[]>.Success(
                items, items.Length == 0, raw, items.Length,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            return InventoryModuleResult<DotNetPlatformInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), raw,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

internal sealed class PowerShellPlatformDiscoveryModule
    : IInventoryModule<PowerShellPlatformInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.PowerShellPlatform;
    public async Task<InventoryModuleResult<PowerShellPlatformInventoryItem[]>> CollectAsync(
        InventoryModuleContext context, CancellationToken cancellationToken)
    {
        long started = context.TimeProvider.GetTimestamp();
        int raw = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records = await context.Session.InvokeAsync(
                PlatformDiscoveryCommands.PowerShell, cancellationToken);
            raw = records.Count;
            PowerShellPlatformInventoryItem[] items =
                PowerShellPlatformDiscoveryNormalizer.Normalize(records);
            return InventoryModuleResult<PowerShellPlatformInventoryItem[]>.Success(
                items, items.Length == 0, raw, items.Length,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            return InventoryModuleResult<PowerShellPlatformInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), raw,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
