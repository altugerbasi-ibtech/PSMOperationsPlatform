using System.Globalization;
using PSMOperationsPlatform.Application.Capabilities;
using PSMOperationsPlatform.Application.Decisions;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class CollectorDecisionEngineTests
{
    [Fact]
    public void Evaluate_ProducesAllReadOnlyStrategiesInDeterministicOrder()
    {
        CollectorDecisionPlan first = Engine().Evaluate(Input(ReadyEntries().Reverse().ToArray()));
        CollectorDecisionPlan second = Engine().Evaluate(Input(ReadyEntries()));

        Assert.Equal(8, first.StrategyCount);
        Assert.Equal(first.Strategies.Select(x => x.StrategyCode),
            second.Strategies.Select(x => x.StrategyCode));
        Assert.All(first.Strategies, x =>
        {
            Assert.True(x.IsReadOnly);
            Assert.False(x.RequiresManualApproval);
            Assert.Equal(1, x.StrategyVersion);
            Assert.NotEmpty(x.ReasonCode);
            Assert.NotEmpty(x.Explanation);
        });
        Assert.Equal(CollectorDecisionEngine.SchemaVersion, first.DecisionSchemaVersion);
    }

    [Fact]
    public void Evaluate_ReadinessUnknown_IsIndeterminateButPlatformEligible()
    {
        CapabilityEntry[] entries = ReadyEntries();
        entries[Array.FindIndex(entries, x => x.CapabilityCode == CapabilityCodes.CanCollectIisLogs)] =
            Entry(CapabilityCodes.CanCollectIisLogs, CapabilityCategory.Collection,
                CapabilityStatus.Supported, CapabilityStatus.Unknown);

        CollectorStrategyDecision decision = Engine().Evaluate(Input(entries)).Strategies
            .Single(x => x.StrategyCode == CollectorStrategyCodes.IisLogCollection);

        Assert.Equal(EligibilityStatus.Eligible, decision.EligibilityStatus);
        Assert.Equal(ExecutionReadinessStatus.Unknown, decision.ExecutionReadinessStatus);
        Assert.Equal(CollectorDecisionStatus.Indeterminate, decision.DecisionStatus);
        Assert.Contains(CapabilityCodes.CanCollectIisLogs, decision.UnknownCapabilities);
    }

    [Fact]
    public void Evaluate_MissingHostingBundle_IsBlockedAndExplained()
    {
        CapabilityEntry[] entries = ReadyEntries();
        entries[Array.FindIndex(entries, x => x.CapabilityCode == CapabilityCodes.HasAspNetCoreHostingBundle)] =
            Entry(CapabilityCodes.HasAspNetCoreHostingBundle, CapabilityCategory.Platform,
                CapabilityStatus.NotSupported, CapabilityStatus.NotSupported);
        entries[Array.FindIndex(entries, x => x.CapabilityCode == CapabilityCodes.CanCollectAspNetCoreIisLogs)] =
            Entry(CapabilityCodes.CanCollectAspNetCoreIisLogs, CapabilityCategory.Collection,
                CapabilityStatus.NotSupported, CapabilityStatus.NotSupported);

        CollectorStrategyDecision decision = Engine().Evaluate(Input(entries)).Strategies
            .Single(x => x.StrategyCode == CollectorStrategyCodes.AspNetCoreIisLog);

        Assert.Equal(CollectorDecisionStatus.Blocked, decision.DecisionStatus);
        Assert.Equal(CollectorDecisionReasonCodes.HostingBundleMissing, decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_IisAbsent_IsNotApplicable()
    {
        CapabilityEntry[] entries = ReadyEntries();
        entries[Array.FindIndex(entries, x => x.CapabilityCode == CapabilityCodes.SupportsIis)] =
            Entry(CapabilityCodes.SupportsIis, CapabilityCategory.Platform,
                CapabilityStatus.NotSupported, CapabilityStatus.NotSupported);

        CollectorStrategyDecision decision = Engine().Evaluate(Input(entries)).Strategies
            .Single(x => x.StrategyCode == CollectorStrategyCodes.IisLogCollection);

        Assert.Equal(CollectorDecisionStatus.NotApplicable, decision.DecisionStatus);
    }

    [Fact]
    public void Evaluate_IsCultureAndRegistrationOrderIndependent()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            string[] expected = Engine().Evaluate(Input(ReadyEntries())).Strategies.Select(x => x.StrategyCode).ToArray();
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            string[] actual = Engine().Evaluate(Input(ReadyEntries().Reverse().ToArray())).Strategies.Select(x => x.StrategyCode).ToArray();
            Assert.Equal(expected, actual);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Evaluate_RejectsDuplicateCapabilitiesAndInvalidSource()
    {
        CapabilityEntry duplicate = ReadyEntries()[0];
        Assert.Throws<ArgumentException>(() => Engine().Evaluate(Input([.. ReadyEntries(), duplicate])));
        Assert.Throws<ArgumentException>(() => Engine().Evaluate(Input(ReadyEntries()) with { SourceInventoryVersion = 0 }));
    }

    [Fact]
    public void Evaluate_ProvenanceRetainsVersionsAndSourceIdentity()
    {
        CollectorStrategyDecision decision = Engine().Evaluate(Input(ReadyEntries())).Strategies[0];
        Assert.All(decision.Provenance, item =>
        {
            Assert.Equal(1, item.CapabilityRuleVersion);
            Assert.Equal(SourceRunId, item.SourceInventoryRunId);
            Assert.Equal(7, item.SourceInventoryVersion);
            Assert.Equal(SnapshotId, item.CapabilitySnapshotId);
        });
    }

    private static readonly Guid ServerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SnapshotId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid SourceRunId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static CollectorDecisionEngine Engine() => new();
    private static CollectorDecisionInput Input(IReadOnlyList<CapabilityEntry> entries) =>
        new(ServerId, "server.example.test", SnapshotId, 1, SourceRunId, 7,
            CapabilitySubject.ManagedTargetServer, CapabilityEvaluationStatus.Succeeded,
            new DateTime(2026, 7, 28, 12, 0, 0), entries);
    private static CapabilityEntry[] ReadyEntries() =>
    [
        Entry(CapabilityCodes.SupportsWindowsPowerShell51, CapabilityCategory.Platform),
        Entry(CapabilityCodes.CanRunWindowsPowerShell51Collection, CapabilityCategory.Collection),
        Entry(CapabilityCodes.SupportsIis, CapabilityCategory.Platform),
        Entry(CapabilityCodes.CanCollectIisPlatformInventory, CapabilityCategory.Collection),
        Entry(CapabilityCodes.CanCollectIisLogs, CapabilityCategory.Collection),
        Entry(CapabilityCodes.CanCollectFailedRequestTracingLogs, CapabilityCategory.Collection),
        Entry(CapabilityCodes.SupportsAspNetFramework, CapabilityCategory.Platform),
        Entry(CapabilityCodes.CanCollectAspNetFrameworkLogs, CapabilityCategory.Collection),
        Entry(CapabilityCodes.SupportsAspNetCore, CapabilityCategory.Platform),
        Entry(CapabilityCodes.HasAspNetCoreHostingBundle, CapabilityCategory.Platform),
        Entry(CapabilityCodes.CanCollectAspNetCoreIisLogs, CapabilityCategory.Collection),
        Entry(CapabilityCodes.SupportsDotNetRuntime, CapabilityCategory.Platform),
        Entry(CapabilityCodes.SupportsDotNet10, CapabilityCategory.Platform),
        Entry(CapabilityCodes.SupportsPowerShell7, CapabilityCategory.Diagnostics),
    ];
    private static CapabilityEntry Entry(string code, CapabilityCategory category,
        CapabilityStatus support = CapabilityStatus.Supported,
        CapabilityStatus readiness = CapabilityStatus.Supported) =>
        new(code, CapabilitySubject.ManagedTargetServer, category, support, readiness, 1,
            "CapabilitySatisfied", "Safe deterministic explanation.", [], [], []);
}
