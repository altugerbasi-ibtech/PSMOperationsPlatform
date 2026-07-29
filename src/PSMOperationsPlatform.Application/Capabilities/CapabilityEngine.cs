namespace PSMOperationsPlatform.Application.Capabilities;

public enum CapabilitySubject { ManagedTargetServer = 1 }
public enum CapabilityCategory { Platform = 1, Collection = 2, Monitoring = 3, Management = 4, Diagnostics = 5 }
public enum CapabilityStatus { Supported = 1, NotSupported = 2, Unknown = 3, NotApplicable = 4, Invalid = 5 }
public enum CapabilityEvaluationStatus { Succeeded = 1, Invalid = 2 }

public static class CapabilityCodes
{
    public const string SupportsIis = nameof(SupportsIis);
    public const string CanCollectIisPlatformInventory = nameof(CanCollectIisPlatformInventory);
    public const string CanCollectIisLogs = nameof(CanCollectIisLogs);
    public const string CanCollectFailedRequestTracingLogs = nameof(CanCollectFailedRequestTracingLogs);
    public const string SupportsAspNetFramework = nameof(SupportsAspNetFramework);
    public const string CanCollectAspNetFrameworkLogs = nameof(CanCollectAspNetFrameworkLogs);
    public const string SupportsDotNetRuntime = nameof(SupportsDotNetRuntime);
    public const string SupportsAspNetCore = nameof(SupportsAspNetCore);
    public const string HasAspNetCoreHostingBundle = nameof(HasAspNetCoreHostingBundle);
    public const string CanCollectAspNetCoreIisLogs = nameof(CanCollectAspNetCoreIisLogs);
    public const string SupportsDotNet10 = nameof(SupportsDotNet10);
    public const string SupportsDotNetSdk = nameof(SupportsDotNetSdk);
    public const string SupportsWindowsPowerShell51 = nameof(SupportsWindowsPowerShell51);
    public const string CanRunWindowsPowerShell51Collection = nameof(CanRunWindowsPowerShell51Collection);
    public const string SupportsPowerShell7 = nameof(SupportsPowerShell7);
    public const string CanRunPowerShell7CollectorTooling = nameof(CanRunPowerShell7CollectorTooling);
}

public static class CapabilityRuleVersions
{
    public const int SupportsIis = 1;
    public const int CanCollectIisPlatformInventory = 1;
    public const int CanCollectIisLogs = 1;
    public const int CanCollectFailedRequestTracingLogs = 1;
    public const int SupportsAspNetFramework = 1;
    public const int CanCollectAspNetFrameworkLogs = 1;
    public const int SupportsDotNetRuntime = 1;
    public const int SupportsAspNetCore = 1;
    public const int HasAspNetCoreHostingBundle = 1;
    public const int CanCollectAspNetCoreIisLogs = 1;
    public const int SupportsDotNet10 = 1;
    public const int SupportsDotNetSdk = 1;
    public const int SupportsWindowsPowerShell51 = 1;
    public const int CanRunWindowsPowerShell51Collection = 1;
    public const int SupportsPowerShell7 = 1;
    public const int CanRunPowerShell7CollectorTooling = 1;
}

public sealed record PlatformFactReference(string Category, string FactKey, string ModuleName);
public sealed record IisCapabilityFact(string Key, bool Installed, string? Version);
public sealed record DotNetCapabilityFact(string Key, string Category, string? Version, int? Release);
public sealed record PowerShellCapabilityFact(string Key, string Edition, string? Version);
public sealed record WindowsFeatureCapabilityFact(string Key, string Name);

public sealed record PlatformCapabilityInput(
    Guid ManagedServerId,
    Guid InventoryRunId,
    long InventoryVersion,
    DateTime CapturedAt,
    IReadOnlyList<IisCapabilityFact>? Iis,
    IReadOnlyList<DotNetCapabilityFact>? DotNet,
    IReadOnlyList<PowerShellCapabilityFact>? PowerShell,
    IReadOnlyList<WindowsFeatureCapabilityFact>? Roles,
    IReadOnlyList<WindowsFeatureCapabilityFact>? Features,
    bool SourceInventoryValid = true);

