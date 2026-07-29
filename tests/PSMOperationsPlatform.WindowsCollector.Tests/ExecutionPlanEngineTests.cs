using System.Globalization;
using PSMOperationsPlatform.Application.Capabilities;
using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Application.ExecutionPlanning;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class ExecutionPlanEngineTests
{
    [Fact]
    public void Build_MapsAllEightEligibleReadyStrategies()
    {
        ExecutionPlanResult plan = Engine().Build(Input(AllStrategies()));

        Assert.Equal(ExecutionPlanStatus.Ready, plan.PlanStatus);
        Assert.Equal(8, plan.StepCount);
        Assert.Empty(plan.Exclusions);
        Assert.Equal(ExecutionPlanEngine.SchemaVersion, plan.ExecutionPlanSchemaVersion);
        Assert.Equal(Enumerable.Range(1, 8), plan.Steps.Select(x => x.StepSequence));
        Assert.All(plan.Steps, step =>
        {
            Assert.True(step.IsReadOnly);
            Assert.False(step.RequiresManualApproval);
            Assert.True(step.Timeout > TimeSpan.Zero);
            Assert.True(step.Timeout <= TimeSpan.FromHours(1));
            Assert.Equal(1, step.TimeoutPolicyVersion);
            Assert.Equal(1, step.RetryPolicyVersion);
            Assert.Empty(step.DependencyStrategyCodes);
            Assert.Null(step.BatchGroupCode);
        });
    }

    [Theory]
    [InlineData(CollectorDecisionStatus.Blocked, PlanningDisposition.SkippedBlocked, "StrategyBlocked")]
    [InlineData(CollectorDecisionStatus.Indeterminate, PlanningDisposition.SkippedIndeterminate, "StrategyIndeterminate")]
    [InlineData(CollectorDecisionStatus.NotApplicable, PlanningDisposition.SkippedNotApplicable, "StrategyNotApplicable")]
    [InlineData(CollectorDecisionStatus.Disabled, PlanningDisposition.SkippedDisabled, "StrategyDisabled")]
    [InlineData(CollectorDecisionStatus.Invalid, PlanningDisposition.SkippedInvalid, "StrategyInvalid")]
    public void Build_NonExecutableDecisionBecomesExplainableExclusion(
        CollectorDecisionStatus status, PlanningDisposition disposition, string reason)
    {
        ExecutionPlanStrategyInput strategy = Strategy(CollectorStrategyCodes.IisLogCollection)
            with
            {
                DecisionStatus = status,
                EligibilityStatus = status == CollectorDecisionStatus.Indeterminate
                    ? EligibilityStatus.Unknown : EligibilityStatus.Ineligible,
                ExecutionReadinessStatus = status == CollectorDecisionStatus.Indeterminate
                    ? ExecutionReadinessStatus.Unknown : ExecutionReadinessStatus.Blocked
            };

        ExecutionPlanResult plan = Engine().Build(Input([strategy]));
        ExecutionPlanExclusion exclusion = Assert.Single(plan.Exclusions);

        Assert.Equal(ExecutionPlanStatus.Empty, plan.PlanStatus);
        Assert.Empty(plan.Steps);
        Assert.Equal(disposition, exclusion.PlanningDisposition);
        Assert.Equal(reason, exclusion.ReasonCode);
        Assert.NotEmpty(exclusion.Explanation);
    }

    [Fact]
    public void Build_EligibleWithUnknownReadinessIsNotPromoted()
    {
        ExecutionPlanStrategyInput strategy = Strategy(CollectorStrategyCodes.IisLogCollection)
            with { ExecutionReadinessStatus = ExecutionReadinessStatus.Unknown };

        ExecutionPlanResult plan = Engine().Build(Input([strategy]));

        Assert.Empty(plan.Steps);
        Assert.Equal(PlanningDisposition.SkippedIndeterminate,
            Assert.Single(plan.Exclusions).PlanningDisposition);
    }

    [Fact]
    public void Build_ManualApprovalIsNotSilentlyExecutable()
    {
        ExecutionPlanResult plan = Engine().Build(Input(
            [Strategy(CollectorStrategyCodes.IisLogCollection) with { RequiresManualApproval = true }]));

        ExecutionPlanExclusion exclusion = Assert.Single(plan.Exclusions);
        Assert.Equal(PlanningDisposition.SkippedManualApproval, exclusion.PlanningDisposition);
        Assert.Equal(ExecutionPlanningReasonCodes.ManualApprovalRequired, exclusion.ReasonCode);
    }

    [Fact]
    public void Build_AssignsExplicitProductPolicies()
    {
        ExecutionPlanResult plan = Engine().Build(Input(AllStrategies()));
        ExecutionPlanStep core = plan.Steps.Single(x => x.StrategyCode == CollectorStrategyCodes.WindowsCoreInventory);
        ExecutionPlanStep iisLogs = plan.Steps.Single(x => x.StrategyCode == CollectorStrategyCodes.IisLogCollection);
        ExecutionPlanStep diagnostics = plan.Steps.Single(x => x.StrategyCode == CollectorStrategyCodes.PowerShell7TargetDiagnostics);

        Assert.Equal(ExecutionPolicyCodes.SerialCore, core.ParallelGroupCode);
        Assert.Equal(ExecutionPolicyCodes.StandardReadOnly, core.TimeoutPolicyCode);
        Assert.Equal(ExecutionPolicyCodes.StandardReadOnlyRetry, core.RetryPolicyCode);
        Assert.Equal(ThrottlingClass.Standard, core.ThrottlingClass);
        Assert.Equal(TimeSpan.FromMinutes(15), iisLogs.Timeout);
        Assert.Equal(ThrottlingClass.Heavy, iisLogs.ThrottlingClass);
        Assert.Equal(ExecutionPolicyCodes.ParallelReadOnlyA, diagnostics.ParallelGroupCode);
        Assert.Equal(ExecutionPolicyCodes.NoRetry, diagnostics.RetryPolicyCode);
        Assert.Equal(TimeSpan.FromMinutes(1), diagnostics.Timeout);
    }

    [Fact]
    public void Build_IsDeterministicAcrossInputOrderAndCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            ExecutionPlanResult first = Engine().Build(Input(AllStrategies().Reverse().ToArray()));
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            ExecutionPlanResult second = Engine().Build(Input(AllStrategies()));

            Assert.Equal(first.Steps.Select(Identity), second.Steps.Select(Identity));
            Assert.Equal(first.Steps.Select(x => x.StepId), second.Steps.Select(x => x.StepId));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Build_MixedOutcomesProducesPartiallyReadyWithoutOverlap()
    {
        ExecutionPlanStrategyInput ready = Strategy(CollectorStrategyCodes.WindowsCoreInventory);
        ExecutionPlanStrategyInput blocked = Strategy(CollectorStrategyCodes.IisLogCollection)
            with
            {
                DecisionStatus = CollectorDecisionStatus.Blocked,
                EligibilityStatus = EligibilityStatus.Ineligible,
                ExecutionReadinessStatus = ExecutionReadinessStatus.Blocked
            };

        ExecutionPlanResult plan = Engine().Build(Input([blocked, ready]));

        Assert.Equal(ExecutionPlanStatus.PartiallyReady, plan.PlanStatus);
        Assert.Single(plan.Steps);
        Assert.Single(plan.Exclusions);
        Assert.Empty(plan.Steps.Select(x => x.StrategyCode)
            .Intersect(plan.Exclusions.Select(x => x.StrategyCode), StringComparer.Ordinal));
    }

    [Fact]
    public void Build_RejectsInvalidIdentityVersionsDuplicatesAndSubject()
    {
        ExecutionPlanInput valid = Input([Strategy(CollectorStrategyCodes.WindowsCoreInventory)]);
        Assert.Throws<ArgumentException>(() => Engine().Build(valid with { ManagedServerId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() => Engine().Build(valid with { DecisionPlanId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() => Engine().Build(valid with { CapabilitySnapshotId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() => Engine().Build(valid with { SourceInventoryRunId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() => Engine().Build(valid with { SourceInventoryVersion = 0 }));
        Assert.Throws<ArgumentException>(() => Engine().Build(valid with { DecisionSchemaVersion = 99 }));
        Assert.Throws<ArgumentException>(() => Engine().Build(valid with
        {
            Strategies = [.. valid.Strategies, valid.Strategies[0]]
        }));
        Assert.Throws<ArgumentException>(() => Engine().Build(valid with
        {
            Strategies = [valid.Strategies[0] with { StrategyVersion = 0 }]
        }));
    }

    [Fact]
    public void Build_RetainsSourceProvenanceAndUsesReadOnlyCollections()
    {
        ExecutionPlanResult plan = Engine().Build(Input(
            [Strategy(CollectorStrategyCodes.IisLogCollection)]));
        ExecutionPlanStep step = Assert.Single(plan.Steps);

        Assert.Equal(DecisionPlanId, step.SourceDecisionPlanId);
        Assert.Equal(CapabilitySnapshotId, step.SourceCapabilitySnapshotId);
        Assert.Equal(InventoryRunId, step.SourceInventoryRunId);
        Assert.Equal(12, step.SourceInventoryVersion);
        Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<ExecutionPlanStep>>(plan.Steps);
        Assert.IsType<System.Collections.ObjectModel.ReadOnlyCollection<string>>(step.DependencyStrategyCodes);
    }

    private static string Identity(ExecutionPlanStep step) =>
        $"{step.StepSequence}|{step.Priority}|{step.ExecutionOrder}|{step.StrategyCode}|{step.TimeoutPolicyCode}|{step.RetryPolicyCode}|{step.ParallelGroupCode}|{step.ThrottlingClass}";

    private static ExecutionPlanEngine Engine() => new(new PlanningTimeProvider());
    private static readonly Guid ServerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DecisionPlanId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CapabilitySnapshotId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid InventoryRunId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static ExecutionPlanInput Input(IReadOnlyList<ExecutionPlanStrategyInput> strategies) =>
        new(ServerId, "server.example.test", DecisionPlanId, 1, CapabilitySnapshotId, 1,
            InventoryRunId, 12, new DateTime(2026, 7, 29, 10, 0, 0),
            DecisionSubject.ManagedTargetServer, strategies, []);

    private static ExecutionPlanStrategyInput[] AllStrategies() =>
    [
        Strategy(CollectorStrategyCodes.WindowsCoreInventory, CollectorStrategyCategory.Inventory, 100, 100),
        Strategy(CollectorStrategyCodes.IisPlatformInventory, CollectorStrategyCategory.Inventory, 200, 200),
        Strategy(CollectorStrategyCodes.IisLogCollection, CollectorStrategyCategory.Logs, 300, 300),
        Strategy(CollectorStrategyCodes.FailedRequestTracingLog, CollectorStrategyCategory.Logs, 300, 310),
        Strategy(CollectorStrategyCodes.AspNetFrameworkLog, CollectorStrategyCategory.Logs, 300, 320),
        Strategy(CollectorStrategyCodes.AspNetCoreIisLog, CollectorStrategyCategory.Logs, 300, 330),
        Strategy(CollectorStrategyCodes.DotNetRuntimeDiagnostics, CollectorStrategyCategory.Diagnostics, 400, 400),
        Strategy(CollectorStrategyCodes.PowerShell7TargetDiagnostics, CollectorStrategyCategory.Diagnostics, 400, 410),
    ];

    private static ExecutionPlanStrategyInput Strategy(string code,
        CollectorStrategyCategory category = CollectorStrategyCategory.Logs,
        int priority = 300, int order = 300)
    {
        CapabilityDecisionProvenance provenance = new("SupportsIis",
            CapabilityCategory.Platform, 1, CapabilityStatus.Supported,
            CapabilityStatus.Supported, CapabilityEvaluationStatus.Succeeded,
            "CapabilitySatisfied", CapabilitySnapshotId, InventoryRunId, 12);
        return new(code, 1, DecisionSubject.ManagedTargetServer, category,
            CollectorDecisionStatus.Eligible, EligibilityStatus.Eligible,
            ExecutionReadinessStatus.Ready, priority, order, true, false,
            CollectorDecisionReasonCodes.StrategyEligible, "Eligible decision.",
            [], [], [provenance], []);
    }

    private sealed class PlanningTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 7, 29, 7, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone =>
            TimeZoneInfo.CreateCustomTimeZone("TRT", TimeSpan.FromHours(3), "TRT", "TRT");
    }
}
