using PSMOperationsPlatform.Domain.Common;
using System.Net;
using System.Net.Sockets;

namespace PSMOperationsPlatform.Domain.Entities;

internal static class InventoryEntityGuard
{
    internal static Guid Id(Guid value, string name) =>
        value == Guid.Empty
            ? throw new ArgumentException("Identifier cannot be empty.", name)
            : value;

    internal static string Required(string value, int maximumLength, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        string normalized = value.Trim();
        return normalized.Length > maximumLength
            ? throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                name)
            : normalized;
    }

    internal static string? Optional(string? value, int maximumLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length > maximumLength
            ? throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                name)
            : normalized;
    }

    internal static long? NonNegative(long? value, string name) =>
        value < 0
            ? throw new ArgumentOutOfRangeException(name)
            : value;

    internal static int? Positive(int? value, string name) =>
        value <= 0
            ? throw new ArgumentOutOfRangeException(name)
            : value;

    internal static DateTime CapturedAt(DateTime value, string name) =>
        value.Kind == DateTimeKind.Utc
            ? throw new ArgumentException(
                "Inventory capture time must use repository local-time semantics.",
                name)
            : value;
}

public sealed class WindowsComputerInventory : Entity
{
    public WindowsComputerInventory(
        Guid managedServerId,
        DateTime capturedAt,
        string? computerName = null,
        string? fqdn = null,
        string? domainName = null,
        string? manufacturer = null,
        string? model = null,
        string? serialNumber = null)
        : base(managedServerId) =>
        Apply(computerName, fqdn, domainName, manufacturer, model, serialNumber, capturedAt);

    private WindowsComputerInventory() { }

    public Guid ManagedServerId => Id;
    public string? ComputerName { get; private set; }
    public string? Fqdn { get; private set; }
    public string? DomainName { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public string? SerialNumber { get; private set; }
    public DateTime CapturedAt { get; private set; }

    public void Apply(
        string? computerName,
        string? fqdn,
        string? domainName,
        string? manufacturer,
        string? model,
        string? serialNumber,
        DateTime capturedAt)
    {
        ComputerName = InventoryEntityGuard.Optional(computerName, 255, nameof(computerName));
        Fqdn = InventoryEntityGuard.Optional(fqdn, 255, nameof(fqdn));
        DomainName = InventoryEntityGuard.Optional(domainName, 255, nameof(domainName));
        Manufacturer = InventoryEntityGuard.Optional(manufacturer, 255, nameof(manufacturer));
        Model = InventoryEntityGuard.Optional(model, 255, nameof(model));
        SerialNumber = InventoryEntityGuard.Optional(serialNumber, 255, nameof(serialNumber));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }
}

public sealed class WindowsOperatingSystemInventory : Entity
{
    public WindowsOperatingSystemInventory(
        Guid managedServerId,
        string caption,
        string version,
        string buildNumber,
        string architecture,
        DateTime capturedAt,
        string? edition = null,
        DateTime? installDate = null,
        DateTime? lastBootTime = null,
        string? timeZoneId = null)
        : base(managedServerId) =>
        Apply(caption, version, buildNumber, architecture, capturedAt, edition, installDate, lastBootTime, timeZoneId);

    private WindowsOperatingSystemInventory()
    {
        Caption = Version = BuildNumber = Architecture = null!;
    }

    public Guid ManagedServerId => Id;
    public string Caption { get; private set; } = null!;
    public string Version { get; private set; } = null!;
    public string BuildNumber { get; private set; } = null!;
    public string? Edition { get; private set; }
    public string Architecture { get; private set; } = null!;
    public DateTime? InstallDate { get; private set; }
    public DateTime? LastBootTime { get; private set; }
    public string? TimeZoneId { get; private set; }
    public DateTime CapturedAt { get; private set; }