public sealed record CapabilityEntry(
    string CapabilityCode,
    CapabilitySubject Subject,
    CapabilityCategory Category,
    CapabilityStatus SupportStatus,
    CapabilityStatus ReadinessStatus,
    int RuleVersion,
    string ReasonCode,
    string Reason,
    IReadOnlyList<string> SatisfiedPrerequisites,
    IReadOnlyList<string> MissingPrerequisites,
    IReadOnlyList<PlatformFactReference> SourceFacts);

public sealed record CapabilityEvaluationResult(
    Guid ManagedServerId,
    Guid InventoryRunId,
    long SourceInventoryVersion,
    DateTime EvaluatedAt,
    int CapabilitySchemaVersion,
    CapabilityEvaluationStatus EvaluationStatus,
    IReadOnlyList<CapabilityEntry> Entries,
    IReadOnlyList<string> Warnings);

public interface ICapabilityEngine
{
    CapabilityEvaluationResult Evaluate(PlatformCapabilityInput input);
}

public sealed class CapabilityEngine(TimeProvider timeProvider) : ICapabilityEngine
{
    public const int SchemaVersion = 1;

    public CapabilityEvaluationResult Evaluate(PlatformCapabilityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ManagedServerId == Guid.Empty || input.InventoryRunId == Guid.Empty
            || input.InventoryVersion < 1 || input.CapturedAt == default
            || !input.SourceInventoryValid)
        {
            throw new ArgumentException("Capability source inventory is invalid.", nameof(input));
        }

        bool? iis = input.Iis is null ? null : input.Iis.Any(x => x.Installed);
        bool? framework = HasCategory(input.DotNet, "Framework");
        bool? runtime = HasCategory(input.DotNet, "Runtime");
        bool? aspnet = HasCategory(input.DotNet, "AspNetRuntime");
        bool? hosting = HasCategory(input.DotNet, "HostingBundle");
        bool? sdk = HasCategory(input.DotNet, "Sdk");
        bool? dotnet10 = HasMajor(input.DotNet, ["Runtime", "AspNetRuntime"], 10);
        bool? ps51 = HasPowerShell(input.PowerShell, "Desktop", 5, 1);
        bool? ps7 = HasPowerShell(input.PowerShell, "Core", 7, 0);
        bool? tracing = HasFeature(input.Features, "Web-Http-Tracing");

