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
        string? serialNumber = null,
        int? domainRole = null,
        string? systemType = null,
        bool? isVirtualMachine = null,
        Guid? uuid = null)
        : base(managedServerId) =>
        Apply(
            computerName, fqdn, domainName, manufacturer, model, serialNumber,
            capturedAt, domainRole, systemType, isVirtualMachine, uuid);

    private WindowsComputerInventory() { }

    public Guid ManagedServerId => Id;
    public Guid InventoryRunId { get; private set; }
    public string? ComputerName { get; private set; }
    public string? Fqdn { get; private set; }
    public string? DomainName { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Model { get; private set; }
    public string? SerialNumber { get; private set; }
    public int? DomainRole { get; private set; }
    public string? SystemType { get; private set; }
    public bool? IsVirtualMachine { get; private set; }
    public Guid? Uuid { get; private set; }
    public DateTime CapturedAt { get; private set; }

    public void Apply(
        string? computerName,
        string? fqdn,
        string? domainName,
        string? manufacturer,
        string? model,
        string? serialNumber,
        DateTime capturedAt,
        int? domainRole = null,
        string? systemType = null,
        bool? isVirtualMachine = null,
        Guid? uuid = null)
    {
        ComputerName = InventoryEntityGuard.Optional(computerName, 255, nameof(computerName));
        Fqdn = InventoryEntityGuard.Optional(fqdn, 255, nameof(fqdn));
        DomainName = InventoryEntityGuard.Optional(domainName, 255, nameof(domainName));
        Manufacturer = InventoryEntityGuard.Optional(manufacturer, 255, nameof(manufacturer));
        Model = InventoryEntityGuard.Optional(model, 255, nameof(model));
        SerialNumber = InventoryEntityGuard.Optional(serialNumber, 255, nameof(serialNumber));
        DomainRole = domainRole is < 0 or > 5
            ? throw new ArgumentOutOfRangeException(nameof(domainRole))
            : domainRole;
        SystemType = InventoryEntityGuard.Optional(systemType, 100, nameof(systemType));
        IsVirtualMachine = isVirtualMachine;
        Uuid = uuid == Guid.Empty ? null : uuid;
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
        string? timeZoneId = null,
        int? productType = null,
        string? installationType = null,
        string? systemDrive = null,
        string? windowsDirectory = null,
        string? locale = null,
        int? currentTimeZoneMinutes = null)
        : base(managedServerId) =>
        Apply(
            caption, version, buildNumber, architecture, capturedAt, edition,
            installDate, lastBootTime, timeZoneId, productType,
            installationType, systemDrive, windowsDirectory, locale,
            currentTimeZoneMinutes);

    private WindowsOperatingSystemInventory()
    {
        Caption = Version = BuildNumber = Architecture = null!;
    }

    public Guid ManagedServerId => Id;
    public Guid InventoryRunId { get; private set; }
    public string Caption { get; private set; } = null!;
    public string Version { get; private set; } = null!;
    public string BuildNumber { get; private set; } = null!;
    public string? Edition { get; private set; }
    public string Architecture { get; private set; } = null!;
    public DateTime? InstallDate { get; private set; }
    public DateTime? LastBootTime { get; private set; }
    public string? TimeZoneId { get; private set; }
    public int? ProductType { get; private set; }
    public string? InstallationType { get; private set; }
    public string? SystemDrive { get; private set; }
    public string? WindowsDirectory { get; private set; }
    public string? Locale { get; private set; }
    public int? CurrentTimeZoneMinutes { get; private set; }
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
        string? timeZoneId = null,
        int? productType = null,
        string? installationType = null,
        string? systemDrive = null,
        string? windowsDirectory = null,
        string? locale = null,
        int? currentTimeZoneMinutes = null)
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
        ProductType = productType is < 1 or > 3
            ? throw new ArgumentOutOfRangeException(nameof(productType))
            : productType;
        InstallationType = InventoryEntityGuard.Optional(installationType, 100, nameof(installationType));
        SystemDrive = InventoryEntityGuard.Optional(systemDrive, 10, nameof(systemDrive));
        WindowsDirectory = InventoryEntityGuard.Optional(windowsDirectory, 260, nameof(windowsDirectory));
        Locale = InventoryEntityGuard.Optional(locale, 20, nameof(locale));
        CurrentTimeZoneMinutes = currentTimeZoneMinutes;
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }
}

public sealed class WindowsBiosInventory : Entity
{
    public WindowsBiosInventory(
        Guid managedServerId,
        DateTime capturedAt,
        string? manufacturer = null,
        string? smbiosBiosVersion = null,
        string? version = null,
        DateTime? releaseDate = null,
        string? serialNumber = null,
        int? smbiosMajorVersion = null,
        int? smbiosMinorVersion = null)
        : base(managedServerId) =>
        Apply(
            manufacturer, smbiosBiosVersion, version, releaseDate,
            serialNumber, smbiosMajorVersion, smbiosMinorVersion, capturedAt);