    public void Apply(
        string caption,
        string version,
        string buildNumber,
        string architecture,
        DateTime capturedAt,
        string? edition = null,
        DateTime? installDate = null,
        DateTime? lastBootTime = null,
        string? timeZoneId = null)
    {
        Caption = InventoryEntityGuard.Required(caption, 255, nameof(caption));
        Version = InventoryEntityGuard.Required(version, 100, nameof(version));
        BuildNumber = InventoryEntityGuard.Required(buildNumber, 50, nameof(buildNumber));
        Architecture = InventoryEntityGuard.Required(architecture, 50, nameof(architecture));
        Edition = InventoryEntityGuard.Optional(edition, 100, nameof(edition));
        InstallDate = installDate.HasValue
            ? InventoryEntityGuard.CapturedAt(installDate.Value, nameof(installDate))
            : null;
        LastBootTime = lastBootTime.HasValue
            ? InventoryEntityGuard.CapturedAt(lastBootTime.Value, nameof(lastBootTime))
            : null;
        TimeZoneId = InventoryEntityGuard.Optional(timeZoneId, 100, nameof(timeZoneId));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }
}

public sealed class WindowsMemoryInventory : Entity
{
    public WindowsMemoryInventory(Guid managedServerId, long totalPhysicalMemoryBytes, DateTime capturedAt)
        : base(managedServerId) =>
        Apply(totalPhysicalMemoryBytes, capturedAt);

    private WindowsMemoryInventory() { }

    public Guid ManagedServerId => Id;
    public long TotalPhysicalMemoryBytes { get; private set; }
    public DateTime CapturedAt { get; private set; }

    public void Apply(long totalPhysicalMemoryBytes, DateTime capturedAt)
    {
        TotalPhysicalMemoryBytes =
            InventoryEntityGuard.NonNegative(totalPhysicalMemoryBytes, nameof(totalPhysicalMemoryBytes))!.Value;
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }
}

public sealed class WindowsProcessorInventory : Entity
{
    public WindowsProcessorInventory(
        Guid id, Guid managedServerId, string stableSourceKey, DateTime capturedAt,
        string? name = null, string? manufacturer = null, int? coreCount = null,
        int? logicalProcessorCount = null, int? maxClockSpeedMhz = null)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        StableSourceKey = InventoryEntityGuard.Required(stableSourceKey, 200, nameof(stableSourceKey));
        Name = InventoryEntityGuard.Optional(name, 255, nameof(name));
        Manufacturer = InventoryEntityGuard.Optional(manufacturer, 255, nameof(manufacturer));
        CoreCount = InventoryEntityGuard.Positive(coreCount, nameof(coreCount));
        LogicalProcessorCount = InventoryEntityGuard.Positive(logicalProcessorCount, nameof(logicalProcessorCount));
        MaxClockSpeedMhz = InventoryEntityGuard.Positive(maxClockSpeedMhz, nameof(maxClockSpeedMhz));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsProcessorInventory() { StableSourceKey = null!; }
    public Guid ManagedServerId { get; private set; }
    public string StableSourceKey { get; private set; }
    public string? Name { get; private set; }
    public string? Manufacturer { get; private set; }
    public int? CoreCount { get; private set; }
    public int? LogicalProcessorCount { get; private set; }
    public int? MaxClockSpeedMhz { get; private set; }
    public DateTime CapturedAt { get; private set; }
}

public sealed class WindowsDiskInventory : Entity
{
    public WindowsDiskInventory(
        Guid id, Guid managedServerId, string stableSourceKey, DateTime capturedAt,
        int? diskNumber = null, string? friendlyName = null, string? serialNumber = null,
        long? sizeBytes = null, string? busType = null, string? partitionStyle = null)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        StableSourceKey = InventoryEntityGuard.Required(stableSourceKey, 260, nameof(stableSourceKey));
        DiskNumber = diskNumber < 0 ? throw new ArgumentOutOfRangeException(nameof(diskNumber)) : diskNumber;
        FriendlyName = InventoryEntityGuard.Optional(friendlyName, 255, nameof(friendlyName));
        SerialNumber = InventoryEntityGuard.Optional(serialNumber, 255, nameof(serialNumber));
        SizeBytes = InventoryEntityGuard.NonNegative(sizeBytes, nameof(sizeBytes));
        BusType = InventoryEntityGuard.Optional(busType, 100, nameof(busType));
        PartitionStyle = InventoryEntityGuard.Optional(partitionStyle, 50, nameof(partitionStyle));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsDiskInventory() { StableSourceKey = null!; }
    public Guid ManagedServerId { get; private set; }
    public string StableSourceKey { get; private set; }
    public int? DiskNumber { get; private set; }
    public string? FriendlyName { get; private set; }
    public string? SerialNumber { get; private set; }
    public long? SizeBytes { get; private set; }
    public string? BusType { get; private set; }
    public string? PartitionStyle { get; private set; }
    public DateTime CapturedAt { get; private set; }
}

public sealed class WindowsVolumeInventory : Entity
{
    public WindowsVolumeInventory(
        Guid id, Guid managedServerId, string stableSourceKey, DateTime capturedAt,
        string? driveLetter = null, string? label = null, string? fileSystem = null,
        long? sizeBytes = null, long? freeSpaceBytes = null)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        StableSourceKey = InventoryEntityGuard.Required(stableSourceKey, 260, nameof(stableSourceKey));
        DriveLetter = InventoryEntityGuard.Optional(driveLetter, 10, nameof(driveLetter));
        Label = InventoryEntityGuard.Optional(label, 255, nameof(label));
        FileSystem = InventoryEntityGuard.Optional(fileSystem, 50, nameof(fileSystem));
        SizeBytes = InventoryEntityGuard.NonNegative(sizeBytes, nameof(sizeBytes));
        FreeSpaceBytes = InventoryEntityGuard.NonNegative(freeSpaceBytes, nameof(freeSpaceBytes));
        if (SizeBytes.HasValue && FreeSpaceBytes > SizeBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(freeSpaceBytes));
        }
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsVolumeInventory() { StableSourceKey = null!; }
    public Guid ManagedServerId { get; private set; }
    public string StableSourceKey { get; private set; }
    public string? DriveLetter { get; private set; }
    public string? Label { get; private set; }
    public string? FileSystem { get; private set; }
    public long? SizeBytes { get; private set; }
    public long? FreeSpaceBytes { get; private set; }
    public DateTime CapturedAt { get; private set; }
}

