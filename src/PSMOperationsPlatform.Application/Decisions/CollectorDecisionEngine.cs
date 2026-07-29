using PSMOperationsPlatform.Application.Capabilities;

namespace PSMOperationsPlatform.Application.Decisions;

public enum DecisionSubject { ManagedTargetServer = 1 }
public enum CollectorStrategyCategory { Inventory = 1, Logs = 2, Diagnostics = 3, Monitoring = 4 }
public enum CollectorDecisionStatus { Eligible = 1, Blocked = 2, Indeterminate = 3, NotApplicable = 4, Disabled = 5, Invalid = 6 }
public enum EligibilityStatus { Eligible = 1, Ineligible = 2, Unknown = 3, NotApplicable = 4, Invalid = 5 }
public enum ExecutionReadinessStatus { Ready = 1, Blocked = 2, Unknown = 3, NotApplicable = 4, Invalid = 5 }

public static class CollectorStrategyCodes
{
    public const string WindowsCoreInventory = "WindowsCoreInventoryStrategy";
    public const string IisPlatformInventory = "IisPlatformInventoryStrategy";
    public const string IisLogCollection = "IisLogCollectionStrategy";
    public const string FailedRequestTracingLog = "FailedRequestTracingLogStrategy";
    public const string AspNetFrameworkLog = "AspNetFrameworkLogStrategy";
    public const string AspNetCoreIisLog = "AspNetCoreIisLogStrategy";
    public const string DotNetRuntimeDiagnostics = "DotNetRuntimeDiagnosticsStrategy";
    public const string PowerShell7TargetDiagnostics = "PowerShell7TargetDiagnosticsStrategy";
}

public static class CollectorDecisionReasonCodes
{
    public const string StrategyEligible = nameof(StrategyEligible);
    public const string RequiredCapabilityNotSupported = nameof(RequiredCapabilityNotSupported);
    public const string RequiredCapabilityNotReady = nameof(RequiredCapabilityNotReady);
    public const string RequiredCapabilityUnknown = nameof(RequiredCapabilityUnknown);
    public const string RequiredCapabilityInvalid = nameof(RequiredCapabilityInvalid);
    public const string IisNotInstalled = nameof(IisNotInstalled);
    public const string HostingBundleMissing = nameof(HostingBundleMissing);
    public const string AspNetCoreRuntimeMissing = nameof(AspNetCoreRuntimeMissing);
    public const string AspNetFrameworkSupportMissing = nameof(AspNetFrameworkSupportMissing);
    public const string FailedRequestTracingUnknown = nameof(FailedRequestTracingUnknown);
    public const string PowerShell51Missing = nameof(PowerShell51Missing);
    public const string PowerShell7Missing = nameof(PowerShell7Missing);
    public const string OperationalPermissionUnknown = nameof(OperationalPermissionUnknown);
    public const string StrategyNotApplicable = nameof(StrategyNotApplicable);
    public const string StrategyDisabledByProductPolicy = nameof(StrategyDisabledByProductPolicy);
    public const string StrategyRuleFailure = nameof(StrategyRuleFailure);
    public const string InvalidDecisionInput = nameof(InvalidDecisionInput);
}

public sealed record CapabilityDecisionProvenance(
    string CapabilityCode,
    CapabilityCategory CapabilityCategory,
    int CapabilityRuleVersion,
    CapabilityStatus SupportStatus,
    CapabilityStatus ReadinessStatus,
    CapabilityEvaluationStatus EvaluationStatus,
    string ReasonCode,
    Guid CapabilitySnapshotId,
    Guid SourceInventoryRunId,
    long SourceInventoryVersion);

public sealed record CollectorDecisionInput(
    Guid ManagedServerId,
    string? TargetFqdn,
    Guid CapabilitySnapshotId,
    int CapabilitySchemaVersion,
    Guid SourceInventoryRunId,
    long SourceInventoryVersion,
    CapabilitySubject Subject,
    CapabilityEvaluationStatus EvaluationStatus,
    DateTime EvaluatedAt,
    IReadOnlyList<CapabilityEntry> Capabilities);

