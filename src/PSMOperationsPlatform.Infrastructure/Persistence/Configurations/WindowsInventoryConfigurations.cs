using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Common;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

internal static class WindowsInventoryConfiguration
{
    internal static void Singular<TEntity>(EntityTypeBuilder<TEntity> builder, string table)
        where TEntity : Entity
    {
        builder.ToTable(table, "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id)
            .HasColumnName("ManagedServerId")
            .ValueGeneratedNever();
        builder.HasOne<ManagedServer>()
            .WithOne()
            .HasForeignKey<TEntity>(entity => entity.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName($"FK_{table}_ManagedServer_ManagedServerId");
    }

    internal static void Collection<TEntity>(
        EntityTypeBuilder<TEntity> builder,
        string table,
        int stableSourceKeyLength)
        where TEntity : Entity
    {
        builder.ToTable(table, "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property("ManagedServerId").IsRequired();
        builder.Property("StableSourceKey")
            .HasMaxLength(stableSourceKeyLength)
            .IsRequired();
        builder.HasOne<ManagedServer>()
            .WithMany()
            .HasForeignKey("ManagedServerId")
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName($"FK_{table}_ManagedServer_ManagedServerId");
        builder.HasIndex("ManagedServerId", "StableSourceKey")
            .IsUnique()
            .HasDatabaseName($"UX_{table}_ManagedServer_StableSourceKey");
    }

    internal static void CapturedAt<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class =>
        builder.Property("CapturedAt").HasColumnType("datetime2(3)").IsRequired();
}

public sealed class WindowsComputerInventoryConfiguration
    : IEntityTypeConfiguration<WindowsComputerInventory>
{
    public void Configure(EntityTypeBuilder<WindowsComputerInventory> builder)
    {
        WindowsInventoryConfiguration.Singular(builder, "WindowsComputerInventory");
        builder.Ignore(entity => entity.ManagedServerId);
        builder.Property(entity => entity.ComputerName).HasMaxLength(255);
        builder.Property(entity => entity.Fqdn).HasMaxLength(255);
        builder.Property(entity => entity.DomainName).HasMaxLength(255);
        builder.Property(entity => entity.Manufacturer).HasMaxLength(255);
        builder.Property(entity => entity.Model).HasMaxLength(255);
        builder.Property(entity => entity.SerialNumber).HasMaxLength(255);
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsOperatingSystemInventoryConfiguration
    : IEntityTypeConfiguration<WindowsOperatingSystemInventory>
{
    public void Configure(EntityTypeBuilder<WindowsOperatingSystemInventory> builder)
    {
        WindowsInventoryConfiguration.Singular(builder, "WindowsOperatingSystemInventory");
        builder.Ignore(entity => entity.ManagedServerId);
        builder.Property(entity => entity.Caption).HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.BuildNumber).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Edition).HasMaxLength(100);
        builder.Property(entity => entity.Architecture).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.InstallDate).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.LastBootTime).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.TimeZoneId).HasMaxLength(100);
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsMemoryInventoryConfiguration
    : IEntityTypeConfiguration<WindowsMemoryInventory>
{
    public void Configure(EntityTypeBuilder<WindowsMemoryInventory> builder)
    {
        WindowsInventoryConfiguration.Singular(builder, "WindowsMemoryInventory");
        builder.Ignore(entity => entity.ManagedServerId);
        builder.Property(entity => entity.TotalPhysicalMemoryBytes).IsRequired();
        builder.ToTable(
            "WindowsMemoryInventory",
            "inventory",
            table => table.HasCheckConstraint(
                "CK_WindowsMemoryInventory_TotalPhysicalMemoryBytes_NonNegative",
                "[TotalPhysicalMemoryBytes] >= 0"));
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsProcessorInventoryConfiguration
    : IEntityTypeConfiguration<WindowsProcessorInventory>
{
    public void Configure(EntityTypeBuilder<WindowsProcessorInventory> builder)
    {
        WindowsInventoryConfiguration.Collection(builder, "WindowsProcessorInventory", 200);
        builder.Property(entity => entity.Name).HasMaxLength(255);
        builder.Property(entity => entity.Manufacturer).HasMaxLength(255);
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsDiskInventoryConfiguration
    : IEntityTypeConfiguration<WindowsDiskInventory>
{
    public void Configure(EntityTypeBuilder<WindowsDiskInventory> builder)
    {
        WindowsInventoryConfiguration.Collection(builder, "WindowsDiskInventory", 260);
        builder.Property(entity => entity.FriendlyName).HasMaxLength(255);
        builder.Property(entity => entity.SerialNumber).HasMaxLength(255);
        builder.Property(entity => entity.BusType).HasMaxLength(100);
        builder.Property(entity => entity.PartitionStyle).HasMaxLength(50);
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsVolumeInventoryConfiguration
    : IEntityTypeConfiguration<WindowsVolumeInventory>
{
    public void Configure(EntityTypeBuilder<WindowsVolumeInventory> builder)
    {
        WindowsInventoryConfiguration.Collection(builder, "WindowsVolumeInventory", 260);
        builder.Property(entity => entity.DriveLetter).HasMaxLength(10);
        builder.Property(entity => entity.Label).HasMaxLength(255);
        builder.Property(entity => entity.FileSystem).HasMaxLength(50);
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsNetworkAdapterInventoryConfiguration
    : IEntityTypeConfiguration<WindowsNetworkAdapterInventory>
{
    public void Configure(EntityTypeBuilder<WindowsNetworkAdapterInventory> builder)
    {
        WindowsInventoryConfiguration.Collection(builder, "WindowsNetworkAdapterInventory", 200);
        builder.Property(entity => entity.Name).HasMaxLength(255);
        builder.Property(entity => entity.InterfaceDescription).HasMaxLength(500);
        builder.Property(entity => entity.MacAddress).HasMaxLength(20);
        builder.Property(entity => entity.OperationalStatus).HasMaxLength(50);
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsIpv4AddressInventoryConfiguration
    : IEntityTypeConfiguration<WindowsIpv4AddressInventory>
{
    public void Configure(EntityTypeBuilder<WindowsIpv4AddressInventory> builder)
    {
        WindowsInventoryConfiguration.Collection(builder, "WindowsIpv4AddressInventory", 300);
        builder.Property(entity => entity.NetworkAdapterInventoryId).IsRequired();
        builder.Property(entity => entity.Address).HasMaxLength(15).IsRequired();
        builder.Property(entity => entity.PrefixLength).IsRequired();
        builder.HasOne<WindowsNetworkAdapterInventory>()
            .WithMany()
            .HasForeignKey(entity => entity.NetworkAdapterInventoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId");
        builder.HasIndex(entity => entity.NetworkAdapterInventoryId)
            .HasDatabaseName("IX_WindowsIpv4AddressInventory_NetworkAdapterInventoryId");
        builder.ToTable(
            "WindowsIpv4AddressInventory",
            "inventory",
            table => table.HasCheckConstraint(
                "CK_WindowsIpv4AddressInventory_PrefixLength_Range",
                "[PrefixLength] >= 0 AND [PrefixLength] <= 32"));
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}
