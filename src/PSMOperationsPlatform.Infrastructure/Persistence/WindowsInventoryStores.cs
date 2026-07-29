using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public sealed class InventoryTargetNotFoundException(Guid managedServerId)
    : InvalidOperationException($"Managed server '{managedServerId}' is not available.");

public sealed record ComputerInventoryState(
    string? ComputerName, string? Fqdn, string? DomainName,
    string? Manufacturer, string? Model, string? SerialNumber,
    int? DomainRole = null, string? SystemType = null,
    bool? IsVirtualMachine = null, Guid? Uuid = null);

public sealed record OperatingSystemInventoryState(
    string Caption, string Version, string BuildNumber, string Architecture,
    string? Edition = null, DateTime? InstallDate = null,
    DateTime? LastBootTime = null, string? TimeZoneId = null,
    int? ProductType = null, string? InstallationType = null,
    string? SystemDrive = null, string? WindowsDirectory = null,
    string? Locale = null, int? CurrentTimeZoneMinutes = null);

public sealed record BiosInventoryState(
    string? Manufacturer, string? SmbiosBiosVersion, string? Version,
    DateTime? ReleaseDate, string? SerialNumber,
    int? SmbiosMajorVersion, int? SmbiosMinorVersion);

public sealed record MemoryInventoryState(long TotalPhysicalMemoryBytes);

public sealed record PhysicalMemoryInventoryItem(
    string ModuleKey,
    string? DeviceLocator,
    string? BankLabel,
    long CapacityBytes,
    int? SpeedMHz,
    int? ConfiguredClockSpeedMHz,
    string? Manufacturer,
    string? PartNumber,
    string? SerialNumber,
    int? FormFactor,
    int? MemoryType);

public sealed record ProcessorInventoryItem(
    string ProcessorKey, string? DeviceId = null, string? Name = null,
    string? Manufacturer = null, string? Description = null,
    string? SocketDesignation = null, string? ProcessorId = null,
    int? CoreCount = null, int? LogicalProcessorCount = null,
    int? MaxClockSpeedMhz = null, int? CurrentClockSpeedMhz = null,
    int? AddressWidth = null, int? DataWidth = null, int? Architecture = null,
    bool? VirtualizationFirmwareEnabled = null,
    bool? SecondLevelAddressTranslationExtensions = null,
    bool? VmMonitorModeExtensions = null);

public sealed record DiskInventoryItem(
    string DiskKey, string? DeviceId = null, int? Index = null,
    string? Model = null, string? Manufacturer = null,
    string? SerialNumber = null, string? FirmwareRevision = null,
    string? InterfaceType = null, string? MediaType = null,
    long? SizeBytes = null, int? BytesPerSector = null,
    int? Partitions = null, string? PnpDeviceId = null,
    string? Status = null)
{
    public string StableSourceKey => DiskKey;
    public int? DiskNumber => Index;
    public string? FriendlyName => Model;
    public string? BusType => InterfaceType;
    public string? PartitionStyle => null;
}

public sealed record VolumeInventoryItem(
    string VolumeKey, string? DeviceId = null, string? DriveLetter = null,
    string? Label = null, string? FileSystem = null,
    long? CapacityBytes = null, long? FreeSpaceBytes = null,
    int? BlockSize = null, int? DriveType = null,
    bool? IsBootVolume = null, bool? IsSystemVolume = null,
    bool? IsPageFileVolume = null, bool? IsDirty = null,
    string? SerialNumber = null)
{
    public string StableSourceKey => VolumeKey;
    public long? SizeBytes => CapacityBytes;
}

public sealed record NetworkAdapterInventoryItem(
    string AdapterKey,
    string? InterfaceGuid = null,
    int? InterfaceIndex = null,
    string? Name = null,
    string? FriendlyName = null,
    string? Description = null,
    string? MacAddress = null,
    string? Manufacturer = null,
    bool? PhysicalAdapter = null,
    int? NetConnectionStatus = null,
    long? Speed = null,
    string? PnpDeviceId = null)
{
    public string StableSourceKey => AdapterKey;
    public string? InterfaceDescription => Description;
    public string? OperationalStatus => NetConnectionStatus?.ToString();
    public long? LinkSpeedBitsPerSecond => Speed;
}

public sealed record Ipv4AddressInventoryItem(
    string Ipv4Key,
    string AdapterKey,
    string Address,
    int PrefixLength,
    string? DefaultGateway = null,
    bool? IsDhcp = null,
    bool? DnsRegistrationEnabled = null)
{
    public Ipv4AddressInventoryItem(
        string adapterKey,
        string address,
        int prefixLength,
        bool? isDhcp = null)
        : this(
            $"{adapterKey}|{address}", adapterKey, address, prefixLength,
            null, isDhcp, null)
    {
    }

    public string NetworkAdapterStableSourceKey => AdapterKey;
}

public sealed record NetworkInventorySnapshot(
    IReadOnlyList<NetworkAdapterInventoryItem> Adapters,
    IReadOnlyList<Ipv4AddressInventoryItem> Ipv4Addresses);

public sealed record WindowsRoleInventoryItem(
    string RoleKey, string Name, string? DisplayName, string? Parent,
    string? FeatureType);

public sealed record WindowsFeatureInventoryItem(
    string FeatureKey, string Name, string? DisplayName, string? Parent,
    string? RestartRequired, string? FeatureType);

public sealed record IisPlatformInventoryItem(
    string IisKey, bool Installed, string? Version);