public sealed record CollectorStrategyDecision(
    string StrategyCode,
    int StrategyVersion,
    string DisplayName,
    string Description,
    DecisionSubject Subject,
    CollectorStrategyCategory Category,
    EligibilityStatus EligibilityStatus,
    ExecutionReadinessStatus ExecutionReadinessStatus,
    CollectorDecisionStatus DecisionStatus,
    int Priority,
    int ExecutionOrder,
    bool IsReadOnly,
    bool RequiresManualApproval,
    string ReasonCode,
    string Explanation,
    IReadOnlyList<string> SatisfiedCapabilities,
    IReadOnlyList<string> BlockingCapabilities,
    IReadOnlyList<string> UnknownCapabilities,
    IReadOnlyList<string> InvalidCapabilities,
    IReadOnlyList<string> OptionalCapabilities,
    IReadOnlyList<CapabilityDecisionProvenance> Provenance,
    IReadOnlyList<string> Warnings);

public sealed record CollectorDecisionPlan(
    Guid ManagedServerId,
    Guid CapabilitySnapshotId,
    Guid SourceInventoryRunId,
    long SourceInventoryVersion,
    int CapabilitySchemaVersion,
    int DecisionSchemaVersion,
    DateTime EvaluatedAt,
    CollectorDecisionStatus OverallStatus,
    IReadOnlyList<CollectorStrategyDecision> Strategies,
    IReadOnlyList<string> Warnings)
{
    public int StrategyCount => Strategies.Count;
    public int EligibleCount => Strategies.Count(x => x.DecisionStatus == CollectorDecisionStatus.Eligible);
    public int BlockedCount => Strategies.Count(x => x.DecisionStatus == CollectorDecisionStatus.Blocked);
    public int IndeterminateCount => Strategies.Count(x => x.DecisionStatus == CollectorDecisionStatus.Indeterminate);
    public int NotApplicableCount => Strategies.Count(x => x.DecisionStatus == CollectorDecisionStatus.NotApplicable);
    public int DisabledCount => Strategies.Count(x => x.DecisionStatus == CollectorDecisionStatus.Disabled);
    public int InvalidCount => Strategies.Count(x => x.DecisionStatus == CollectorDecisionStatus.Invalid);
}

public interface ICollectorDecisionEngine
{
    CollectorDecisionPlan Evaluate(CollectorDecisionInput input);
}

public interface ICollectorDecisionRule
{
    string StrategyCode { get; }
    int StrategyVersion { get; }
    CollectorStrategyDecision Evaluate(CollectorDecisionInput input);
}

public sealed class CollectorDecisionEngine : ICollectorDecisionEngine
{
    public const int SchemaVersion = 1;
    private readonly ICollectorDecisionRule[] rules;

    public CollectorDecisionEngine() : this(CollectorDecisionCatalog.CreateRules()) { }

    public CollectorDecisionEngine(IEnumerable<ICollectorDecisionRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        this.rules = rules.ToArray();
        if (this.rules.Any(x => x.StrategyVersion < 1)
            || this.rules.Select(x => x.StrategyCode).Distinct(StringComparer.Ordinal).Count() != this.rules.Length)
            throw new ArgumentException("Decision rules must have unique codes and positive versions.", nameof(rules));
    }

    public CollectorDecisionPlan Evaluate(CollectorDecisionInput input)
    {
        Validate(input);
        var decisions = new List<CollectorStrategyDecision>(rules.Length);
        foreach (ICollectorDecisionRule rule in rules)
        {
            try { decisions.Add(rule.Evaluate(input)); }
            catch (OperationCanceledException) { throw; }
            catch
            {
                decisions.Add(CollectorDecisionRule.Invalid(rule,
                    CollectorDecisionReasonCodes.StrategyRuleFailure,
                    "The strategy rule could not produce a valid decision."));
            }
        }
        CollectorStrategyDecision[] ordered = decisions
            .OrderBy(x => x.Priority).ThenBy(x => x.ExecutionOrder)
            .ThenBy(x => x.StrategyCode, StringComparer.Ordinal).ToArray();
        CollectorDecisionStatus overall = ordered.Any(x => x.DecisionStatus == CollectorDecisionStatus.Invalid)
            ? CollectorDecisionStatus.Invalid
            : ordered.Any(x => x.DecisionStatus == CollectorDecisionStatus.Indeterminate)
                ? CollectorDecisionStatus.Indeterminate
                : CollectorDecisionStatus.Eligible;
        return new(input.ManagedServerId, input.CapabilitySnapshotId, input.SourceInventoryRunId,
            input.SourceInventoryVersion, input.CapabilitySchemaVersion, SchemaVersion,
            input.EvaluatedAt, overall, ordered, []);
    }