public sealed class WindowsNetworkAdapterInventory : Entity
{
    public WindowsNetworkAdapterInventory(
        Guid id, Guid managedServerId, string stableSourceKey, DateTime capturedAt,
        string? name = null, string? interfaceDescription = null, string? macAddress = null,
        string? operationalStatus = null, long? linkSpeedBitsPerSecond = null)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        StableSourceKey = InventoryEntityGuard.Required(stableSourceKey, 200, nameof(stableSourceKey));
        Name = InventoryEntityGuard.Optional(name, 255, nameof(name));
        InterfaceDescription = InventoryEntityGuard.Optional(interfaceDescription, 500, nameof(interfaceDescription));
        MacAddress = InventoryEntityGuard.Optional(macAddress, 20, nameof(macAddress));
        OperationalStatus = InventoryEntityGuard.Optional(operationalStatus, 50, nameof(operationalStatus));
        LinkSpeedBitsPerSecond = InventoryEntityGuard.NonNegative(linkSpeedBitsPerSecond, nameof(linkSpeedBitsPerSecond));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsNetworkAdapterInventory() { StableSourceKey = null!; }
    public Guid ManagedServerId { get; private set; }
    public string StableSourceKey { get; private set; }
    public string? Name { get; private set; }
    public string? InterfaceDescription { get; private set; }
    public string? MacAddress { get; private set; }
    public string? OperationalStatus { get; private set; }
    public long? LinkSpeedBitsPerSecond { get; private set; }
    public DateTime CapturedAt { get; private set; }
}

public sealed class WindowsIpv4AddressInventory : Entity
{
    public WindowsIpv4AddressInventory(
        Guid id, Guid managedServerId, Guid networkAdapterInventoryId,
        string stableSourceKey, string address, int prefixLength, bool? isDhcp,
        DateTime capturedAt)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        NetworkAdapterInventoryId =
            InventoryEntityGuard.Id(networkAdapterInventoryId, nameof(networkAdapterInventoryId));
        StableSourceKey = InventoryEntityGuard.Required(stableSourceKey, 300, nameof(stableSourceKey));
        string normalizedAddress =
            InventoryEntityGuard.Required(address, 15, nameof(address));
        if (!IPAddress.TryParse(normalizedAddress, out IPAddress? parsedAddress)
            || parsedAddress.AddressFamily != AddressFamily.InterNetwork
            || !string.Equals(
                normalizedAddress,
                parsedAddress.ToString(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Address must be canonical IPv4.",
                nameof(address));
        }
        Address = normalizedAddress;
        if (prefixLength is < 0 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(prefixLength));
        }
        PrefixLength = prefixLength;
        IsDhcp = isDhcp;
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsIpv4AddressInventory() { StableSourceKey = Address = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid NetworkAdapterInventoryId { get; private set; }
    public string StableSourceKey { get; private set; }
    public string Address { get; private set; }
    public int PrefixLength { get; private set; }
    public bool? IsDhcp { get; private set; }
    public DateTime CapturedAt { get; private set; }
}
