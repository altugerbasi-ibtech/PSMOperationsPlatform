using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using PSMOperationsPlatform.Application.Decisions;

namespace PSMOperationsPlatform.Application.ExecutionPlanning;

public enum ExecutionPlanStatus { Ready = 1, PartiallyReady = 2, Empty = 3, Invalid = 4 }
public enum PlanningDisposition
{
    SkippedBlocked = 1,
    SkippedIndeterminate = 2,
    SkippedNotApplicable = 3,
    SkippedDisabled = 4,
    SkippedInvalid = 5,
    SkippedManualApproval = 6
}
public enum ThrottlingClass { Lightweight = 1, Standard = 2, Heavy = 3 }

public static class ExecutionPlanningReasonCodes
{
    public const string StrategyPlannedReadOnly = nameof(StrategyPlannedReadOnly);
    public const string StrategyBlocked = nameof(StrategyBlocked);
    public const string StrategyIndeterminate = nameof(StrategyIndeterminate);
    public const string StrategyNotApplicable = nameof(StrategyNotApplicable);
    public const string StrategyDisabled = nameof(StrategyDisabled);
    public const string StrategyInvalid = nameof(StrategyInvalid);
    public const string ManualApprovalRequired = nameof(ManualApprovalRequired);
    public const string InvalidExecutionPlanInput = nameof(InvalidExecutionPlanInput);
    public const string InvalidExecutionPolicy = nameof(InvalidExecutionPolicy);
    public const string DependencyUnavailable = nameof(DependencyUnavailable);
    public const string DependencyCycle = nameof(DependencyCycle);
}

public static class ExecutionPolicyCodes
{
    public const int Version = 1;
    public const string ShortReadOnly = nameof(ShortReadOnly);
    public const string StandardReadOnly = nameof(StandardReadOnly);
    public const string LongReadOnly = nameof(LongReadOnly);
    public const string NoRetry = nameof(NoRetry);
    public const string StandardReadOnlyRetry = nameof(StandardReadOnlyRetry);
    public const string SerialCore = nameof(SerialCore);
    public const string ParallelReadOnlyA = nameof(ParallelReadOnlyA);
}

public sealed record ExecutionPlanStrategyInput(
    string StrategyCode,
    int StrategyVersion,
    DecisionSubject Subject,
    CollectorStrategyCategory Category,
    CollectorDecisionStatus DecisionStatus,
    EligibilityStatus EligibilityStatus,
    ExecutionReadinessStatus ExecutionReadinessStatus,
    int Priority,
    int ExecutionOrder,
    bool IsReadOnly,
    bool RequiresManualApproval,
    string ReasonCode,
    string Explanation,
    IReadOnlyList<string> BlockingCapabilities,
    IReadOnlyList<string> UnknownCapabilities,
    IReadOnlyList<CapabilityDecisionProvenance> Provenance,
    IReadOnlyList<string> Warnings);

public sealed record ExecutionPlanInput(
    Guid ManagedServerId,
    string? TargetFqdn,
    Guid DecisionPlanId,
    int DecisionSchemaVersion,
    Guid CapabilitySnapshotId,
    int CapabilitySchemaVersion,
    Guid SourceInventoryRunId,
    long SourceInventoryVersion,
    DateTime DecisionPlanEvaluatedAt,
    DecisionSubject Subject,
    IReadOnlyList<ExecutionPlanStrategyInput> Strategies,
    IReadOnlyList<string> Warnings);

public sealed record ExecutionPlanStep(
    Guid StepId,
    string StrategyCode,
    int StrategyVersion,
    DecisionSubject Subject,
    CollectorStrategyCategory Category,
    int StepSequence,
    int Priority,
    int ExecutionOrder,
    string ParallelGroupCode,
    string TimeoutPolicyCode,
    int TimeoutPolicyVersion,
    TimeSpan Timeout,
    string RetryPolicyCode,
    int RetryPolicyVersion,
    ThrottlingClass ThrottlingClass,
    string? BatchGroupCode,
    bool IsReadOnly,
    bool RequiresManualApproval,
    IReadOnlyList<string> DependencyStrategyCodes,
    CollectorDecisionStatus SourceDecisionStatus,
    string SourceDecisionReasonCode,
    string InclusionReasonCode,
    string Explanation,
    Guid SourceDecisionPlanId,
    Guid SourceCapabilitySnapshotId,
    Guid SourceInventoryRunId,
    long SourceInventoryVersion,
    int DecisionSchemaVersion,
    int CapabilitySchemaVersion);