public sealed record DotNetPlatformInventoryItem(
    string DotNetKey, string Category, string Name, string? Version,
    int? Release);

public sealed record PowerShellPlatformInventoryItem(
    string PowerShellKey, string Edition, string? Version, string Path);

public sealed record CoreWindowsInventorySnapshot(
    ComputerInventoryState Computer,
    OperatingSystemInventoryState OperatingSystem,
    BiosInventoryState Bios,
    IReadOnlyList<ProcessorInventoryItem> Processors,
    IReadOnlyList<PhysicalMemoryInventoryItem> MemoryModules,
    IReadOnlyList<DiskInventoryItem> Disks,
    IReadOnlyList<VolumeInventoryItem> Volumes,
    NetworkInventorySnapshot Network,
    IReadOnlyList<WindowsRoleInventoryItem>? WindowsRoles = null,
    IReadOnlyList<WindowsFeatureInventoryItem>? WindowsFeatures = null,
    IReadOnlyList<IisPlatformInventoryItem>? IisPlatforms = null,
    IReadOnlyList<DotNetPlatformInventoryItem>? DotNetPlatforms = null,
    IReadOnlyList<PowerShellPlatformInventoryItem>? PowerShellPlatforms = null);

public interface ICoreWindowsInventoryStore
{
    Task ReplaceAsync(
        Guid managedServerId,
        CoreWindowsInventorySnapshot snapshot,
        Guid inventoryRunId,
        DateTime capturedAt,
        DateTime nextInventoryAttemptAt,
        CancellationToken cancellationToken);
}

public interface IInventoryScheduleStore
{
    Task RecordFailureAsync(
        Guid managedServerId,
        DateTime attemptedAt,
        string failureCategory,
        CancellationToken cancellationToken);
}

public interface IComputerInventoryStore
{
    Task UpsertAsync(Guid managedServerId, ComputerInventoryState state, CancellationToken cancellationToken);
}

public interface IOperatingSystemInventoryStore
{
    Task UpsertAsync(Guid managedServerId, OperatingSystemInventoryState state, CancellationToken cancellationToken);
}

public interface IMemoryInventoryStore
{
    Task UpsertAsync(Guid managedServerId, MemoryInventoryState state, CancellationToken cancellationToken);
}

public interface IProcessorSnapshotStore
{
    Task ReplaceAsync(Guid managedServerId, IReadOnlyList<ProcessorInventoryItem> items, CancellationToken cancellationToken);
}

public interface IDiskSnapshotStore
{
    Task ReplaceAsync(Guid managedServerId, IReadOnlyList<DiskInventoryItem> items, CancellationToken cancellationToken);
}

public interface IVolumeSnapshotStore
{
    Task ReplaceAsync(Guid managedServerId, IReadOnlyList<VolumeInventoryItem> items, CancellationToken cancellationToken);
}

public interface INetworkSnapshotStore
{
    Task ReplaceAsync(Guid managedServerId, NetworkInventorySnapshot snapshot, CancellationToken cancellationToken);
}

internal static class InventoryStoreGuard
{
    internal static void TargetId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Managed server identifier cannot be empty.", nameof(id));
        }
    }

    internal static async Task TargetAsync(
        OperationsDbContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        TargetId(id);
        bool exists = await context.ManagedServers
            .AsNoTracking()
            .AnyAsync(server => server.Id == id && server.IsEnabled, cancellationToken);
        if (!exists)
        {
            throw new InventoryTargetNotFoundException(id);
        }
    }

    internal static void UniqueKeys(IEnumerable<string> keys, string parameterName)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in keys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key, parameterName);
            if (!unique.Add(key.Trim()))
            {
                throw new ArgumentException("Duplicate stable source key.", parameterName);
            }
        }
    }

    internal static DateTime CapturedAt(TimeProvider timeProvider) =>
        timeProvider.GetLocalNow().DateTime;

    internal static async Task RollbackAndClearAsync(
        OperationsDbContext context,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        context.ChangeTracker.Clear();
    }

    internal static async Task ExecuteTransactionAsync(
        OperationsDbContext context,
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy executionStrategy =
            context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await operation();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await RollbackAndClearAsync(
                    context,
                    transaction,
                    CancellationToken.None);
                throw;
            }
        });
    }
}

