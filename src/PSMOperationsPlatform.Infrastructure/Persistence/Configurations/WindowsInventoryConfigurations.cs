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
        where TEntity : class
    {
        builder.Property("CapturedAt").HasColumnType("datetime2(3)").IsRequired();
        InventoryRun(builder);
    }

    internal static void InventoryRun<TEntity>(EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property("InventoryRunId").IsRequired();
        string ownerProperty = builder.Metadata.FindProperty("ManagedServerId") is null
            ? "Id"
            : "ManagedServerId";
        builder.HasIndex(ownerProperty, "InventoryRunId")
            .HasDatabaseName($"IX_{builder.Metadata.GetTableName()}_ManagedServer_InventoryRun");
    }
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
        builder.Property(entity => entity.SystemType).HasMaxLength(100);
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
        builder.Property(entity => entity.InstallationType).HasMaxLength(100);
        builder.Property(entity => entity.SystemDrive).HasMaxLength(10);
        builder.Property(entity => entity.WindowsDirectory).HasMaxLength(260);
        builder.Property(entity => entity.Locale).HasMaxLength(20);
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsBiosInventoryConfiguration
    : IEntityTypeConfiguration<WindowsBiosInventory>
{
    public void Configure(EntityTypeBuilder<WindowsBiosInventory> builder)
    {
        WindowsInventoryConfiguration.Singular(builder, "WindowsBiosInventory");
        builder.Ignore(entity => entity.ManagedServerId);
        builder.Property(entity => entity.Manufacturer).HasMaxLength(255);
        builder.Property(entity => entity.SmbiosBiosVersion).HasMaxLength(255);
        builder.Property(entity => entity.Version).HasMaxLength(255);
        builder.Property(entity => entity.ReleaseDate).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.SerialNumber).HasMaxLength(255);
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsMemoryInventoryConfiguration
    : IEntityTypeConfiguration<WindowsMemoryInventory>
{
    public void Configure(EntityTypeBuilder<WindowsMemoryInventory> builder)
    {
        builder.ToTable("WindowsMemoryInventory", "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.ModuleKey).HasMaxLength(200).IsRequired();
        builder.HasOne<ManagedServer>()
            .WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WindowsMemoryInventory_ManagedServer_ManagedServerId");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.ModuleKey })
            .IsUnique()
            .HasDatabaseName("UX_WindowsMemoryInventory_ManagedServer_ModuleKey");
        builder.Property(entity => entity.DeviceLocator).HasMaxLength(255);
        builder.Property(entity => entity.BankLabel).HasMaxLength(255);
        builder.Property(entity => entity.CapacityBytes).IsRequired();
        builder.Property(entity => entity.Manufacturer).HasMaxLength(255);
        builder.Property(entity => entity.PartNumber).HasMaxLength(255);
        builder.Property(entity => entity.SerialNumber).HasMaxLength(255);
        builder.Property(entity => entity.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .IsRequired();
        builder.ToTable(
            "WindowsMemoryInventory",
            "inventory",
            table => table.HasCheckConstraint(
                "CK_WindowsMemoryInventory_CapacityBytes_Positive",
                "[CapacityBytes] > 0"));
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsProcessorInventoryConfiguration
    : IEntityTypeConfiguration<WindowsProcessorInventory>
{
    public void Configure(EntityTypeBuilder<WindowsProcessorInventory> builder)
    {
        builder.ToTable("WindowsProcessorInventory", "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.ProcessorKey).HasMaxLength(200).IsRequired();
        builder.HasOne<ManagedServer>()
            .WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WindowsProcessorInventory_ManagedServer_ManagedServerId");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.ProcessorKey })
            .IsUnique()
            .HasDatabaseName("UX_WindowsProcessorInventory_ManagedServer_ProcessorKey");
        builder.Property(entity => entity.DeviceId).HasMaxLength(100);
        builder.Property(entity => entity.Name).HasMaxLength(255);
        builder.Property(entity => entity.Manufacturer).HasMaxLength(255);
        builder.Property(entity => entity.Description).HasMaxLength(255);
        builder.Property(entity => entity.SocketDesignation).HasMaxLength(255);
        builder.Property(entity => entity.ProcessorId).HasMaxLength(100);
        builder.Property(entity => entity.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .IsRequired();
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsDiskInventoryConfiguration
    : IEntityTypeConfiguration<WindowsDiskInventory>
{
    public void Configure(EntityTypeBuilder<WindowsDiskInventory> builder)
    {
        builder.ToTable("WindowsDiskInventory", "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.DiskKey).HasMaxLength(260).IsRequired();
        builder.HasOne<ManagedServer>().WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WindowsDiskInventory_ManagedServer_ManagedServerId");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.DiskKey })
            .IsUnique()
            .HasDatabaseName("UX_WindowsDiskInventory_ManagedServer_DiskKey");
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
        builder.ToTable("WindowsVolumeInventory", "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.VolumeKey).HasMaxLength(260).IsRequired();
        builder.HasOne<ManagedServer>().WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WindowsVolumeInventory_ManagedServer_ManagedServerId");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.VolumeKey })
            .IsUnique()
            .HasDatabaseName("UX_WindowsVolumeInventory_ManagedServer_VolumeKey");
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
        builder.ToTable("WindowsNetworkAdapterInventory", "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.AdapterKey).HasMaxLength(200).IsRequired();
        builder.HasOne<ManagedServer>().WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WindowsNetworkAdapterInventory_ManagedServer_ManagedServerId");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.AdapterKey })
            .IsUnique()
            .HasDatabaseName("UX_WindowsNetworkAdapterInventory_ManagedServer_AdapterKey");
        builder.Property(entity => entity.Name).HasMaxLength(255);
        builder.Property(entity => entity.InterfaceDescription).HasMaxLength(500);
        builder.Property(entity => entity.MacAddress).HasMaxLength(20);
        builder.Property(entity => entity.OperationalStatus).HasMaxLength(50);
        builder.Property(entity => entity.InterfaceGuid).HasMaxLength(36);
        builder.Property(entity => entity.FriendlyName).HasMaxLength(255);
        builder.Property(entity => entity.Manufacturer).HasMaxLength(255);
        builder.Property(entity => entity.PnpDeviceId).HasMaxLength(500);
        builder.Property(entity => entity.RowVersion).IsRowVersion()
            .IsConcurrencyToken().IsRequired();
        builder.HasAlternateKey(entity => new { entity.Id, entity.ManagedServerId })
            .HasName("AK_WindowsNetworkAdapterInventory_Id_ManagedServerId");
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsIpv4AddressInventoryConfiguration
    : IEntityTypeConfiguration<WindowsIpv4AddressInventory>
{
    public void Configure(EntityTypeBuilder<WindowsIpv4AddressInventory> builder)
    {
        builder.ToTable("WindowsIpv4AddressInventory", "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.Ipv4Key).HasMaxLength(300).IsRequired();
        builder.HasOne<ManagedServer>().WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_WindowsIpv4AddressInventory_ManagedServer_ManagedServerId");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.Ipv4Key })
            .IsUnique()
            .HasDatabaseName("UX_WindowsIpv4AddressInventory_ManagedServer_Ipv4Key");
        builder.Property(entity => entity.NetworkAdapterInventoryId).IsRequired();
        builder.Property(entity => entity.AdapterKey).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Address).HasMaxLength(15).IsRequired();
        builder.Property(entity => entity.DefaultGateway).HasMaxLength(15);
        builder.Property(entity => entity.PrefixLength).IsRequired();
        builder.Property(entity => entity.RowVersion).IsRowVersion()
            .IsConcurrencyToken().IsRequired();
        builder.HasOne<WindowsNetworkAdapterInventory>()
            .WithMany()
            .HasForeignKey(entity => new
            {
                entity.NetworkAdapterInventoryId,
                entity.ManagedServerId,
            })
            .HasPrincipalKey(adapter => new
            {
                adapter.Id,
                adapter.ManagedServerId,
            })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "FK_WindowsIpv4AddressInventory_WindowsNetworkAdapterInventory_NetworkAdapterInventoryId_ManagedServerId");
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