    private WindowsBiosInventory() { }

    public Guid ManagedServerId => Id;
    public Guid InventoryRunId { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? SmbiosBiosVersion { get; private set; }
    public string? Version { get; private set; }
    public DateTime? ReleaseDate { get; private set; }
    public string? SerialNumber { get; private set; }
    public int? SmbiosMajorVersion { get; private set; }
    public int? SmbiosMinorVersion { get; private set; }
    public DateTime CapturedAt { get; private set; }

    public void Apply(
        string? manufacturer,
        string? smbiosBiosVersion,
        string? version,
        DateTime? releaseDate,
        string? serialNumber,
        int? smbiosMajorVersion,
        int? smbiosMinorVersion,
        DateTime capturedAt)
    {
        Manufacturer = InventoryEntityGuard.Optional(manufacturer, 255, nameof(manufacturer));
        SmbiosBiosVersion = InventoryEntityGuard.Optional(smbiosBiosVersion, 255, nameof(smbiosBiosVersion));
        Version = InventoryEntityGuard.Optional(version, 255, nameof(version));
        ReleaseDate = releaseDate.HasValue
            ? InventoryEntityGuard.CapturedAt(releaseDate.Value, nameof(releaseDate))
            : null;
        SmbiosMajorVersion = InventoryEntityGuard.NonNegative(smbiosMajorVersion, nameof(smbiosMajorVersion)) is long major
            ? checked((int)major)
            : null;
        SmbiosMinorVersion = InventoryEntityGuard.NonNegative(smbiosMinorVersion, nameof(smbiosMinorVersion)) is long minor
            ? checked((int)minor)
            : null;
        SerialNumber = InventoryEntityGuard.Optional(serialNumber, 255, nameof(serialNumber));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }
}

public sealed class WindowsMemoryInventory : Entity
{
    public WindowsMemoryInventory(
        Guid id,
        Guid managedServerId,
        string moduleKey,
        long capacityBytes,
        DateTime capturedAt,
        string? deviceLocator = null,
        string? bankLabel = null,
        int? speedMhz = null,
        int? configuredClockSpeedMhz = null,
        string? manufacturer = null,
        string? partNumber = null,
        string? serialNumber = null,
        int? formFactor = null,
        int? memoryType = null)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        ModuleKey = InventoryEntityGuard.Required(moduleKey, 200, nameof(moduleKey));
        DeviceLocator = InventoryEntityGuard.Optional(deviceLocator, 255, nameof(deviceLocator));
        BankLabel = InventoryEntityGuard.Optional(bankLabel, 255, nameof(bankLabel));
        if (capacityBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityBytes));
        }
        CapacityBytes = capacityBytes;
        SpeedMHz = InventoryEntityGuard.NonNegative(speedMhz, nameof(speedMhz)) is long speed
            ? checked((int)speed)
            : null;
        ConfiguredClockSpeedMHz =
            InventoryEntityGuard.NonNegative(configuredClockSpeedMhz, nameof(configuredClockSpeedMhz)) is long configuredSpeed
                ? checked((int)configuredSpeed)
                : null;
        Manufacturer = InventoryEntityGuard.Optional(manufacturer, 255, nameof(manufacturer));
        PartNumber = InventoryEntityGuard.Optional(partNumber, 255, nameof(partNumber));
        SerialNumber = InventoryEntityGuard.Optional(serialNumber, 255, nameof(serialNumber));
        FormFactor = InventoryEntityGuard.NonNegative(formFactor, nameof(formFactor)) is long value
            ? checked((int)value)
            : null;
        MemoryType = InventoryEntityGuard.NonNegative(memoryType, nameof(memoryType)) is long type
            ? checked((int)type)
            : null;
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
        RowVersion = null!;
    }

    private WindowsMemoryInventory() { ModuleKey = null!; RowVersion = null!; }

    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public string ModuleKey { get; private set; }
    public string? DeviceLocator { get; private set; }
    public string? BankLabel { get; private set; }
    public long CapacityBytes { get; private set; }
    public int? SpeedMHz { get; private set; }
    public int? ConfiguredClockSpeedMHz { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? PartNumber { get; private set; }
    public string? SerialNumber { get; private set; }
    public int? FormFactor { get; private set; }
    public int? MemoryType { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
}

public sealed class WindowsProcessorInventory : Entity
{
    public WindowsProcessorInventory(
        Guid id, Guid managedServerId, string processorKey, DateTime capturedAt,
        string? deviceId = null, string? name = null, string? manufacturer = null,
        string? description = null, string? socketDesignation = null,
        string? processorId = null, int? coreCount = null,
        int? logicalProcessorCount = null, int? maxClockSpeedMhz = null,
        int? currentClockSpeedMhz = null, int? addressWidth = null,
        int? dataWidth = null, int? architecture = null,
        bool? virtualizationFirmwareEnabled = null,
        bool? secondLevelAddressTranslationExtensions = null,
        bool? vmMonitorModeExtensions = null)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        ProcessorKey = InventoryEntityGuard.Required(processorKey, 200, nameof(processorKey));
        DeviceId = InventoryEntityGuard.Optional(deviceId, 100, nameof(deviceId));
        Name = InventoryEntityGuard.Optional(name, 255, nameof(name));
        Manufacturer = InventoryEntityGuard.Optional(manufacturer, 255, nameof(manufacturer));
        Description = InventoryEntityGuard.Optional(description, 255, nameof(description));
        SocketDesignation = InventoryEntityGuard.Optional(socketDesignation, 255, nameof(socketDesignation));
        ProcessorId = InventoryEntityGuard.Optional(processorId, 100, nameof(processorId));
        CoreCount = InventoryEntityGuard.Positive(coreCount, nameof(coreCount));
        LogicalProcessorCount = InventoryEntityGuard.Positive(logicalProcessorCount, nameof(logicalProcessorCount));
        if (CoreCount.HasValue && LogicalProcessorCount < CoreCount)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalProcessorCount));
        }
        MaxClockSpeedMhz = ToNonNegativeInt(maxClockSpeedMhz, nameof(maxClockSpeedMhz));
        CurrentClockSpeedMhz = ToNonNegativeInt(currentClockSpeedMhz, nameof(currentClockSpeedMhz));
        AddressWidth = ValidateWidth(addressWidth, nameof(addressWidth));
        DataWidth = ValidateWidth(dataWidth, nameof(dataWidth));
        Architecture = ToNonNegativeInt(architecture, nameof(architecture));
        VirtualizationFirmwareEnabled = virtualizationFirmwareEnabled;
        SecondLevelAddressTranslationExtensions = secondLevelAddressTranslationExtensions;
        VmMonitorModeExtensions = vmMonitorModeExtensions;
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
        RowVersion = null!;
    }

    private WindowsProcessorInventory() { ProcessorKey = null!; RowVersion = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public string ProcessorKey { get; private set; }
    public string? DeviceId { get; private set; }
    public string? Name { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? Description { get; private set; }
    public string? SocketDesignation { get; private set; }
    public string? ProcessorId { get; private set; }
    public int? CoreCount { get; private set; }
    public int? LogicalProcessorCount { get; private set; }
    public int? MaxClockSpeedMhz { get; private set; }
    public int? CurrentClockSpeedMhz { get; private set; }
    public int? AddressWidth { get; private set; }
    public int? DataWidth { get; private set; }
    public int? Architecture { get; private set; }
    public bool? VirtualizationFirmwareEnabled { get; private set; }
    public bool? SecondLevelAddressTranslationExtensions { get; private set; }
    public bool? VmMonitorModeExtensions { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    private static int? ToNonNegativeInt(int? value, string name) =>
        InventoryEntityGuard.NonNegative(value, name) is long converted
            ? checked((int)converted)
            : null;

    private static int? ValidateWidth(int? value, string name) =>
        value is null or 32 or 64
            ? value
            : throw new ArgumentOutOfRangeException(name);
}

public sealed class WindowsDiskInventory : Entity
{
    public WindowsDiskInventory(
        Guid id, Guid managedServerId, string diskKey, DateTime capturedAt,
        int? diskNumber = null, string? friendlyName = null, string? serialNumber = null,
        long? sizeBytes = null, string? busType = null, string? partitionStyle = null)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        DiskKey = InventoryEntityGuard.Required(diskKey, 260, nameof(diskKey));
        DiskNumber = diskNumber < 0 ? throw new ArgumentOutOfRangeException(nameof(diskNumber)) : diskNumber;
        FriendlyName = InventoryEntityGuard.Optional(friendlyName, 255, nameof(friendlyName));
        SerialNumber = InventoryEntityGuard.Optional(serialNumber, 255, nameof(serialNumber));
        SizeBytes = InventoryEntityGuard.NonNegative(sizeBytes, nameof(sizeBytes));
        BusType = InventoryEntityGuard.Optional(busType, 100, nameof(busType));
        PartitionStyle = InventoryEntityGuard.Optional(partitionStyle, 50, nameof(partitionStyle));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsDiskInventory() { DiskKey = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public string DiskKey { get; private set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string StableSourceKey => DiskKey;
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
        Guid id, Guid managedServerId, string volumeKey, DateTime capturedAt,
        string? driveLetter = null, string? label = null, string? fileSystem = null,
        long? sizeBytes = null, long? freeSpaceBytes = null)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        VolumeKey = InventoryEntityGuard.Required(volumeKey, 260, nameof(volumeKey));
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

    private WindowsVolumeInventory() { VolumeKey = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public string VolumeKey { get; private set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string StableSourceKey => VolumeKey;
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
        Guid id, Guid managedServerId, string adapterKey, DateTime capturedAt,
        string? name = null, string? interfaceDescription = null, string? macAddress = null,
        string? operationalStatus = null, long? linkSpeedBitsPerSecond = null,
        string? interfaceGuid = null, int? interfaceIndex = null,
        string? friendlyName = null, string? manufacturer = null,
        bool? physicalAdapter = null, string? pnpDeviceId = null)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        AdapterKey = InventoryEntityGuard.Required(adapterKey, 200, nameof(adapterKey));
        Name = InventoryEntityGuard.Optional(name, 255, nameof(name));
        InterfaceDescription = InventoryEntityGuard.Optional(interfaceDescription, 500, nameof(interfaceDescription));
        MacAddress = InventoryEntityGuard.Optional(macAddress, 20, nameof(macAddress));
        OperationalStatus = InventoryEntityGuard.Optional(operationalStatus, 50, nameof(operationalStatus));
        LinkSpeedBitsPerSecond = InventoryEntityGuard.NonNegative(linkSpeedBitsPerSecond, nameof(linkSpeedBitsPerSecond));
        InterfaceGuid = InventoryEntityGuard.Optional(interfaceGuid, 36, nameof(interfaceGuid));
        InterfaceIndex = interfaceIndex < 0
            ? throw new ArgumentOutOfRangeException(nameof(interfaceIndex))
            : interfaceIndex;
        FriendlyName = InventoryEntityGuard.Optional(friendlyName, 255, nameof(friendlyName));
        Manufacturer = InventoryEntityGuard.Optional(manufacturer, 255, nameof(manufacturer));
        PhysicalAdapter = physicalAdapter;
        PnpDeviceId = InventoryEntityGuard.Optional(pnpDeviceId, 500, nameof(pnpDeviceId));
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsNetworkAdapterInventory() { AdapterKey = null!; RowVersion = null!; }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public string AdapterKey { get; private set; }
    public string? Name { get; private set; }
    public string? InterfaceDescription { get; private set; }
    public string? MacAddress { get; private set; }
    public string? OperationalStatus { get; private set; }
    public long? LinkSpeedBitsPerSecond { get; private set; }
    public string? InterfaceGuid { get; private set; }
    public int? InterfaceIndex { get; private set; }
    public string? FriendlyName { get; private set; }
    public string? Manufacturer { get; private set; }
    public bool? PhysicalAdapter { get; private set; }
    public string? PnpDeviceId { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
}

public sealed class WindowsIpv4AddressInventory : Entity
{
    public WindowsIpv4AddressInventory(
        Guid id, Guid managedServerId, Guid networkAdapterInventoryId,
        string ipv4Key, string address, int prefixLength, bool? isDhcp,
        DateTime capturedAt, string? adapterKey = null,
        string? defaultGateway = null, bool? dnsRegistrationEnabled = null)
        : base(id)
    {
        ManagedServerId = InventoryEntityGuard.Id(managedServerId, nameof(managedServerId));
        NetworkAdapterInventoryId =
            InventoryEntityGuard.Id(networkAdapterInventoryId, nameof(networkAdapterInventoryId));
        Ipv4Key = InventoryEntityGuard.Required(ipv4Key, 300, nameof(ipv4Key));
        AdapterKey = InventoryEntityGuard.Required(
            adapterKey!, 200, nameof(adapterKey));
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
        DefaultGateway = InventoryEntityGuard.Optional(defaultGateway, 15, nameof(defaultGateway));
        DnsRegistrationEnabled = dnsRegistrationEnabled;
        CapturedAt = InventoryEntityGuard.CapturedAt(capturedAt, nameof(capturedAt));
    }

    private WindowsIpv4AddressInventory()
    {
        Ipv4Key = AdapterKey = Address = null!;
        RowVersion = null!;
    }
    public Guid ManagedServerId { get; private set; }
    public Guid InventoryRunId { get; private set; }
    public Guid NetworkAdapterInventoryId { get; private set; }
    public string Ipv4Key { get; private set; }
    public string AdapterKey { get; private set; }
    public string Address { get; private set; }
    public int PrefixLength { get; private set; }
    public bool? IsDhcp { get; private set; }
    public string? DefaultGateway { get; private set; }
    public bool? DnsRegistrationEnabled { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;
}
