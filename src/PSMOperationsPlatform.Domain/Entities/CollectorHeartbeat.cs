using PSMOperationsPlatform.Domain.Common;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class CollectorHeartbeat : Entity
{
    public CollectorHeartbeat(
        Guid id,
        Guid collectorNodeId,
        DateTime observedAt,
        CollectorHealthStatus status,
        string? message = null,
        int? processId = null,
        long? workingSetBytes = null)
        : base(id)
    {
        if (collectorNodeId == Guid.Empty)
        {
            throw new ArgumentException("Collector identifier cannot be empty.", nameof(collectorNodeId));
        }

        if (processId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        if (workingSetBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workingSetBytes));
        }

        CollectorNodeId = collectorNodeId;
        ObservedAt = observedAt;
        Status = EnumGuard.Defined(status, nameof(status));
        Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        ProcessId = processId;
        WorkingSetBytes = workingSetBytes;
    }

    private CollectorHeartbeat()
    {
    }

    public Guid CollectorNodeId { get; private set; }

    public DateTime ObservedAt { get; private set; }

    public CollectorHealthStatus Status { get; private set; }

    public string? Message { get; private set; }

    public int? ProcessId { get; private set; }

    public long? WorkingSetBytes { get; private set; }
}
