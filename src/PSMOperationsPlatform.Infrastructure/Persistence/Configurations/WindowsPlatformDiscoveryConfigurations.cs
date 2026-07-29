using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class WindowsRoleInventoryConfiguration
    : IEntityTypeConfiguration<WindowsRoleInventory>
{
    public void Configure(EntityTypeBuilder<WindowsRoleInventory> builder)
    {
        ConfigureBase(builder, "WindowsRoleInventory", entity => entity.RoleKey, 260,
            "UX_WindowsRoleInventory_ManagedServer_RoleKey");
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(255);
        builder.Property(entity => entity.Parent).HasMaxLength(200);
        builder.Property(entity => entity.FeatureType).HasMaxLength(50);
    }

    private static void ConfigureBase(
        EntityTypeBuilder<WindowsRoleInventory> builder, string table,
        System.Linq.Expressions.Expression<Func<WindowsRoleInventory, string>> key,
        int keyLength, string index)
    {
        builder.ToTable(table, "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(key).HasMaxLength(keyLength).IsRequired();
        builder.HasOne<ManagedServer>().WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.RoleKey })
            .IsUnique().HasDatabaseName(index);
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.InventoryRunId })
            .HasDatabaseName("IX_WindowsRoleInventory_ManagedServer_InventoryRun");
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsRequired();
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsFeatureInventoryConfiguration
    : IEntityTypeConfiguration<WindowsFeatureInventory>
{
    public void Configure(EntityTypeBuilder<WindowsFeatureInventory> builder)
    {
        builder.ToTable("WindowsFeatureInventory", "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.FeatureKey).HasMaxLength(260).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(255);
        builder.Property(entity => entity.Parent).HasMaxLength(200);
        builder.Property(entity => entity.RestartRequired).HasMaxLength(50);
        builder.Property(entity => entity.FeatureType).HasMaxLength(50);
        builder.HasOne<ManagedServer>().WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.FeatureKey })
            .IsUnique()
            .HasDatabaseName("UX_WindowsFeatureInventory_ManagedServer_FeatureKey");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.InventoryRunId })
            .HasDatabaseName("IX_WindowsFeatureInventory_ManagedServer_InventoryRun");
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsRequired();
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsIisPlatformInventoryConfiguration
    : IEntityTypeConfiguration<WindowsIisPlatformInventory>
{
    public void Configure(EntityTypeBuilder<WindowsIisPlatformInventory> builder)
    {
        builder.ToTable("WindowsIisPlatformInventory", "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.IisKey).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(100);
        builder.HasOne<ManagedServer>().WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.IisKey })
            .IsUnique()
            .HasDatabaseName("UX_WindowsIisPlatformInventory_ManagedServer_IisKey");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.InventoryRunId })
            .HasDatabaseName("IX_WindowsIisPlatformInventory_ManagedServer_InventoryRun");
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsRequired();
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsDotNetPlatformInventoryConfiguration
    : IEntityTypeConfiguration<WindowsDotNetPlatformInventory>
{
    public void Configure(EntityTypeBuilder<WindowsDotNetPlatformInventory> builder)
    {
        builder.ToTable("WindowsDotNetPlatformInventory", "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.DotNetKey).HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.Category).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(100);
        builder.HasOne<ManagedServer>().WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.DotNetKey })
            .IsUnique()
            .HasDatabaseName("UX_WindowsDotNetPlatformInventory_ManagedServer_DotNetKey");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.InventoryRunId })
            .HasDatabaseName("IX_WindowsDotNetPlatformInventory_ManagedServer_InventoryRun");
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsRequired();
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}

public sealed class WindowsPowerShellPlatformInventoryConfiguration
    : IEntityTypeConfiguration<WindowsPowerShellPlatformInventory>
{
    public void Configure(EntityTypeBuilder<WindowsPowerShellPlatformInventory> builder)
    {
        builder.ToTable("WindowsPowerShellPlatformInventory", "inventory");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.PowerShellKey).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Edition).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(100);
        builder.Property(entity => entity.Path).HasMaxLength(500).IsRequired();
        builder.HasOne<ManagedServer>().WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.PowerShellKey })
            .IsUnique()
            .HasDatabaseName("UX_WindowsPowerShellPlatformInventory_ManagedServer_PowerShellKey");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.InventoryRunId })
            .HasDatabaseName("IX_WindowsPowerShellPlatformInventory_ManagedServer_InventoryRun");
        builder.Property(entity => entity.RowVersion).IsRowVersion().IsRequired();
        WindowsInventoryConfiguration.CapturedAt(builder);
    }
}