        var entries = new[]
        {
            Presence(CapabilityCodes.SupportsIis, CapabilityCategory.Platform, CapabilityRuleVersions.SupportsIis, iis, "IisNotInstalled", Facts(input.Iis, "IIS")),
            Operational(CapabilityCodes.CanCollectIisPlatformInventory, CapabilityRuleVersions.CanCollectIisPlatformInventory, iis, "IisNotInstalled", ["IIS installed"]),
            Operational(CapabilityCodes.CanCollectIisLogs, CapabilityRuleVersions.CanCollectIisLogs, iis, "IisNotInstalled", ["IIS installed"]),
            Composite(CapabilityCodes.CanCollectFailedRequestTracingLogs, CapabilityCategory.Collection, CapabilityRuleVersions.CanCollectFailedRequestTracingLogs, [iis, tracing],
                ["IIS installed", "Failed Request Tracing feature"], "RequiredFactMissing"),
            Presence(CapabilityCodes.SupportsAspNetFramework, CapabilityCategory.Platform, CapabilityRuleVersions.SupportsAspNetFramework, framework, "DotNetFrameworkNotFound", Facts(input.DotNet, "DotNet")),
            CompositeOperational(CapabilityCodes.CanCollectAspNetFrameworkLogs, CapabilityRuleVersions.CanCollectAspNetFrameworkLogs, [iis, framework],
                ["IIS installed", "ASP.NET Framework supported"]),
            Presence(CapabilityCodes.SupportsDotNetRuntime, CapabilityCategory.Platform, CapabilityRuleVersions.SupportsDotNetRuntime, runtime, "DotNetRuntimeNotFound", Facts(input.DotNet, "DotNet")),
            Presence(CapabilityCodes.SupportsAspNetCore, CapabilityCategory.Platform, CapabilityRuleVersions.SupportsAspNetCore, aspnet, "AspNetCoreRuntimeNotFound", Facts(input.DotNet, "DotNet")),
            Presence(CapabilityCodes.HasAspNetCoreHostingBundle, CapabilityCategory.Platform, CapabilityRuleVersions.HasAspNetCoreHostingBundle, hosting, "HostingBundleNotInstalled", Facts(input.DotNet, "DotNet")),
            Composite(CapabilityCodes.CanCollectAspNetCoreIisLogs, CapabilityCategory.Collection, CapabilityRuleVersions.CanCollectAspNetCoreIisLogs, [iis, aspnet, hosting],
                ["IIS installed", "ASP.NET Core runtime", "Hosting Bundle"], "RequiredFactMissing"),
            Presence(CapabilityCodes.SupportsDotNet10, CapabilityCategory.Platform, CapabilityRuleVersions.SupportsDotNet10, dotnet10, "DotNet10RuntimeNotFound", Facts(input.DotNet, "DotNet")),
            Presence(CapabilityCodes.SupportsDotNetSdk, CapabilityCategory.Platform, CapabilityRuleVersions.SupportsDotNetSdk, sdk, "DotNetSdkNotFound", Facts(input.DotNet, "DotNet")),
            Presence(CapabilityCodes.SupportsWindowsPowerShell51, CapabilityCategory.Platform, CapabilityRuleVersions.SupportsWindowsPowerShell51, ps51, "WindowsPowerShell51NotFound", Facts(input.PowerShell, "PowerShell")),
            Operational(CapabilityCodes.CanRunWindowsPowerShell51Collection, CapabilityRuleVersions.CanRunWindowsPowerShell51Collection, ps51, "WindowsPowerShell51NotFound", ["Windows PowerShell 5.1"]),
            Presence(CapabilityCodes.SupportsPowerShell7, CapabilityCategory.Diagnostics, CapabilityRuleVersions.SupportsPowerShell7, ps7, "PowerShell7NotFound", Facts(input.PowerShell, "PowerShell")),
            new CapabilityEntry(CapabilityCodes.CanRunPowerShell7CollectorTooling,
                CapabilitySubject.ManagedTargetServer, CapabilityCategory.Diagnostics, CapabilityStatus.NotApplicable,
                CapabilityStatus.NotApplicable, CapabilityRuleVersions.CanRunPowerShell7CollectorTooling, "NotApplicableToSubject",
                "Collector tooling capability is not applicable to a managed target server.",
                [], [], []),
        }.OrderBy(x => x.CapabilityCode, StringComparer.Ordinal).ToArray();