public sealed record ExecutionPlanExclusion(
    string StrategyCode,
    int StrategyVersion,
    CollectorDecisionStatus SourceDecisionStatus,
    PlanningDisposition PlanningDisposition,
    string ReasonCode,
    string Explanation,
    IReadOnlyList<string> BlockingCapabilities,
    IReadOnlyList<string> UnknownCapabilities,
    IReadOnlyList<CapabilityDecisionProvenance> Provenance);

public sealed record ExecutionPlanResult(
    Guid ManagedServerId,
    Guid DecisionPlanId,
    Guid CapabilitySnapshotId,
    Guid SourceInventoryRunId,
    long SourceInventoryVersion,
    int CapabilitySchemaVersion,
    int DecisionSchemaVersion,
    int ExecutionPlanSchemaVersion,
    DateTime CreatedAt,
    ExecutionPlanStatus PlanStatus,
    IReadOnlyList<ExecutionPlanStep> Steps,
    IReadOnlyList<ExecutionPlanExclusion> Exclusions,
    IReadOnlyList<string> Warnings)
{
    public int StepCount => Steps.Count;
    public int ExclusionCount => Exclusions.Count;
}

public interface IExecutionPlanEngine
{
    ExecutionPlanResult Build(ExecutionPlanInput input);
}

public sealed class ExecutionPlanEngine(TimeProvider timeProvider) : IExecutionPlanEngine
{
    public const int SchemaVersion = 1;

    public ExecutionPlanResult Build(ExecutionPlanInput input)
    {
        ValidateInput(input);
        ExecutionPlanStrategyInput[] strategies = input.Strategies.ToArray();
        var steps = new List<(ExecutionPlanStrategyInput Source, ExecutionStrategyPolicy Policy)>();
        var exclusions = new List<ExecutionPlanExclusion>();

        foreach (ExecutionPlanStrategyInput strategy in strategies
                     .OrderBy(x => x.Priority)
                     .ThenBy(x => x.ExecutionOrder)
                     .ThenBy(x => x.StrategyCode, StringComparer.Ordinal))
        {
            if (strategy.DecisionStatus == CollectorDecisionStatus.Eligible
                && strategy.EligibilityStatus == EligibilityStatus.Eligible
                && strategy.ExecutionReadinessStatus == ExecutionReadinessStatus.Ready
                && !strategy.RequiresManualApproval)
            {
                if (!ExecutionStrategyPolicyCatalog.TryGet(strategy.StrategyCode, out ExecutionStrategyPolicy? policy))
                    throw new ArgumentException(ExecutionPlanningReasonCodes.InvalidExecutionPolicy, nameof(input));
                steps.Add((strategy, policy!));
                continue;
            }

            exclusions.Add(CreateExclusion(strategy));
        }

        ValidateDependencies(steps);
        var ordered = TopologicalOrder(steps);
        ReadOnlyCollection<ExecutionPlanStep> resultSteps = Array.AsReadOnly(
            ordered.Select((item, index) => CreateStep(input, item.Source, item.Policy, index + 1)).ToArray());
        ReadOnlyCollection<ExecutionPlanExclusion> resultExclusions = Array.AsReadOnly(
            exclusions.OrderBy(x => x.StrategyCode, StringComparer.Ordinal).ToArray());
        ExecutionPlanStatus status = resultSteps.Count == 0 ? ExecutionPlanStatus.Empty
            : resultExclusions.Count > 0 ? ExecutionPlanStatus.PartiallyReady
            : ExecutionPlanStatus.Ready;

        return new(input.ManagedServerId, input.DecisionPlanId, input.CapabilitySnapshotId,
            input.SourceInventoryRunId, input.SourceInventoryVersion,
            input.CapabilitySchemaVersion, input.DecisionSchemaVersion, SchemaVersion,
            timeProvider.GetLocalNow().DateTime, status, resultSteps, resultExclusions,
            Array.AsReadOnly(input.Warnings.Order(StringComparer.Ordinal).ToArray()));
    }

