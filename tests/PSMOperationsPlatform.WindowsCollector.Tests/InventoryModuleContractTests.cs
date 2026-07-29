using Microsoft.EntityFrameworkCore;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class InventoryModuleContractTests
{
    [Fact]
    public void Context_is_narrow_and_excludes_persistence_and_secrets()
    {
        Type[] types = typeof(InventoryModuleContext).GetProperties()
            .Select(property => property.PropertyType)
            .ToArray();

        Assert.DoesNotContain(types, type => typeof(DbContext).IsAssignableFrom(type));
        Assert.DoesNotContain(types, type => type == typeof(OperationsDbContext));
        Assert.DoesNotContain(
            typeof(InventoryModuleContext).GetProperties(),
            property => property.Name.Contains("Credential", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Processor_memory_disk_and_volume_conform_to_lifecycle()
    {
        Assert.Contains(
            typeof(IInventoryModule<ProcessorInventoryItem[]>),
            typeof(ProcessorInventoryModule).GetInterfaces());
        Assert.Contains(
            typeof(IInventoryModule<PhysicalMemoryInventoryItem[]>),
            typeof(PhysicalMemoryInventoryModule).GetInterfaces());
        Assert.Contains(
            typeof(IInventoryModule<DiskInventoryItem[]>),
            typeof(PhysicalDiskInventoryModule).GetInterfaces());
        Assert.Contains(
            typeof(IInventoryModule<VolumeInventoryItem[]>),
            typeof(VolumeInventoryModule).GetInterfaces());
        Assert.Contains(
            typeof(IInventoryModule<NetworkAdapterInventoryItem[]>),
            typeof(NetworkAdapterInventoryModule).GetInterfaces());
        Assert.Contains(
            typeof(IInventoryModule<Ipv4AddressInventoryItem[]>),
            typeof(Ipv4InventoryModule).GetInterfaces());
    }

    [Fact]
    public void Result_exposes_valid_empty_and_safe_counts()
    {
        string[] names = typeof(InventoryModuleResult<>).GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains("IsValidEmpty", names);
        Assert.Contains("RawResultCount", names);
        Assert.Contains("NormalizedResultCount", names);
        Assert.DoesNotContain(names, name => name.Contains("RawOutput", StringComparison.Ordinal));
    }
}
