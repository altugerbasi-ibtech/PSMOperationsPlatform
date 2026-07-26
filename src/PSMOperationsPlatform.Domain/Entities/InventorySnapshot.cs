using System.Text.Json;
using PSMOperationsPlatform.Domain.Common;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class InventorySnapshot : Entity
{
    public InventorySnapshot(
        Guid id,
        Guid collectorRunId,
        Guid managedServerId,
        string snapshotType,
        int schemaVersion,
        DateTime capturedAt,
        string payloadJson,
        string? payloadHash = null)
        : base(id)
    {
        CollectorRunId = RequiredId(collectorRunId, nameof(collectorRunId));
        ManagedServerId = RequiredId(managedServerId, nameof(managedServerId));
        SnapshotType = Required(snapshotType, nameof(snapshotType));
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }

        SchemaVersion = schemaVersion;
        CapturedAt = capturedAt;
        PayloadJson = ValidJson(payloadJson, nameof(payloadJson));
        PayloadHash = Optional(payloadHash);
    }

    private InventorySnapshot()
    {
        SnapshotType = null!;
        PayloadJson = null!;
    }

    public Guid CollectorRunId { get; private set; }

    public Guid ManagedServerId { get; private set; }

    public string SnapshotType { get; private set; }

    public int SchemaVersion { get; private set; }

    public DateTime CapturedAt { get; private set; }

    public string PayloadJson { get; private set; }

    public string? PayloadHash { get; private set; }

    internal static string ValidJson(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        try
        {
            using JsonDocument _ = JsonDocument.Parse(value);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Value must contain valid JSON.", parameterName, exception);
        }

        return value;
    }

    private static Guid RequiredId(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException("Identifier cannot be empty.", parameterName)
            : value;

    private static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
