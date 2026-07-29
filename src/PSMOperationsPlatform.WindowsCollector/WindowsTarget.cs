using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.WindowsCollector;

internal sealed record WindowsTarget(
    Guid TargetId,
    string HostName,
    WinRmTransportMode TransportMode,
    int HttpsPort,
    int HttpPort,
    TimeSpan ProbeTimeout,
    byte[]? RowVersion = null,
    bool IsInventoryDue = true);