    private static void Validate(CollectorDecisionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ManagedServerId == Guid.Empty || input.CapabilitySnapshotId == Guid.Empty
            || input.SourceInventoryRunId == Guid.Empty || input.SourceInventoryVersion < 1
            || input.CapabilitySchemaVersion != CapabilityEngine.SchemaVersion
            || input.Subject != CapabilitySubject.ManagedTargetServer
            || input.EvaluationStatus != CapabilityEvaluationStatus.Succeeded
            || input.EvaluatedAt == default || input.Capabilities is null)
            throw new ArgumentException(CollectorDecisionReasonCodes.InvalidDecisionInput, nameof(input));
        CapabilityEntry[] entries = input.Capabilities.ToArray();
        if (entries.Select(x => x.CapabilityCode).Distinct(StringComparer.Ordinal).Count() != entries.Length
            || entries.Any(x => string.IsNullOrWhiteSpace(x.CapabilityCode) || x.RuleVersion < 1
                || x.Subject != input.Subject))
            throw new ArgumentException(CollectorDecisionReasonCodes.InvalidDecisionInput, nameof(input));
    }
}

internal static class CollectorDecisionCatalog
{
    public static ICollectorDecisionRule[] CreateRules() =>
    [
        new CollectorDecisionRule(CollectorStrategyCodes.WindowsCoreInventory, "Windows Core Inventory",
            "Continued eligibility for approved read-only Windows core inventory.", CollectorStrategyCategory.Inventory,
            100, 100, [CapabilityCodes.SupportsWindowsPowerShell51, CapabilityCodes.CanRunWindowsPowerShell51Collection]),
        new CollectorDecisionRule(CollectorStrategyCodes.IisPlatformInventory, "IIS Platform Inventory",
            "Read-only IIS platform inventory.", CollectorStrategyCategory.Inventory,
            200, 200, [CapabilityCodes.SupportsIis, CapabilityCodes.CanCollectIisPlatformInventory], iisStrategy: true),
        new CollectorDecisionRule(CollectorStrategyCodes.IisLogCollection, "IIS Log Collection",
            "Read-only IIS log collection eligibility.", CollectorStrategyCategory.Logs,
            300, 300, [CapabilityCodes.SupportsIis, CapabilityCodes.CanCollectIisLogs], iisStrategy: true),
        new CollectorDecisionRule(CollectorStrategyCodes.FailedRequestTracingLog, "Failed Request Tracing Logs",
            "Read-only Failed Request Tracing log eligibility.", CollectorStrategyCategory.Logs,
            300, 310, [CapabilityCodes.SupportsIis, CapabilityCodes.CanCollectFailedRequestTracingLogs], iisStrategy: true),
        new CollectorDecisionRule(CollectorStrategyCodes.AspNetFrameworkLog, "ASP.NET Framework Logs",
            "Read-only ASP.NET Framework log eligibility.", CollectorStrategyCategory.Logs,
            300, 320, [CapabilityCodes.SupportsIis, CapabilityCodes.SupportsAspNetFramework, CapabilityCodes.CanCollectAspNetFrameworkLogs], iisStrategy: true),
        new CollectorDecisionRule(CollectorStrategyCodes.AspNetCoreIisLog, "ASP.NET Core IIS Logs",
            "Read-only ASP.NET Core IIS log eligibility.", CollectorStrategyCategory.Logs,
            300, 330, [CapabilityCodes.SupportsIis, CapabilityCodes.SupportsAspNetCore,
                CapabilityCodes.HasAspNetCoreHostingBundle, CapabilityCodes.CanCollectAspNetCoreIisLogs], iisStrategy: true),
        new CollectorDecisionRule(CollectorStrategyCodes.DotNetRuntimeDiagnostics, ".NET Runtime Diagnostics",
            "Future read-only runtime diagnostics platform eligibility.", CollectorStrategyCategory.Diagnostics,
            400, 400, [CapabilityCodes.SupportsDotNetRuntime], [CapabilityCodes.SupportsDotNet10], platformOnly: true),
        new CollectorDecisionRule(CollectorStrategyCodes.PowerShell7TargetDiagnostics, "PowerShell 7 Target Diagnostics",
            "Future read-only target PowerShell 7 diagnostics eligibility.", CollectorStrategyCategory.Diagnostics,
            400, 410, [CapabilityCodes.SupportsPowerShell7], platformOnly: true),
    ];
}

