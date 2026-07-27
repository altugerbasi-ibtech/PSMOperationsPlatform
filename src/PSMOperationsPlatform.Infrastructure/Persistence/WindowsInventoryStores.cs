using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public sealed class InventoryTargetNotFoundException(Guid managedServerId)
    : InvalidOperationException($"Managed server '{managedServerId}' is not available.");

public sealed record ComputerInventoryState(
    string? ComputerName, string? Fqdn, string? DomainName,
    string? Manufacturer, string? Model, string? SerialNumber);

public sealed record OperatingSystemInventoryState(
    string Caption, string Version, string BuildNumber, string Architecture,
    string? Edition = null, DateTime? InstallDate = null,
    DateTime? LastBootTime = null, string? TimeZoneId = null);

public sealed record MemoryInventoryState(long TotalPhysicalMemoryBytes);

public sealed record ProcessorInventoryItem(
    string StableSourceKey, string? Name = null, string? Manufacturer = null,
    int? CoreCount = null, int? LogicalProcessorCount = null,
    int? MaxClockSpeedMhz = null);

public sealed record DiskInventoryItem(
    string StableSourceKey, int? DiskNumber = null, string? FriendlyName = null,
    string? SerialNumber = null, long? SizeBytes = null, string? BusType = null,
    string? PartitionStyle = null);

public sealed record VolumeInventoryItem(
    string StableSourceKey, string? DriveLetter = null, string? Label = null,
    string? FileSystem = null, long? SizeBytes = null, long? FreeSpaceBytes = null);

public sealed record NetworkAdapterInventoryItem(
    string StableSourceKey, string? Name = null,
    string? InterfaceDescription = null, string? MacAddress = null,
    string? OperationalStatus = null, long? LinkSpeedBitsPerSecond = null);

public sealed record Ipv4AddressInventoryItem(
    string NetworkAdapterStableSourceKey,
    string Address,
    int PrefixLength,
    bool? IsDhcp = null);

public sealed record NetworkInventorySnapshot(
    IReadOnlyList<NetworkAdapterInventoryItem> Adapters,
    IReadOnlyList<Ipv4AddressInventoryItem> Ipv4Addresses);

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
        WindowsMemoryInventory? entity =
            await context.WindowsMemoryInventories.FindAsync([managedServerId], cancellationToken);
        if (entity is null)
        {
            context.WindowsMemoryInventories.Add(
                new WindowsMemoryInventory(
                    managedServerId, state.TotalPhysicalMemoryBytes, capturedAt));
        }
        else
        {
            entity.Apply(state.TotalPhysicalMemoryBytes, capturedAt);
        }

        await context.SaveChangesAsync(cancellationToken);
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
        InventoryStoreGuard.UniqueKeys(input.Select(item => item.StableSourceKey), nameof(items));
        DateTime capturedAt = InventoryStoreGuard.CapturedAt(timeProvider);
        WindowsProcessorInventory[] entities = input.Select(item =>
            new WindowsProcessorInventory(
                Guid.NewGuid(), managedServerId, item.StableSourceKey, capturedAt,
                item.Name, item.Manufacturer, item.CoreCount,
                item.LogicalProcessorCount, item.MaxClockSpeedMhz)).ToArray();
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
                item.LinkSpeedBitsPerSecond)).ToArray();
        var addressKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        WindowsIpv4AddressInventory[] addressEntities = addresses.Select(item =>
        {
            string adapterKey = item.NetworkAdapterStableSourceKey?.Trim()
                ?? throw new ArgumentException("Adapter key is required.", nameof(snapshot));
            if (!adapterIds.TryGetValue(adapterKey, out Guid adapterId))
            {
                throw new ArgumentException("IPv4 address references an unknown adapter.", nameof(snapshot));
            }
            string stableKey = $"{adapterKey}|{item.Address}/{item.PrefixLength}";
            if (!addressKeys.Add(stableKey))
            {
                throw new ArgumentException("Duplicate IPv4 identity.", nameof(snapshot));
            }
            return new WindowsIpv4AddressInventory(
                Guid.NewGuid(), managedServerId, adapterId, stableKey,
                item.Address, item.PrefixLength, item.IsDhcp, capturedAt);
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