        return new(input.ManagedServerId, input.InventoryRunId, input.InventoryVersion,
            timeProvider.GetLocalNow().DateTime, SchemaVersion,
            CapabilityEvaluationStatus.Succeeded, entries, []);
    }

    private static CapabilityEntry Presence(string code, CapabilityCategory category, int ruleVersion, bool? value, string absent,
        IReadOnlyList<PlatformFactReference> facts) =>
        Entry(code, category, ruleVersion, value, value, absent, facts, ["Required platform fact"]);

    private static CapabilityEntry Operational(string code, int ruleVersion, bool? support, string absent,
        IReadOnlyList<string> prerequisites)
    {
        if (support == true)
            return new(code, CapabilitySubject.ManagedTargetServer, CapabilityCategory.Collection, CapabilityStatus.Supported,
                CapabilityStatus.Unknown, ruleVersion, "OperationalPermissionUnknown",
                "Platform support is present; operational access readiness is not inventoried.",
                prerequisites, ["Operational access facts"], []);
        return Entry(code, CapabilityCategory.Collection, ruleVersion, support, support, absent, [], prerequisites);
    }

    private static CapabilityEntry CompositeOperational(string code, int ruleVersion, bool?[] values,
        IReadOnlyList<string> prerequisites)
    {
        CapabilityEntry entry = Composite(code, CapabilityCategory.Collection, ruleVersion, values, prerequisites, "RequiredFactMissing");
        return entry.ReadinessStatus == CapabilityStatus.Supported
            ? entry with { ReadinessStatus = CapabilityStatus.Unknown,
                ReasonCode = "OperationalPermissionUnknown",
                Reason = "Platform prerequisites are present; operational access readiness is not inventoried." }
            : entry;
    }

    private static CapabilityEntry Composite(string code, CapabilityCategory category, int ruleVersion, bool?[] values,
        IReadOnlyList<string> prerequisites, string unknown)
    {
        bool? value = values.Any(x => x == false) ? false : values.Any(x => x is null) ? null : true;
        return Entry(code, category, ruleVersion, value, value, unknown, [], prerequisites);
    }

    private static CapabilityEntry Entry(string code, CapabilityCategory category, int ruleVersion, bool? support, bool? ready,
        string absentReason, IReadOnlyList<PlatformFactReference> facts,
        IReadOnlyList<string> prerequisites)
    {
        CapabilityStatus status = support is true ? CapabilityStatus.Supported
            : support is false ? CapabilityStatus.NotSupported : CapabilityStatus.Unknown;
        string reason = status == CapabilityStatus.Supported ? "CapabilitySatisfied"
            : status == CapabilityStatus.Unknown ? "RequiredFactMissing" : absentReason;
        return new(code, CapabilitySubject.ManagedTargetServer, category, status,
            ready is true ? CapabilityStatus.Supported
                : ready is false ? CapabilityStatus.NotSupported : CapabilityStatus.Unknown,
            ruleVersion, reason,
            status == CapabilityStatus.Supported ? "All documented platform prerequisites are satisfied."
                : status == CapabilityStatus.Unknown ? "Required normalized platform facts are unavailable."
                : "A documented platform prerequisite is not satisfied.",
            support == true ? prerequisites.Order(StringComparer.Ordinal).ToArray() : [],
            support == true ? [] : prerequisites.Order(StringComparer.Ordinal).ToArray(), facts);
    }

    private static bool? HasCategory(IReadOnlyList<DotNetCapabilityFact>? facts, string category) =>
        facts is null ? null : facts.Any(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    private static bool? HasFeature(IReadOnlyList<WindowsFeatureCapabilityFact>? facts, string name) =>
        facts is null ? null : facts.Any(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    private static bool? HasMajor(IReadOnlyList<DotNetCapabilityFact>? facts, string[] categories, int major) =>
        facts is null ? null : facts.Any(x => categories.Contains(x.Category, StringComparer.OrdinalIgnoreCase)
            && Version.TryParse(x.Version, out Version? v) && v.Major == major);
    private static bool? HasPowerShell(IReadOnlyList<PowerShellCapabilityFact>? facts,
        string edition, int major, int minor) =>
        facts is null ? null : facts.Any(x => x.Edition.Equals(edition, StringComparison.OrdinalIgnoreCase)
            && Version.TryParse(x.Version, out Version? v) && v >= new Version(major, minor));
    private static PlatformFactReference[] Facts<T>(IReadOnlyList<T>? facts, string module) =>
        facts is null ? [] : facts.Select((_, index) =>
            new PlatformFactReference(module, $"{module}:{index + 1}", module))
            .OrderBy(x => x.FactKey, StringComparer.Ordinal).ToArray();
}