    private static void ValidateInput(ExecutionPlanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ManagedServerId == Guid.Empty || input.DecisionPlanId == Guid.Empty
            || input.CapabilitySnapshotId == Guid.Empty || input.SourceInventoryRunId == Guid.Empty
            || input.SourceInventoryVersion < 1
            || input.DecisionSchemaVersion != CollectorDecisionEngine.SchemaVersion
            || input.CapabilitySchemaVersion < 1 || input.DecisionPlanEvaluatedAt == default
            || input.Subject != DecisionSubject.ManagedTargetServer
            || input.Strategies is null || input.Warnings is null)
            throw new ArgumentException(ExecutionPlanningReasonCodes.InvalidExecutionPlanInput, nameof(input));

        ExecutionPlanStrategyInput[] strategies = input.Strategies.ToArray();
        if (strategies.Select(x => x.StrategyCode).Distinct(StringComparer.Ordinal).Count() != strategies.Length
            || strategies.Any(x => string.IsNullOrWhiteSpace(x.StrategyCode)
                || x.StrategyVersion < 1 || x.Priority < 1 || x.ExecutionOrder < 1
                || x.Subject != input.Subject || string.IsNullOrWhiteSpace(x.ReasonCode)
                || x.BlockingCapabilities is null || x.UnknownCapabilities is null
                || x.Provenance is null || x.Warnings is null
                || x.Provenance.Any(p => p.CapabilitySnapshotId != input.CapabilitySnapshotId
                    || p.SourceInventoryRunId != input.SourceInventoryRunId
                    || p.SourceInventoryVersion != input.SourceInventoryVersion)))
            throw new ArgumentException(ExecutionPlanningReasonCodes.InvalidExecutionPlanInput, nameof(input));
    }

    private static ExecutionPlanExclusion CreateExclusion(ExecutionPlanStrategyInput strategy)
    {
        (PlanningDisposition disposition, string reason, string explanation) =
            strategy.RequiresManualApproval
                ? (PlanningDisposition.SkippedManualApproval,
                    ExecutionPlanningReasonCodes.ManualApprovalRequired,
                    "The strategy requires manual approval and is not executable in this plan.")
                : strategy.ExecutionReadinessStatus == ExecutionReadinessStatus.Unknown
                    ? (PlanningDisposition.SkippedIndeterminate,
                        ExecutionPlanningReasonCodes.StrategyIndeterminate,
                        "The strategy is not executable because operational readiness is unknown.")
                : strategy.DecisionStatus switch
                {
                    CollectorDecisionStatus.Blocked => (PlanningDisposition.SkippedBlocked,
                        ExecutionPlanningReasonCodes.StrategyBlocked,
                        "The strategy is not executable because its decision is blocked."),
                    CollectorDecisionStatus.Indeterminate => (PlanningDisposition.SkippedIndeterminate,
                        ExecutionPlanningReasonCodes.StrategyIndeterminate,
                        "The strategy is not executable because required readiness or evidence is unknown."),
                    CollectorDecisionStatus.NotApplicable => (PlanningDisposition.SkippedNotApplicable,
                        ExecutionPlanningReasonCodes.StrategyNotApplicable,
                        "The strategy is not applicable to this managed target."),
                    CollectorDecisionStatus.Disabled => (PlanningDisposition.SkippedDisabled,
                        ExecutionPlanningReasonCodes.StrategyDisabled,
                        "The strategy is disabled by product policy."),
                    _ => (PlanningDisposition.SkippedInvalid,
                        ExecutionPlanningReasonCodes.StrategyInvalid,
                        "The strategy decision is inconsistent and cannot become an execution step.")
                };
        return new(strategy.StrategyCode, strategy.StrategyVersion, strategy.DecisionStatus,
            disposition, reason, explanation,
            Array.AsReadOnly(strategy.BlockingCapabilities.Order(StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(strategy.UnknownCapabilities.Order(StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(strategy.Provenance.OrderBy(x => x.CapabilityCode, StringComparer.Ordinal).ToArray()));
    }

    private static ExecutionPlanStep CreateStep(ExecutionPlanInput input,
        ExecutionPlanStrategyInput strategy, ExecutionStrategyPolicy policy, int sequence) =>
        new(DeterministicStepId(input.DecisionPlanId, strategy.StrategyCode),
            strategy.StrategyCode, strategy.StrategyVersion, strategy.Subject, strategy.Category,
            sequence, strategy.Priority, strategy.ExecutionOrder, policy.ParallelGroupCode,
            policy.TimeoutPolicyCode, policy.TimeoutPolicyVersion, policy.Timeout,
            policy.RetryPolicyCode, policy.RetryPolicyVersion, policy.ThrottlingClass,
            policy.BatchGroupCode, strategy.IsReadOnly, strategy.RequiresManualApproval,
            Array.AsReadOnly(policy.DependencyStrategyCodes.Order(StringComparer.Ordinal).ToArray()),
            strategy.DecisionStatus, strategy.ReasonCode,
            ExecutionPlanningReasonCodes.StrategyPlannedReadOnly,
            "The eligible and ready read-only strategy is included using an explicit execution policy.",
            input.DecisionPlanId, input.CapabilitySnapshotId, input.SourceInventoryRunId,
            input.SourceInventoryVersion, input.DecisionSchemaVersion, input.CapabilitySchemaVersion);

    private static Guid DeterministicStepId(Guid decisionPlanId, string strategyCode)
    {
        byte[] source = Encoding.UTF8.GetBytes($"{decisionPlanId:D}|{strategyCode}");
        byte[] hash = SHA256.HashData(source);
        return new Guid(hash.AsSpan(0, 16));
    }

    private static void ValidateDependencies(
        IReadOnlyList<(ExecutionPlanStrategyInput Source, ExecutionStrategyPolicy Policy)> steps)
    {
        var codes = steps.Select(x => x.Source.StrategyCode).ToHashSet(StringComparer.Ordinal);
        foreach ((ExecutionPlanStrategyInput source, ExecutionStrategyPolicy policy) in steps)
        {
            if (policy.DependencyStrategyCodes.Contains(source.StrategyCode, StringComparer.Ordinal)
                || policy.DependencyStrategyCodes.Any(x => !codes.Contains(x)))
                throw new ArgumentException(ExecutionPlanningReasonCodes.DependencyUnavailable);
        }
    }

    private static IReadOnlyList<(ExecutionPlanStrategyInput Source, ExecutionStrategyPolicy Policy)> TopologicalOrder(
        IReadOnlyList<(ExecutionPlanStrategyInput Source, ExecutionStrategyPolicy Policy)> steps)
    {
        var remaining = steps.ToDictionary(x => x.Source.StrategyCode, StringComparer.Ordinal);
        var complete = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<(ExecutionPlanStrategyInput, ExecutionStrategyPolicy)>(steps.Count);
        while (remaining.Count > 0)
        {
            var next = remaining.Values
                .Where(x => x.Policy.DependencyStrategyCodes.All(complete.Contains))
                .OrderBy(x => x.Source.Priority).ThenBy(x => x.Source.ExecutionOrder)
                .ThenBy(x => x.Source.StrategyCode, StringComparer.Ordinal).FirstOrDefault();
            if (next.Source is null)
                throw new ArgumentException(ExecutionPlanningReasonCodes.DependencyCycle);
            remaining.Remove(next.Source.StrategyCode);
            complete.Add(next.Source.StrategyCode);
            ordered.Add(next);
        }
        return ordered;
    }
}

internal sealed record ExecutionStrategyPolicy(
    string StrategyCode,
    string TimeoutPolicyCode,
    int TimeoutPolicyVersion,
    TimeSpan Timeout,
    string RetryPolicyCode,
    int RetryPolicyVersion,
    string ParallelGroupCode,
    ThrottlingClass ThrottlingClass,
    string? BatchGroupCode,
    IReadOnlyList<string> DependencyStrategyCodes);

internal static class ExecutionStrategyPolicyCatalog
{
    private static readonly IReadOnlyDictionary<string, ExecutionStrategyPolicy> Policies =
        new Dictionary<string, ExecutionStrategyPolicy>(StringComparer.Ordinal)
        {
            [CollectorStrategyCodes.WindowsCoreInventory] = Policy(CollectorStrategyCodes.WindowsCoreInventory,
                ExecutionPolicyCodes.StandardReadOnly, TimeSpan.FromMinutes(5),
                ExecutionPolicyCodes.StandardReadOnlyRetry, ExecutionPolicyCodes.SerialCore, ThrottlingClass.Standard),
            [CollectorStrategyCodes.IisPlatformInventory] = Policy(CollectorStrategyCodes.IisPlatformInventory,
                ExecutionPolicyCodes.StandardReadOnly, TimeSpan.FromMinutes(5),
                ExecutionPolicyCodes.StandardReadOnlyRetry, ExecutionPolicyCodes.SerialCore, ThrottlingClass.Standard),
            [CollectorStrategyCodes.IisLogCollection] = Policy(CollectorStrategyCodes.IisLogCollection,
                ExecutionPolicyCodes.LongReadOnly, TimeSpan.FromMinutes(15),
                ExecutionPolicyCodes.StandardReadOnlyRetry, ExecutionPolicyCodes.SerialCore, ThrottlingClass.Heavy),
            [CollectorStrategyCodes.FailedRequestTracingLog] = Policy(CollectorStrategyCodes.FailedRequestTracingLog,
                ExecutionPolicyCodes.LongReadOnly, TimeSpan.FromMinutes(15),
                ExecutionPolicyCodes.StandardReadOnlyRetry, ExecutionPolicyCodes.SerialCore, ThrottlingClass.Heavy),
            [CollectorStrategyCodes.AspNetFrameworkLog] = Policy(CollectorStrategyCodes.AspNetFrameworkLog,
                ExecutionPolicyCodes.LongReadOnly, TimeSpan.FromMinutes(15),
                ExecutionPolicyCodes.StandardReadOnlyRetry, ExecutionPolicyCodes.SerialCore, ThrottlingClass.Heavy),
            [CollectorStrategyCodes.AspNetCoreIisLog] = Policy(CollectorStrategyCodes.AspNetCoreIisLog,
                ExecutionPolicyCodes.LongReadOnly, TimeSpan.FromMinutes(15),
                ExecutionPolicyCodes.StandardReadOnlyRetry, ExecutionPolicyCodes.SerialCore, ThrottlingClass.Heavy),
            [CollectorStrategyCodes.DotNetRuntimeDiagnostics] = Policy(CollectorStrategyCodes.DotNetRuntimeDiagnostics,
                ExecutionPolicyCodes.StandardReadOnly, TimeSpan.FromMinutes(5),
                ExecutionPolicyCodes.NoRetry, ExecutionPolicyCodes.ParallelReadOnlyA, ThrottlingClass.Lightweight),
            [CollectorStrategyCodes.PowerShell7TargetDiagnostics] = Policy(CollectorStrategyCodes.PowerShell7TargetDiagnostics,
                ExecutionPolicyCodes.ShortReadOnly, TimeSpan.FromMinutes(1),
                ExecutionPolicyCodes.NoRetry, ExecutionPolicyCodes.ParallelReadOnlyA, ThrottlingClass.Lightweight),
        };

    public static bool TryGet(string strategyCode, out ExecutionStrategyPolicy? policy) =>
        Policies.TryGetValue(strategyCode, out policy);

    private static ExecutionStrategyPolicy Policy(string strategyCode, string timeoutCode,
        TimeSpan timeout, string retryCode, string parallelGroup, ThrottlingClass throttling) =>
        new(strategyCode, timeoutCode, ExecutionPolicyCodes.Version, timeout,
            retryCode, ExecutionPolicyCodes.Version, parallelGroup, throttling, null,
            Array.AsReadOnly(Array.Empty<string>()));
}
