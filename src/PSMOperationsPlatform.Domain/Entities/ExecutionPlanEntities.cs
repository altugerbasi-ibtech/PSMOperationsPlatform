using PSMOperationsPlatform.Domain.Common;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class ExecutionPlan : Entity
{
    public ExecutionPlan(Guid id, Guid managedServerId, Guid decisionPlanId,
        Guid capabilitySnapshotId, Guid sourceInventoryRunId, long sourceInventoryVersion,
        int capabilitySchemaVersion, int decisionSchemaVersion, int executionPlanSchemaVersion,
        DateTime createdAt, string planStatus, int stepCount, int exclusionCount) : base(id)
    {
        ManagedServerId = GuidValue(managedServerId);
        DecisionPlanId = GuidValue(decisionPlanId);
        CapabilitySnapshotId = GuidValue(capabilitySnapshotId);
        SourceInventoryRunId = GuidValue(sourceInventoryRunId);
        SourceInventoryVersion = Positive(sourceInventoryVersion);
        CapabilitySchemaVersion = Positive(capabilitySchemaVersion);
        DecisionSchemaVersion = Positive(decisionSchemaVersion);
        ExecutionPlanSchemaVersion = Positive(executionPlanSchemaVersion);
        CreatedAt = createdAt == default ? throw new ArgumentOutOfRangeException(nameof(createdAt)) : createdAt;
        PlanStatus = Text(planStatus, 30);
        StepCount = NonNegative(stepCount);
        ExclusionCount = NonNegative(exclusionCount);
    }
    private ExecutionPlan() { PlanStatus = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid DecisionPlanId { get; private set; }
    public Guid CapabilitySnapshotId { get; private set; }
    public Guid SourceInventoryRunId { get; private set; }
    public long SourceInventoryVersion { get; private set; }
    public int CapabilitySchemaVersion { get; private set; }
    public int DecisionSchemaVersion { get; private set; }
    public int ExecutionPlanSchemaVersion { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string PlanStatus { get; private set; }
    public int StepCount { get; private set; }
    public int ExclusionCount { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public ICollection<ExecutionPlanStep> Steps { get; private set; } = [];
    public ICollection<ExecutionPlanExclusion> Exclusions { get; private set; } = [];
    private static Guid GuidValue(Guid value) => value == Guid.Empty ? throw new ArgumentException(nameof(value)) : value;
    private static int Positive(int value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static long Positive(long value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static int NonNegative(int value) => value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static string Text(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Length > max ? throw new ArgumentException(nameof(value)) : value;
}

public sealed class ExecutionPlanStep : Entity
{
    public ExecutionPlanStep(Guid id, Guid executionPlanId, Guid logicalStepId,
        string strategyCode, int strategyVersion, string subject, string category,
        int stepSequence, int priority, int executionOrder, string parallelGroupCode,
        string timeoutPolicyCode, int timeoutPolicyVersion, int timeoutSeconds,
        string retryPolicyCode, int retryPolicyVersion, string throttlingClass,
        string? batchGroupCode, bool isReadOnly, bool requiresManualApproval,
        string sourceDecisionStatus, string sourceDecisionReasonCode,
        string inclusionReasonCode, string explanation) : base(id)
    {
        ExecutionPlanId = GuidValue(executionPlanId); LogicalStepId = GuidValue(logicalStepId);
        StrategyCode = Text(strategyCode, 100); StrategyVersion = Positive(strategyVersion);
        Subject = Text(subject, 50); Category = Text(category, 30);
        StepSequence = Positive(stepSequence); Priority = Positive(priority); ExecutionOrder = Positive(executionOrder);
        ParallelGroupCode = Text(parallelGroupCode, 50);
        TimeoutPolicyCode = Text(timeoutPolicyCode, 50); TimeoutPolicyVersion = Positive(timeoutPolicyVersion);
        TimeoutSeconds = Positive(timeoutSeconds);
        RetryPolicyCode = Text(retryPolicyCode, 50); RetryPolicyVersion = Positive(retryPolicyVersion);
        ThrottlingClass = Text(throttlingClass, 30);
        BatchGroupCode = string.IsNullOrWhiteSpace(batchGroupCode) ? null : Text(batchGroupCode, 50);
        IsReadOnly = isReadOnly; RequiresManualApproval = requiresManualApproval;
        SourceDecisionStatus = Text(sourceDecisionStatus, 30);
        SourceDecisionReasonCode = Text(sourceDecisionReasonCode, 100);
        InclusionReasonCode = Text(inclusionReasonCode, 100); Explanation = Text(explanation, 500);
    }
    private ExecutionPlanStep()
    {
        StrategyCode = Subject = Category = ParallelGroupCode = TimeoutPolicyCode =
            RetryPolicyCode = ThrottlingClass = SourceDecisionStatus =
            SourceDecisionReasonCode = InclusionReasonCode = Explanation = null!;
    }
    public Guid ExecutionPlanId { get; private set; }
    public Guid LogicalStepId { get; private set; }
    public string StrategyCode { get; private set; }
    public int StrategyVersion { get; private set; }
    public string Subject { get; private set; }
    public string Category { get; private set; }
    public int StepSequence { get; private set; }
    public int Priority { get; private set; }
    public int ExecutionOrder { get; private set; }
    public string ParallelGroupCode { get; private set; }
    public string TimeoutPolicyCode { get; private set; }
    public int TimeoutPolicyVersion { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public string RetryPolicyCode { get; private set; }
    public int RetryPolicyVersion { get; private set; }
    public string ThrottlingClass { get; private set; }
    public string? BatchGroupCode { get; private set; }
    public bool IsReadOnly { get; private set; }
    public bool RequiresManualApproval { get; private set; }
    public string SourceDecisionStatus { get; private set; }
    public string SourceDecisionReasonCode { get; private set; }
    public string InclusionReasonCode { get; private set; }
    public string Explanation { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    private static Guid GuidValue(Guid value) => value == Guid.Empty ? throw new ArgumentException(nameof(value)) : value;
    private static int Positive(int value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    private static string Text(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Length > max ? throw new ArgumentException(nameof(value)) : value;
}

public sealed class ExecutionPlanExclusion : Entity
{
    public ExecutionPlanExclusion(Guid id, Guid executionPlanId, string strategyCode,
        int strategyVersion, string sourceDecisionStatus, string planningDisposition,
        string reasonCode, string explanation) : base(id)
    {
        ExecutionPlanId = executionPlanId == Guid.Empty ? throw new ArgumentException(nameof(executionPlanId)) : executionPlanId;
        StrategyCode = Text(strategyCode, 100);
        StrategyVersion = strategyVersion > 0 ? strategyVersion : throw new ArgumentOutOfRangeException(nameof(strategyVersion));
        SourceDecisionStatus = Text(sourceDecisionStatus, 30);
        PlanningDisposition = Text(planningDisposition, 40);
        ReasonCode = Text(reasonCode, 100); Explanation = Text(explanation, 500);
    }
    private ExecutionPlanExclusion()
    {
        StrategyCode = SourceDecisionStatus = PlanningDisposition = ReasonCode = Explanation = null!;
    }
    public Guid ExecutionPlanId { get; private set; }
    public string StrategyCode { get; private set; }
    public int StrategyVersion { get; private set; }
    public string SourceDecisionStatus { get; private set; }
    public string PlanningDisposition { get; private set; }
    public string ReasonCode { get; private set; }
    public string Explanation { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public ICollection<ExecutionPlanExclusionCapability> Capabilities { get; private set; } = [];
    private static string Text(string value, int max) => string.IsNullOrWhiteSpace(value) || value.Length > max ? throw new ArgumentException(nameof(value)) : value;
}

public sealed class ExecutionPlanExclusionCapability : Entity
{
    public ExecutionPlanExclusionCapability(Guid id, Guid exclusionId, string capabilityCode,
        string classification, int capabilityRuleVersion, Guid capabilitySnapshotId,
        Guid sourceInventoryRunId, long sourceInventoryVersion) : base(id)
    {
        ExclusionId = exclusionId;
        CapabilityCode = capabilityCode;
        Classification = classification;
        CapabilityRuleVersion = capabilityRuleVersion;
        CapabilitySnapshotId = capabilitySnapshotId;
        SourceInventoryRunId = sourceInventoryRunId;
        SourceInventoryVersion = sourceInventoryVersion;
    }
    private ExecutionPlanExclusionCapability() { CapabilityCode = Classification = null!; }
    public Guid ExclusionId { get; private set; }
    public string CapabilityCode { get; private set; }
    public string Classification { get; private set; }
    public int CapabilityRuleVersion { get; private set; }
    public Guid CapabilitySnapshotId { get; private set; }
    public Guid SourceInventoryRunId { get; private set; }
    public long SourceInventoryVersion { get; private set; }
}
