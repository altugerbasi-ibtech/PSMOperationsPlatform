using PSMOperationsPlatform.Domain.Common;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class WindowsCapabilitySnapshot : Entity
{
    public WindowsCapabilitySnapshot(Guid id, Guid managedServerId, Guid inventoryRunId,
        long sourceInventoryVersion, int capabilitySchemaVersion, DateTime evaluatedAt,
        string evaluationStatus) : base(id)
    {
        if (managedServerId == Guid.Empty || inventoryRunId == Guid.Empty) throw new ArgumentException("Identifiers are required.");
        if (sourceInventoryVersion < 1 || capabilitySchemaVersion < 1) throw new ArgumentOutOfRangeException(nameof(sourceInventoryVersion));
        ManagedServerId = managedServerId;
        InventoryRunId = inventoryRunId;
        SourceInventoryVersion = sourceInventoryVersion;
        CapabilitySchemaVersion = capabilitySchemaVersion;
        EvaluatedAt = evaluatedAt == default ? throw new ArgumentOutOfRangeException(nameof(evaluatedAt)) : evaluatedAt;
        EvaluationStatus = string.IsNullOrWhiteSpace(evaluationStatus) ? throw new ArgumentException(nameof(evaluationStatus)) : evaluationStatus;
    }
    private WindowsCapabilitySnapshot() { EvaluationStatus = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public long SourceInventoryVersion { get; private set; }
    public int CapabilitySchemaVersion { get; private set; }
    public DateTime EvaluatedAt { get; private set; }
    public string EvaluationStatus { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public ICollection<WindowsCapabilityEntry> Entries { get; private set; } = [];
}

public sealed class WindowsCapabilityEntry : Entity
{
    public WindowsCapabilityEntry(Guid id, Guid snapshotId, string capabilityCode,
        string subject, string category, string supportStatus, string readinessStatus, int ruleVersion,
        string reasonCode, string reason) : base(id)
    {
        SnapshotId = snapshotId == Guid.Empty ? throw new ArgumentException(nameof(snapshotId)) : snapshotId;
        CapabilityCode = Required(capabilityCode, 100);
        Subject = Required(subject, 50);
        Category = Required(category, 30);
        SupportStatus = Required(supportStatus, 30);
        ReadinessStatus = Required(readinessStatus, 30);
        RuleVersion = ruleVersion > 0 ? ruleVersion : throw new ArgumentOutOfRangeException(nameof(ruleVersion));
        ReasonCode = Required(reasonCode, 100);
        Reason = Required(reason, 500);
    }
    private WindowsCapabilityEntry()
    {
        CapabilityCode = Subject = Category = SupportStatus = ReadinessStatus =
            ReasonCode = Reason = null!;
    }
    public Guid SnapshotId { get; private set; }
    public string CapabilityCode { get; private set; }
    public string Subject { get; private set; }
    public string Category { get; private set; }
    public string SupportStatus { get; private set; }
    public string ReadinessStatus { get; private set; }
    public int RuleVersion { get; private set; }
    public string ReasonCode { get; private set; }
    public string Reason { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
    public ICollection<WindowsCapabilityProvenance> Provenance { get; private set; } = [];
    private static string Required(string value, int max) =>
        string.IsNullOrWhiteSpace(value) || value.Length > max ? throw new ArgumentException(nameof(value)) : value.Trim();
}

public sealed class WindowsCapabilityProvenance : Entity
{
    public WindowsCapabilityProvenance(Guid id, Guid capabilityEntryId, string moduleName,
        string factCategory, string factKey, Guid inventoryRunId, long inventoryVersion)
        : base(id)
    {
        CapabilityEntryId = capabilityEntryId == Guid.Empty ? throw new ArgumentException(nameof(capabilityEntryId)) : capabilityEntryId;
        ModuleName = Required(moduleName);
        FactCategory = Required(factCategory);
        FactKey = Required(factKey);
        InventoryRunId = inventoryRunId == Guid.Empty ? throw new ArgumentException(nameof(inventoryRunId)) : inventoryRunId;
        InventoryVersion = inventoryVersion > 0 ? inventoryVersion : throw new ArgumentOutOfRangeException(nameof(inventoryVersion));
    }
    private WindowsCapabilityProvenance() { ModuleName = FactCategory = FactKey = null!; }
    public Guid CapabilityEntryId { get; private set; }
    public string ModuleName { get; private set; }
    public string FactCategory { get; private set; }
    public string FactKey { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public long InventoryVersion { get; private set; }
    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 100 ? throw new ArgumentException(nameof(value)) : value.Trim();
}
