using PSMOperationsPlatform.Domain.Common;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class CollectorDecisionPlan : Entity
{
    public CollectorDecisionPlan(Guid id, Guid managedServerId, Guid capabilitySnapshotId,
        Guid sourceInventoryRunId, long sourceInventoryVersion, int capabilitySchemaVersion,
        int decisionSchemaVersion, DateTime evaluatedAt, string overallStatus,
        int strategyCount, int eligibleCount, int blockedCount, int indeterminateCount,
        int notApplicableCount, int disabledCount, int invalidCount) : base(id)
    {
        ManagedServerId = Required(managedServerId);
        CapabilitySnapshotId = Required(capabilitySnapshotId);
        SourceInventoryRunId = Required(sourceInventoryRunId);
        SourceInventoryVersion = Positive(sourceInventoryVersion);
        CapabilitySchemaVersion = Positive(capabilitySchemaVersion);
        DecisionSchemaVersion = Positive(decisionSchemaVersion);
        EvaluatedAt = evaluatedAt == default ? throw new ArgumentOutOfRangeException(nameof(evaluatedAt)) : evaluatedAt;
        OverallStatus = Text(overallStatus, 30);
        StrategyCount = NonNegative(strategyCount); EligibleCount = NonNegative(eligibleCount);
        BlockedCount = NonNegative(blockedCount); IndeterminateCount = NonNegative(indeterminateCount);
        NotApplicableCount = NonNegative(notApplicableCount); DisabledCount = NonNegative(disabledCount);
        InvalidCount = NonNegative(invalidCount);
    }
    private CollectorDecisionPlan() { OverallStatus = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid CapabilitySnapshotId { get; private set; }
    public Guid SourceInventoryRunId { get; private set; }
    public long SourceInventoryVersion { get; private set; }
    public int CapabilitySchemaVersion { get; private set; }
    public int DecisionSchemaVersion { get; private set; }
    public DateTime EvaluatedAt { get; private set; }
    public string OverallStatus { get; private set; }
    public int StrategyCount { get; private set; }
    public int EligibleCount { get; private set; }
    public int BlockedCount { get; private set; }
    public int IndeterminateCount { get; private set; }
    public int NotApplicableCount { get; private set; }
    public int DisabledCount { get; private set; }
    public int InvalidCount { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public ICollection<CollectorStrategyDecision> Strategies { get; private set; } = [];
    private static Guid Required(Guid value) => value == Guid.Empty ? throw new ArgumentException(nameof(value)) : value;
    private static int Positive(int value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static long Positive(long value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static int NonNegative(int value) => value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static string Text(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Length > max ? throw new ArgumentException(nameof(value)) : value;
}

public sealed class CollectorStrategyDecision : Entity
{
    public CollectorStrategyDecision(Guid id, Guid planId, string strategyCode, int strategyVersion,
        string subject, string category, string eligibilityStatus, string executionReadinessStatus,
        string decisionStatus, int priority, int executionOrder, bool isReadOnly,
        bool requiresManualApproval, string reasonCode, string explanation) : base(id)
    {
        PlanId = planId == Guid.Empty ? throw new ArgumentException(nameof(planId)) : planId;
        StrategyCode = Text(strategyCode, 100); StrategyVersion = Positive(strategyVersion);
        Subject = Text(subject, 50); Category = Text(category, 30);
        EligibilityStatus = Text(eligibilityStatus, 30); ExecutionReadinessStatus = Text(executionReadinessStatus, 30);
        DecisionStatus = Text(decisionStatus, 30); Priority = Positive(priority); ExecutionOrder = Positive(executionOrder);
        IsReadOnly = isReadOnly; RequiresManualApproval = requiresManualApproval;
        ReasonCode = Text(reasonCode, 100); Explanation = Text(explanation, 500);
    }
    private CollectorStrategyDecision()
    {
        StrategyCode = Subject = Category = EligibilityStatus = ExecutionReadinessStatus =
            DecisionStatus = ReasonCode = Explanation = null!;
    }
    public Guid PlanId { get; private set; }
    public string StrategyCode { get; private set; }
    public int StrategyVersion { get; private set; }
    public string Subject { get; private set; }
    public string Category { get; private set; }
    public string EligibilityStatus { get; private set; }
    public string ExecutionReadinessStatus { get; private set; }
    public string DecisionStatus { get; private set; }
    public int Priority { get; private set; }
    public int ExecutionOrder { get; private set; }
    public bool IsReadOnly { get; private set; }
    public bool RequiresManualApproval { get; private set; }
    public string ReasonCode { get; private set; }
    public string Explanation { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public ICollection<CollectorDecisionCapabilityReference> CapabilityReferences { get; private set; } = [];
    private static int Positive(int value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static string Text(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Length > max ? throw new ArgumentException(nameof(value)) : value;
}

public sealed class CollectorDecisionCapabilityReference : Entity
{
    public CollectorDecisionCapabilityReference(Guid id, Guid strategyDecisionId,
        string capabilityCode, string capabilityCategory, string prerequisiteStatus, int capabilityRuleVersion,
        string supportStatus, string readinessStatus, string evaluationStatus, string reasonCode,
        Guid capabilitySnapshotId, Guid sourceInventoryRunId, long sourceInventoryVersion) : base(id)
    {
        StrategyDecisionId = strategyDecisionId;
        CapabilityCode = capabilityCode; CapabilityCategory = capabilityCategory;
        PrerequisiteStatus = prerequisiteStatus;
        CapabilityRuleVersion = capabilityRuleVersion; SupportStatus = supportStatus;
        ReadinessStatus = readinessStatus; EvaluationStatus = evaluationStatus; ReasonCode = reasonCode;
        CapabilitySnapshotId = capabilitySnapshotId; SourceInventoryRunId = sourceInventoryRunId;
        SourceInventoryVersion = sourceInventoryVersion;
    }
    private CollectorDecisionCapabilityReference()
    {
        CapabilityCode = CapabilityCategory = PrerequisiteStatus = SupportStatus = ReadinessStatus = EvaluationStatus = ReasonCode = null!;
    }
    public Guid StrategyDecisionId { get; private set; }
    public string CapabilityCode { get; private set; }
    public string CapabilityCategory { get; private set; }
    public string PrerequisiteStatus { get; private set; }
    public int CapabilityRuleVersion { get; private set; }
    public string SupportStatus { get; private set; }
    public string ReadinessStatus { get; private set; }
    public string EvaluationStatus { get; private set; }
    public string ReasonCode { get; private set; }
    public Guid CapabilitySnapshotId { get; private set; }
    public Guid SourceInventoryRunId { get; private set; }
    public long SourceInventoryVersion { get; private set; }
}