public sealed class ComputerInventoryStore(
    OperationsDbContext context,
    TimeProvider timeProvider) : IComputerInventoryStore
{
    public async Task UpsertAsync(
        Guid managedServerId,
        ComputerInventoryState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        await InventoryStoreGuard.TargetAsync(context, managedServerId, cancellationToken);
        DateTime capturedAt = InventoryStoreGuard.CapturedAt(timeProvider);
        WindowsComputerInventory? entity =
            await context.WindowsComputerInventories.FindAsync([managedServerId], cancellationToken);
        if (entity is null)
        {
            context.WindowsComputerInventories.Add(
                new WindowsComputerInventory(
                    managedServerId, capturedAt, state.ComputerName, state.Fqdn,
                    state.DomainName, state.Manufacturer, state.Model, state.SerialNumber));
        }
        else
        {
            entity.Apply(
                state.ComputerName, state.Fqdn, state.DomainName,
                state.Manufacturer, state.Model, state.SerialNumber, capturedAt);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class OperatingSystemInventoryStore(
    OperationsDbContext context,
    TimeProvider timeProvider) : IOperatingSystemInventoryStore
{
    public async Task UpsertAsync(
        Guid managedServerId,
        OperatingSystemInventoryState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        await InventoryStoreGuard.TargetAsync(context, managedServerId, cancellationToken);
        DateTime capturedAt = InventoryStoreGuard.CapturedAt(timeProvider);
        WindowsOperatingSystemInventory? entity =
            await context.WindowsOperatingSystemInventories.FindAsync([managedServerId], cancellationToken);
        if (entity is null)
        {
            context.WindowsOperatingSystemInventories.Add(
                new WindowsOperatingSystemInventory(
                    managedServerId, state.Caption, state.Version, state.BuildNumber,
                    state.Architecture, capturedAt, state.Edition, state.InstallDate,
                    state.LastBootTime, state.TimeZoneId));
        }
        else
        {
            entity.Apply(
                state.Caption, state.Version, state.BuildNumber, state.Architecture,
                capturedAt, state.Edition, state.InstallDate, state.LastBootTime,
                state.TimeZoneId);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class MemoryInventoryStore(
    OperationsDbContext context,
    TimeProvider timeProvider) : IMemoryInventoryStore
{
    public async Task UpsertAsync(
        Guid managedServerId,
        MemoryInventoryState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        await InventoryStoreGuard.TargetAsync(context, managedServerId, cancellationToken);
        DateTime capturedAt = InventoryStoreGuard.CapturedAt(timeProvider);
        context.WindowsMemoryInventories.RemoveRange(
            await context.WindowsMemoryInventories
                .Where(item => item.ManagedServerId == managedServerId)
                .ToListAsync(cancellationToken));
        context.WindowsMemoryInventories.Add(
            new WindowsMemoryInventory(
                Guid.NewGuid(),
                managedServerId,
                "legacy-total",
                state.TotalPhysicalMemoryBytes,
                capturedAt));
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CoreWindowsInventoryStore(
    OperationsDbContext context,
    ILogger<CoreWindowsInventoryStore> logger,
    IWindowsCapabilityCoordinator? capabilityCoordinator = null) : ICoreWindowsInventoryStore
{
    public async Task ReplaceAsync(
        Guid managedServerId,
        CoreWindowsInventorySnapshot snapshot,
        Guid inventoryRunId,
        DateTime capturedAt,
        DateTime nextInventoryAttemptAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        InventoryStoreGuard.TargetId(managedServerId);
        Validate(managedServerId, snapshot, capturedAt);

        logger.LogInformation(
            "Core inventory persistence started. TargetId={TargetId} InventoryRunId={InventoryRunId}",
            managedServerId,
            inventoryRunId);
        long inventoryVersion = 0;
        try
        {
            await InventoryStoreGuard.ExecuteTransactionAsync(
                context,
                async () =>
                {
                ManagedServer target = await context.ManagedServers
                    .SingleOrDefaultAsync(
                        server => server.Id == managedServerId && server.IsEnabled,
                        cancellationToken)
                    ?? throw new InventoryTargetNotFoundException(managedServerId);

                await ReplaceSingularAsync(target, snapshot, capturedAt, cancellationToken);
                await RemoveCollectionsAsync(managedServerId, cancellationToken);
                AddCollections(managedServerId, snapshot, capturedAt);
                StampInventoryRun(managedServerId, inventoryRunId);
                target.ApplyInventorySuccess(capturedAt, nextInventoryAttemptAt);
                await context.SaveChangesAsync(cancellationToken);
                inventoryVersion = target.InventoryVersion;
                },
                cancellationToken);
            if (capabilityCoordinator is not null)
                await capabilityCoordinator.EvaluateAndReplaceAsync(
                    managedServerId, inventoryRunId, inventoryVersion, capturedAt,
                    snapshot, cancellationToken);
            logger.LogInformation(
                "Core inventory transaction committed. TargetId={TargetId} InventoryRunId={InventoryRunId}",
                managedServerId,
                inventoryRunId);
        }
        catch
        {
            logger.LogWarning(
                "Core inventory transaction rolled back. TargetId={TargetId} InventoryRunId={InventoryRunId}",
                managedServerId,
                inventoryRunId);
            throw;
        }
    }

    private void StampInventoryRun(Guid managedServerId, Guid inventoryRunId)
    {
        InventoryStoreGuard.TargetId(inventoryRunId);
        foreach (var entry in context.ChangeTracker.Entries())
        {
            Guid? ownerId = entry.Entity switch
            {
                WindowsComputerInventory value => value.ManagedServerId,
                WindowsOperatingSystemInventory value => value.ManagedServerId,
                WindowsBiosInventory value => value.ManagedServerId,
                WindowsMemoryInventory value => value.ManagedServerId,
                WindowsProcessorInventory value => value.ManagedServerId,
                WindowsDiskInventory value => value.ManagedServerId,
                WindowsVolumeInventory value => value.ManagedServerId,
                WindowsNetworkAdapterInventory value => value.ManagedServerId,
                WindowsIpv4AddressInventory value => value.ManagedServerId,
                WindowsRoleInventory value => value.ManagedServerId,
                WindowsFeatureInventory value => value.ManagedServerId,
                WindowsIisPlatformInventory value => value.ManagedServerId,
                WindowsDotNetPlatformInventory value => value.ManagedServerId,
                WindowsPowerShellPlatformInventory value => value.ManagedServerId,
                _ => null,
            };
            if (ownerId == managedServerId)
            {
                entry.Property("InventoryRunId").CurrentValue = inventoryRunId;
            }
        }
    }

    private async Task ReplaceSingularAsync(
        ManagedServer target,
        CoreWindowsInventorySnapshot snapshot,
        DateTime capturedAt,
        CancellationToken cancellationToken)
    {
        WindowsComputerInventory? computer =
            await context.WindowsComputerInventories.FindAsync([target.Id], cancellationToken);
        if (computer is null)
        {
            context.WindowsComputerInventories.Add(new(
                target.Id, capturedAt, snapshot.Computer.ComputerName,
                snapshot.Computer.Fqdn, snapshot.Computer.DomainName,
                snapshot.Computer.Manufacturer, snapshot.Computer.Model,
                snapshot.Computer.SerialNumber, snapshot.Computer.DomainRole,
                snapshot.Computer.SystemType, snapshot.Computer.IsVirtualMachine,
                snapshot.Computer.Uuid));
        }
        else
        {
            computer.Apply(
                snapshot.Computer.ComputerName, snapshot.Computer.Fqdn,
                snapshot.Computer.DomainName, snapshot.Computer.Manufacturer,
                snapshot.Computer.Model, snapshot.Computer.SerialNumber, capturedAt,
                snapshot.Computer.DomainRole, snapshot.Computer.SystemType,
                snapshot.Computer.IsVirtualMachine, snapshot.Computer.Uuid);
        }

        WindowsOperatingSystemInventory? operatingSystem =
            await context.WindowsOperatingSystemInventories.FindAsync([target.Id], cancellationToken);
        if (operatingSystem is null)
        {
            context.WindowsOperatingSystemInventories.Add(new(
                target.Id, snapshot.OperatingSystem.Caption,
                snapshot.OperatingSystem.Version, snapshot.OperatingSystem.BuildNumber,
                snapshot.OperatingSystem.Architecture, capturedAt,
                snapshot.OperatingSystem.Edition, snapshot.OperatingSystem.InstallDate,
                snapshot.OperatingSystem.LastBootTime, snapshot.OperatingSystem.TimeZoneId,
                snapshot.OperatingSystem.ProductType,
                snapshot.OperatingSystem.InstallationType,
                snapshot.OperatingSystem.SystemDrive,
                snapshot.OperatingSystem.WindowsDirectory,
                snapshot.OperatingSystem.Locale,
                snapshot.OperatingSystem.CurrentTimeZoneMinutes));
        }
        else
        {
            operatingSystem.Apply(
                snapshot.OperatingSystem.Caption, snapshot.OperatingSystem.Version,
                snapshot.OperatingSystem.BuildNumber, snapshot.OperatingSystem.Architecture,
                capturedAt, snapshot.OperatingSystem.Edition,
                snapshot.OperatingSystem.InstallDate,
                snapshot.OperatingSystem.LastBootTime,
                snapshot.OperatingSystem.TimeZoneId,
                snapshot.OperatingSystem.ProductType,
                snapshot.OperatingSystem.InstallationType,
                snapshot.OperatingSystem.SystemDrive,
                snapshot.OperatingSystem.WindowsDirectory,
                snapshot.OperatingSystem.Locale,
                snapshot.OperatingSystem.CurrentTimeZoneMinutes);
        }

        WindowsBiosInventory? bios =
            await context.WindowsBiosInventories.FindAsync([target.Id], cancellationToken);
        if (bios is null)
        {
            context.WindowsBiosInventories.Add(new(
                target.Id, capturedAt, snapshot.Bios.Manufacturer,
                snapshot.Bios.SmbiosBiosVersion, snapshot.Bios.Version,
                snapshot.Bios.ReleaseDate, snapshot.Bios.SerialNumber,
                snapshot.Bios.SmbiosMajorVersion, snapshot.Bios.SmbiosMinorVersion));
        }
        else
        {
            bios.Apply(
                snapshot.Bios.Manufacturer, snapshot.Bios.SmbiosBiosVersion,
                snapshot.Bios.Version, snapshot.Bios.ReleaseDate,
                snapshot.Bios.SerialNumber, snapshot.Bios.SmbiosMajorVersion,
                snapshot.Bios.SmbiosMinorVersion, capturedAt);
        }
    }

    private async Task RemoveCollectionsAsync(
        Guid managedServerId,
        CancellationToken cancellationToken)
    {
        context.WindowsPowerShellPlatformInventories.RemoveRange(
            await context.WindowsPowerShellPlatformInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
        context.WindowsDotNetPlatformInventories.RemoveRange(
            await context.WindowsDotNetPlatformInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
        context.WindowsIisPlatformInventories.RemoveRange(
            await context.WindowsIisPlatformInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
        context.WindowsFeatureInventories.RemoveRange(
            await context.WindowsFeatureInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
        context.WindowsRoleInventories.RemoveRange(
            await context.WindowsRoleInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
        context.WindowsIpv4AddressInventories.RemoveRange(
            await context.WindowsIpv4AddressInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
        context.WindowsNetworkAdapterInventories.RemoveRange(
            await context.WindowsNetworkAdapterInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
        context.WindowsVolumeInventories.RemoveRange(
            await context.WindowsVolumeInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
        context.WindowsDiskInventories.RemoveRange(
            await context.WindowsDiskInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
        context.WindowsMemoryInventories.RemoveRange(
            await context.WindowsMemoryInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
        context.WindowsProcessorInventories.RemoveRange(
            await context.WindowsProcessorInventories.Where(x => x.ManagedServerId == managedServerId).ToListAsync(cancellationToken));
    }

    private void AddCollections(
        Guid managedServerId,
        CoreWindowsInventorySnapshot snapshot,
        DateTime capturedAt)
    {
        context.WindowsProcessorInventories.AddRange(snapshot.Processors.Select(item =>
            new WindowsProcessorInventory(
                Guid.NewGuid(), managedServerId, item.ProcessorKey, capturedAt,
                item.DeviceId, item.Name, item.Manufacturer, item.Description,
                item.SocketDesignation, item.ProcessorId, item.CoreCount,
                item.LogicalProcessorCount, item.MaxClockSpeedMhz,
                item.CurrentClockSpeedMhz, item.AddressWidth, item.DataWidth,
                item.Architecture, item.VirtualizationFirmwareEnabled,
                item.SecondLevelAddressTranslationExtensions,
                item.VmMonitorModeExtensions)));
        context.WindowsMemoryInventories.AddRange(snapshot.MemoryModules.Select(item =>
            new WindowsMemoryInventory(
                Guid.NewGuid(), managedServerId, item.ModuleKey, item.CapacityBytes,
                capturedAt, item.DeviceLocator, item.BankLabel, item.SpeedMHz,
                item.ConfiguredClockSpeedMHz, item.Manufacturer, item.PartNumber,
                item.SerialNumber, item.FormFactor, item.MemoryType)));
        context.WindowsDiskInventories.AddRange(snapshot.Disks.Select(item =>
            new WindowsDiskInventory(
                Guid.NewGuid(), managedServerId, item.StableSourceKey, capturedAt,
                item.DiskNumber, item.FriendlyName, item.SerialNumber,
                item.SizeBytes, item.BusType, item.PartitionStyle)));
        context.WindowsVolumeInventories.AddRange(snapshot.Volumes.Select(item =>
            new WindowsVolumeInventory(
                Guid.NewGuid(), managedServerId, item.StableSourceKey, capturedAt,
                item.DriveLetter, item.Label, item.FileSystem,
                item.SizeBytes, item.FreeSpaceBytes)));

        var adapterIds = snapshot.Network.Adapters.ToDictionary(
            item => item.StableSourceKey,
            _ => Guid.NewGuid(),
            StringComparer.OrdinalIgnoreCase);
        context.WindowsNetworkAdapterInventories.AddRange(
            snapshot.Network.Adapters.Select(item =>
                new WindowsNetworkAdapterInventory(
                    adapterIds[item.StableSourceKey], managedServerId,
                    item.StableSourceKey, capturedAt, item.Name,
                    item.InterfaceDescription, item.MacAddress,
                    item.OperationalStatus, item.LinkSpeedBitsPerSecond,
                    item.InterfaceGuid, item.InterfaceIndex, item.FriendlyName,
                    item.Manufacturer, item.PhysicalAdapter, item.PnpDeviceId)));
        context.WindowsIpv4AddressInventories.AddRange(
            snapshot.Network.Ipv4Addresses.Select(item =>
            {
                return new WindowsIpv4AddressInventory(
                    Guid.NewGuid(), managedServerId,
                    adapterIds[item.NetworkAdapterStableSourceKey], item.Ipv4Key,
                    item.Address, item.PrefixLength, item.IsDhcp, capturedAt,
                    item.AdapterKey, item.DefaultGateway,
                    item.DnsRegistrationEnabled);
            }));
        context.WindowsRoleInventories.AddRange(
            (snapshot.WindowsRoles ?? []).Select(item =>
                new WindowsRoleInventory(
                    Guid.NewGuid(), managedServerId, item.RoleKey, item.Name,
                    capturedAt, item.DisplayName, item.Parent, item.FeatureType)));
        context.WindowsFeatureInventories.AddRange(
            (snapshot.WindowsFeatures ?? []).Select(item =>
                new WindowsFeatureInventory(
                    Guid.NewGuid(), managedServerId, item.FeatureKey, item.Name,
                    capturedAt, item.DisplayName, item.Parent,
                    item.RestartRequired, item.FeatureType)));
        context.WindowsIisPlatformInventories.AddRange(
            (snapshot.IisPlatforms ?? []).Select(item =>
                new WindowsIisPlatformInventory(
                    Guid.NewGuid(), managedServerId, item.IisKey,
                    item.Installed, capturedAt, item.Version)));
        context.WindowsDotNetPlatformInventories.AddRange(
            (snapshot.DotNetPlatforms ?? []).Select(item =>
                new WindowsDotNetPlatformInventory(
                    Guid.NewGuid(), managedServerId, item.DotNetKey,
                    item.Category, item.Name, capturedAt, item.Version,
                    item.Release)));
        context.WindowsPowerShellPlatformInventories.AddRange(
            (snapshot.PowerShellPlatforms ?? []).Select(item =>
                new WindowsPowerShellPlatformInventory(
                    Guid.NewGuid(), managedServerId, item.PowerShellKey,
                    item.Edition, item.Path, capturedAt, item.Version)));
    }

    private static void Validate(
        Guid managedServerId,
        CoreWindowsInventorySnapshot snapshot,
        DateTime capturedAt)
    {
        ArgumentNullException.ThrowIfNull(snapshot.Computer);
        ArgumentNullException.ThrowIfNull(snapshot.OperatingSystem);
        ArgumentNullException.ThrowIfNull(snapshot.Bios);
        InventoryStoreGuard.UniqueKeys(snapshot.Processors.Select(x => x.ProcessorKey), nameof(snapshot));
        InventoryStoreGuard.UniqueKeys(snapshot.MemoryModules.Select(x => x.ModuleKey), nameof(snapshot));
        InventoryStoreGuard.UniqueKeys(snapshot.Disks.Select(x => x.StableSourceKey), nameof(snapshot));
        InventoryStoreGuard.UniqueKeys(snapshot.Volumes.Select(x => x.StableSourceKey), nameof(snapshot));
        InventoryStoreGuard.UniqueKeys(snapshot.Network.Adapters.Select(x => x.StableSourceKey), nameof(snapshot));
        InventoryStoreGuard.UniqueKeys(
            (snapshot.WindowsRoles ?? []).Select(x => x.RoleKey), nameof(snapshot));
        InventoryStoreGuard.UniqueKeys(
            (snapshot.WindowsFeatures ?? []).Select(x => x.FeatureKey), nameof(snapshot));
        InventoryStoreGuard.UniqueKeys(
            (snapshot.IisPlatforms ?? []).Select(x => x.IisKey), nameof(snapshot));
        InventoryStoreGuard.UniqueKeys(
            (snapshot.DotNetPlatforms ?? []).Select(x => x.DotNetKey), nameof(snapshot));
        InventoryStoreGuard.UniqueKeys(
            (snapshot.PowerShellPlatforms ?? []).Select(x => x.PowerShellKey), nameof(snapshot));
        if (snapshot.Processors.Count == 0)
        {
            throw new ArgumentException("Processor collection cannot be empty.", nameof(snapshot));
        }
        if (snapshot.Volumes.Count == 0)
        {
            throw new ArgumentException("Volume collection cannot be empty.", nameof(snapshot));
        }
        var adapterKeys = snapshot.Network.Adapters
            .Select(x => x.StableSourceKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (snapshot.Network.Ipv4Addresses.Any(
            x => !adapterKeys.Contains(x.NetworkAdapterStableSourceKey)))
        {
            throw new ArgumentException("IPv4 address references an unknown adapter.", nameof(snapshot));
        }

        _ = new WindowsComputerInventory(
            managedServerId, capturedAt, snapshot.Computer.ComputerName,
            snapshot.Computer.Fqdn, snapshot.Computer.DomainName,
            snapshot.Computer.Manufacturer, snapshot.Computer.Model,
            snapshot.Computer.SerialNumber, snapshot.Computer.DomainRole,
            snapshot.Computer.SystemType, snapshot.Computer.IsVirtualMachine,
            snapshot.Computer.Uuid);
        _ = new WindowsOperatingSystemInventory(
            managedServerId, snapshot.OperatingSystem.Caption,
            snapshot.OperatingSystem.Version, snapshot.OperatingSystem.BuildNumber,
            snapshot.OperatingSystem.Architecture, capturedAt,
            snapshot.OperatingSystem.Edition, snapshot.OperatingSystem.InstallDate,
            snapshot.OperatingSystem.LastBootTime, snapshot.OperatingSystem.TimeZoneId,
            snapshot.OperatingSystem.ProductType,
            snapshot.OperatingSystem.InstallationType,
            snapshot.OperatingSystem.SystemDrive,
            snapshot.OperatingSystem.WindowsDirectory,
            snapshot.OperatingSystem.Locale,
            snapshot.OperatingSystem.CurrentTimeZoneMinutes);
        _ = new WindowsBiosInventory(
            managedServerId, capturedAt, snapshot.Bios.Manufacturer,
            snapshot.Bios.SmbiosBiosVersion, snapshot.Bios.Version,
            snapshot.Bios.ReleaseDate, snapshot.Bios.SerialNumber,
            snapshot.Bios.SmbiosMajorVersion, snapshot.Bios.SmbiosMinorVersion);
        foreach (ProcessorInventoryItem item in snapshot.Processors)
        {
            _ = new WindowsProcessorInventory(
                Guid.NewGuid(), managedServerId, item.ProcessorKey, capturedAt,
                item.DeviceId, item.Name, item.Manufacturer, item.Description,
                item.SocketDesignation, item.ProcessorId, item.CoreCount,
                item.LogicalProcessorCount, item.MaxClockSpeedMhz,
                item.CurrentClockSpeedMhz, item.AddressWidth, item.DataWidth,
                item.Architecture, item.VirtualizationFirmwareEnabled,
                item.SecondLevelAddressTranslationExtensions,
                item.VmMonitorModeExtensions);
        }
        foreach (PhysicalMemoryInventoryItem item in snapshot.MemoryModules)
        {
            _ = new WindowsMemoryInventory(
                Guid.NewGuid(), managedServerId, item.ModuleKey, item.CapacityBytes,
                capturedAt, item.DeviceLocator, item.BankLabel, item.SpeedMHz,
                item.ConfiguredClockSpeedMHz, item.Manufacturer, item.PartNumber,
                item.SerialNumber, item.FormFactor, item.MemoryType);
        }
        foreach (DiskInventoryItem item in snapshot.Disks)
        {
            _ = new WindowsDiskInventory(
                Guid.NewGuid(), managedServerId, item.StableSourceKey, capturedAt,
                item.DiskNumber, item.FriendlyName, item.SerialNumber,
                item.SizeBytes, item.BusType, item.PartitionStyle);
        }
        foreach (VolumeInventoryItem item in snapshot.Volumes)
        {
            _ = new WindowsVolumeInventory(
                Guid.NewGuid(), managedServerId, item.StableSourceKey, capturedAt,
                item.DriveLetter, item.Label, item.FileSystem,
                item.SizeBytes, item.FreeSpaceBytes);
        }
        var validationAdapterIds = snapshot.Network.Adapters.ToDictionary(
            item => item.StableSourceKey,
            _ => Guid.NewGuid(),
            StringComparer.OrdinalIgnoreCase);
        foreach (NetworkAdapterInventoryItem item in snapshot.Network.Adapters)
        {
            _ = new WindowsNetworkAdapterInventory(
                validationAdapterIds[item.StableSourceKey], managedServerId,
                item.StableSourceKey, capturedAt, item.Name,
                item.InterfaceDescription, item.MacAddress,
                item.OperationalStatus, item.LinkSpeedBitsPerSecond,
                item.InterfaceGuid, item.InterfaceIndex, item.FriendlyName,
                item.Manufacturer, item.PhysicalAdapter, item.PnpDeviceId);
        }
        foreach (Ipv4AddressInventoryItem item in snapshot.Network.Ipv4Addresses)
        {
            _ = new WindowsIpv4AddressInventory(
                Guid.NewGuid(), managedServerId,
                validationAdapterIds[item.NetworkAdapterStableSourceKey],
                item.Ipv4Key, item.Address, item.PrefixLength, item.IsDhcp,
                capturedAt, item.AdapterKey, item.DefaultGateway,
                item.DnsRegistrationEnabled);
        }
    }
}

public sealed class InventoryScheduleStore(
    OperationsDbContext context,
    ILogger<InventoryScheduleStore> logger) : IInventoryScheduleStore
{
    public async Task RecordFailureAsync(
        Guid managedServerId,
        DateTime attemptedAt,
        string failureCategory,
        CancellationToken cancellationToken)
    {
        ManagedServer target = await context.ManagedServers.SingleAsync(
            server => server.Id == managedServerId && server.IsEnabled,
            cancellationToken);
        int nextFailure = target.ConsecutiveInventoryFailures == int.MaxValue
            ? int.MaxValue
            : target.ConsecutiveInventoryFailures + 1;
        TimeSpan delay = nextFailure switch
        {
            1 => TimeSpan.FromMinutes(5),
            2 => TimeSpan.FromMinutes(15),
            3 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromMinutes(60),
        };
        target.ApplyInventoryFailure(
            attemptedAt,
            failureCategory,
            attemptedAt.Add(delay));
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Inventory failure schedule recorded. TargetId={TargetId} FailureCategory={FailureCategory} ConsecutiveFailures={ConsecutiveFailures} NextInventoryAttemptAt={NextInventoryAttemptAt}",
            managedServerId,
            failureCategory,
            target.ConsecutiveInventoryFailures,
            target.NextInventoryAttemptAt);
    }
}

public sealed class ProcessorSnapshotStore(
    OperationsDbContext context,
    TimeProvider timeProvider) : IProcessorSnapshotStore
{
    public async Task ReplaceAsync(
        Guid managedServerId,
        IReadOnlyList<ProcessorInventoryItem> items,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ProcessorInventoryItem[] input = items.ToArray();
        InventoryStoreGuard.UniqueKeys(input.Select(item => item.ProcessorKey), nameof(items));
        DateTime capturedAt = InventoryStoreGuard.CapturedAt(timeProvider);
        WindowsProcessorInventory[] entities = input.Select(item =>
            new WindowsProcessorInventory(
                Guid.NewGuid(), managedServerId, item.ProcessorKey, capturedAt,
                item.DeviceId, item.Name, item.Manufacturer, item.Description,
                item.SocketDesignation, item.ProcessorId, item.CoreCount,
                item.LogicalProcessorCount, item.MaxClockSpeedMhz,
                item.CurrentClockSpeedMhz, item.AddressWidth, item.DataWidth,
                item.Architecture, item.VirtualizationFirmwareEnabled,
                item.SecondLevelAddressTranslationExtensions,
                item.VmMonitorModeExtensions)).ToArray();
        await InventoryStoreGuard.TargetAsync(context, managedServerId, cancellationToken);

        await InventoryStoreGuard.ExecuteTransactionAsync(
            context,
            async () =>
            {
                context.WindowsProcessorInventories.RemoveRange(
                    await context.WindowsProcessorInventories
                        .Where(item => item.ManagedServerId == managedServerId)
                        .ToListAsync(cancellationToken));
                context.WindowsProcessorInventories.AddRange(entities);
                await context.SaveChangesAsync(cancellationToken);
            },
            cancellationToken);
    }
}

public sealed class DiskSnapshotStore(
    OperationsDbContext context,
    TimeProvider timeProvider) : IDiskSnapshotStore
{
    public async Task ReplaceAsync(Guid managedServerId, IReadOnlyList<DiskInventoryItem> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        DiskInventoryItem[] input = items.ToArray();
        InventoryStoreGuard.UniqueKeys(input.Select(item => item.StableSourceKey), nameof(items));
        DateTime capturedAt = InventoryStoreGuard.CapturedAt(timeProvider);
        WindowsDiskInventory[] entities = input.Select(item =>
            new WindowsDiskInventory(
                Guid.NewGuid(), managedServerId, item.StableSourceKey, capturedAt,
                item.DiskNumber, item.FriendlyName, item.SerialNumber, item.SizeBytes,
                item.BusType, item.PartitionStyle)).ToArray();
        await InventoryStoreGuard.TargetAsync(context, managedServerId, cancellationToken);
        await InventoryStoreGuard.ExecuteTransactionAsync(
            context,
            async () =>
            {
                context.WindowsDiskInventories.RemoveRange(
                    await context.WindowsDiskInventories
                        .Where(item => item.ManagedServerId == managedServerId)
                        .ToListAsync(cancellationToken));
                context.WindowsDiskInventories.AddRange(entities);
                await context.SaveChangesAsync(cancellationToken);
            },
            cancellationToken);
    }
}

public sealed class VolumeSnapshotStore(
    OperationsDbContext context,
    TimeProvider timeProvider) : IVolumeSnapshotStore
{
    public async Task ReplaceAsync(Guid managedServerId, IReadOnlyList<VolumeInventoryItem> items, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        VolumeInventoryItem[] input = items.ToArray();
        InventoryStoreGuard.UniqueKeys(input.Select(item => item.StableSourceKey), nameof(items));
        DateTime capturedAt = InventoryStoreGuard.CapturedAt(timeProvider);
        WindowsVolumeInventory[] entities = input.Select(item =>
            new WindowsVolumeInventory(
                Guid.NewGuid(), managedServerId, item.StableSourceKey, capturedAt,
                item.DriveLetter, item.Label, item.FileSystem, item.SizeBytes,
                item.FreeSpaceBytes)).ToArray();
        await InventoryStoreGuard.TargetAsync(context, managedServerId, cancellationToken);
        await InventoryStoreGuard.ExecuteTransactionAsync(
            context,
            async () =>
            {
                context.WindowsVolumeInventories.RemoveRange(
                    await context.WindowsVolumeInventories
                        .Where(item => item.ManagedServerId == managedServerId)
                        .ToListAsync(cancellationToken));
                context.WindowsVolumeInventories.AddRange(entities);
                await context.SaveChangesAsync(cancellationToken);
            },
            cancellationToken);
    }
}

public sealed class NetworkSnapshotStore(
    OperationsDbContext context,
    TimeProvider timeProvider) : INetworkSnapshotStore
{
    public async Task ReplaceAsync(
        Guid managedServerId,
        NetworkInventorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        NetworkAdapterInventoryItem[] adapters = snapshot.Adapters?.ToArray()
            ?? throw new ArgumentException("Adapters are required.", nameof(snapshot));
        Ipv4AddressInventoryItem[] addresses = snapshot.Ipv4Addresses?.ToArray()
            ?? throw new ArgumentException("IPv4 addresses are required.", nameof(snapshot));
        InventoryStoreGuard.UniqueKeys(adapters.Select(item => item.StableSourceKey), nameof(snapshot));
        var adapterIds = adapters.ToDictionary(
            item => item.StableSourceKey.Trim(),
            _ => Guid.NewGuid(),
            StringComparer.OrdinalIgnoreCase);
        DateTime capturedAt = InventoryStoreGuard.CapturedAt(timeProvider);
        WindowsNetworkAdapterInventory[] adapterEntities = adapters.Select(item =>
            new WindowsNetworkAdapterInventory(
                adapterIds[item.StableSourceKey.Trim()], managedServerId,
                item.StableSourceKey, capturedAt, item.Name,
                item.InterfaceDescription, item.MacAddress, item.OperationalStatus,
                item.LinkSpeedBitsPerSecond, item.InterfaceGuid,
                item.InterfaceIndex, item.FriendlyName, item.Manufacturer,
                item.PhysicalAdapter, item.PnpDeviceId)).ToArray();
        var addressKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        WindowsIpv4AddressInventory[] addressEntities = addresses.Select(item =>
        {
            string adapterKey = item.NetworkAdapterStableSourceKey?.Trim()
                ?? throw new ArgumentException("Adapter key is required.", nameof(snapshot));
            if (!adapterIds.TryGetValue(adapterKey, out Guid adapterId))
            {
                throw new ArgumentException("IPv4 address references an unknown adapter.", nameof(snapshot));
            }
            if (!addressKeys.Add(item.Ipv4Key))
            {
                throw new ArgumentException("Duplicate IPv4 identity.", nameof(snapshot));
            }
            return new WindowsIpv4AddressInventory(
                Guid.NewGuid(), managedServerId, adapterId, item.Ipv4Key,
                item.Address, item.PrefixLength, item.IsDhcp, capturedAt,
                item.AdapterKey, item.DefaultGateway,
                item.DnsRegistrationEnabled);
        }).ToArray();
        await InventoryStoreGuard.TargetAsync(context, managedServerId, cancellationToken);

        await InventoryStoreGuard.ExecuteTransactionAsync(
            context,
            async () =>
            {
                context.WindowsIpv4AddressInventories.RemoveRange(
                    await context.WindowsIpv4AddressInventories
                        .Where(item => item.ManagedServerId == managedServerId)
                        .ToListAsync(cancellationToken));
                context.WindowsNetworkAdapterInventories.RemoveRange(
                    await context.WindowsNetworkAdapterInventories
                        .Where(item => item.ManagedServerId == managedServerId)
                        .ToListAsync(cancellationToken));
                context.WindowsNetworkAdapterInventories.AddRange(adapterEntities);
                context.WindowsIpv4AddressInventories.AddRange(addressEntities);
                await context.SaveChangesAsync(cancellationToken);
            },
            cancellationToken);
    }
}