internal sealed class CollectorDecisionRule(
    string strategyCode, string displayName, string description,
    CollectorStrategyCategory category, int priority, int executionOrder,
    string[] required, string[]? optional = null, bool iisStrategy = false,
    bool platformOnly = false) : ICollectorDecisionRule
{
    public string StrategyCode => strategyCode;
    public int StrategyVersion => 1;

    public CollectorStrategyDecision Evaluate(CollectorDecisionInput input)
    {
        var byCode = input.Capabilities.ToDictionary(x => x.CapabilityCode, StringComparer.Ordinal);
        CapabilityEntry[] evaluated = required.Concat(optional ?? [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(code => byCode.TryGetValue(code, out CapabilityEntry? entry)
                ? entry : Missing(code)).ToArray();
        CapabilityEntry[] requiredEntries = evaluated.Where(x => required.Contains(x.CapabilityCode, StringComparer.Ordinal)).ToArray();
        string[] invalid = Codes(requiredEntries, x => x.SupportStatus == CapabilityStatus.Invalid || x.ReadinessStatus == CapabilityStatus.Invalid);
        string[] unknown = Codes(requiredEntries, x => x.SupportStatus == CapabilityStatus.Unknown || x.ReadinessStatus == CapabilityStatus.Unknown);
        string[] blocking = Codes(requiredEntries, x => x.SupportStatus == CapabilityStatus.NotSupported || x.ReadinessStatus == CapabilityStatus.NotSupported);
        string[] satisfied = Codes(requiredEntries, x => x.SupportStatus == CapabilityStatus.Supported
            && (platformOnly || x.ReadinessStatus == CapabilityStatus.Supported));
        bool iisAbsent = iisStrategy && byCode.TryGetValue(CapabilityCodes.SupportsIis, out CapabilityEntry? iis)
            && iis.SupportStatus == CapabilityStatus.NotSupported;

        CollectorDecisionStatus status = invalid.Length > 0 ? CollectorDecisionStatus.Invalid
            : iisAbsent ? CollectorDecisionStatus.NotApplicable
            : unknown.Length > 0 ? CollectorDecisionStatus.Indeterminate
            : blocking.Length > 0 ? CollectorDecisionStatus.Blocked : CollectorDecisionStatus.Eligible;
        EligibilityStatus eligibility = status switch
        {
            CollectorDecisionStatus.Eligible or CollectorDecisionStatus.Indeterminate when
                !requiredEntries.Any(x => x.SupportStatus != CapabilityStatus.Supported) => EligibilityStatus.Eligible,
            CollectorDecisionStatus.NotApplicable => EligibilityStatus.NotApplicable,
            CollectorDecisionStatus.Invalid => EligibilityStatus.Invalid,
            CollectorDecisionStatus.Indeterminate => EligibilityStatus.Unknown,
            _ => EligibilityStatus.Ineligible
        };
        ExecutionReadinessStatus readiness = status switch
        {
            CollectorDecisionStatus.Eligible => ExecutionReadinessStatus.Ready,
            CollectorDecisionStatus.NotApplicable => ExecutionReadinessStatus.NotApplicable,
            CollectorDecisionStatus.Invalid => ExecutionReadinessStatus.Invalid,
            CollectorDecisionStatus.Indeterminate => ExecutionReadinessStatus.Unknown,
            _ => ExecutionReadinessStatus.Blocked
        };
        string reason = Reason(status, blocking, unknown);
        return new(strategyCode, StrategyVersion, displayName, description,
            DecisionSubject.ManagedTargetServer, category, eligibility, readiness, status,
            priority, executionOrder, true, false, reason, Explanation(status),
            satisfied, blocking, unknown, invalid,
            (optional ?? []).Order(StringComparer.Ordinal).ToArray(),
            evaluated.Select(x => Provenance(input, x)).ToArray(), []);
    }

    public static CollectorStrategyDecision Invalid(ICollectorDecisionRule rule, string reason, string explanation) =>
        new(rule.StrategyCode, rule.StrategyVersion, rule.StrategyCode, rule.StrategyCode,
            DecisionSubject.ManagedTargetServer, CollectorStrategyCategory.Diagnostics,
            EligibilityStatus.Invalid, ExecutionReadinessStatus.Invalid, CollectorDecisionStatus.Invalid,
            400, 9999, true, false, reason, explanation, [], [], [], [], [], [], []);

    private string Reason(CollectorDecisionStatus status, string[] blocking, string[] unknown)
    {
        if (status == CollectorDecisionStatus.Eligible) return CollectorDecisionReasonCodes.StrategyEligible;
        if (status == CollectorDecisionStatus.NotApplicable) return CollectorDecisionReasonCodes.IisNotInstalled;
        if (status == CollectorDecisionStatus.Invalid) return CollectorDecisionReasonCodes.RequiredCapabilityInvalid;
        if (unknown.Contains(CapabilityCodes.CanCollectFailedRequestTracingLogs, StringComparer.Ordinal))
            return CollectorDecisionReasonCodes.FailedRequestTracingUnknown;
        if (unknown.Length > 0 && unknown.Any(x => x.StartsWith("Can", StringComparison.Ordinal)))
            return CollectorDecisionReasonCodes.OperationalPermissionUnknown;
        if (unknown.Length > 0) return CollectorDecisionReasonCodes.RequiredCapabilityUnknown;
        if (blocking.Contains(CapabilityCodes.HasAspNetCoreHostingBundle, StringComparer.Ordinal))
            return CollectorDecisionReasonCodes.HostingBundleMissing;
        if (blocking.Contains(CapabilityCodes.SupportsAspNetCore, StringComparer.Ordinal))
            return CollectorDecisionReasonCodes.AspNetCoreRuntimeMissing;
        if (blocking.Contains(CapabilityCodes.SupportsAspNetFramework, StringComparer.Ordinal))
            return CollectorDecisionReasonCodes.AspNetFrameworkSupportMissing;
        if (blocking.Contains(CapabilityCodes.SupportsWindowsPowerShell51, StringComparer.Ordinal))
            return CollectorDecisionReasonCodes.PowerShell51Missing;
        if (blocking.Contains(CapabilityCodes.SupportsPowerShell7, StringComparer.Ordinal))
            return CollectorDecisionReasonCodes.PowerShell7Missing;
        return CollectorDecisionReasonCodes.RequiredCapabilityNotSupported;
    }

    private static string Explanation(CollectorDecisionStatus status) => status switch
    {
        CollectorDecisionStatus.Eligible => "All documented capability prerequisites are satisfied.",
        CollectorDecisionStatus.Blocked => "The strategy is relevant, but a documented prerequisite is confirmed unavailable or not ready.",
        CollectorDecisionStatus.Indeterminate => "The strategy cannot be evaluated because required capability evidence or operational readiness is unavailable.",
        CollectorDecisionStatus.NotApplicable => "The strategy does not apply because IIS is not installed on the managed target.",
        _ => "The strategy decision is invalid because capability evidence is inconsistent."
    };

    private static string[] Codes(IEnumerable<CapabilityEntry> entries, Func<CapabilityEntry, bool> predicate) =>
        entries.Where(predicate).Select(x => x.CapabilityCode).Order(StringComparer.Ordinal).ToArray();
    private static CapabilityEntry Missing(string code) =>
        new(code, CapabilitySubject.ManagedTargetServer, CapabilityCategory.Diagnostics,
            CapabilityStatus.Invalid, CapabilityStatus.Invalid, 1, "CapabilitySnapshotInvalid",
            "A required capability entry is unavailable.", [], [code], []);
    private static CapabilityDecisionProvenance Provenance(CollectorDecisionInput input, CapabilityEntry entry) =>
        new(entry.CapabilityCode, entry.Category, entry.RuleVersion, entry.SupportStatus,
            entry.ReadinessStatus, input.EvaluationStatus, entry.ReasonCode,
            input.CapabilitySnapshotId, input.SourceInventoryRunId, input.SourceInventoryVersion);
}
