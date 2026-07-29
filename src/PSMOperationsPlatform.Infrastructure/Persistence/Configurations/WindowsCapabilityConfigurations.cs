using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class WindowsCapabilitySnapshotConfiguration : IEntityTypeConfiguration<WindowsCapabilitySnapshot>
{
    public void Configure(EntityTypeBuilder<WindowsCapabilitySnapshot> builder)
    {
        builder.ToTable("WindowsCapabilitySnapshot", "inventory", table =>
        {
            table.HasCheckConstraint("CK_WindowsCapabilitySnapshot_SourceInventoryVersion", "[SourceInventoryVersion] > 0");
            table.HasCheckConstraint("CK_WindowsCapabilitySnapshot_SchemaVersion", "[CapabilitySchemaVersion] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EvaluationStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.EvaluatedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired();
        builder.HasOne<ManagedServer>().WithMany().HasForeignKey(x => x.ManagedServerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ManagedServerId).IsUnique().HasDatabaseName("UX_WindowsCapabilitySnapshot_ManagedServer");
        builder.HasIndex(x => new { x.ManagedServerId, x.InventoryRunId, x.SourceInventoryVersion })
            .HasDatabaseName("IX_WindowsCapabilitySnapshot_SourceInventory");
        builder.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WindowsCapabilityEntryConfiguration : IEntityTypeConfiguration<WindowsCapabilityEntry>
{
    public void Configure(EntityTypeBuilder<WindowsCapabilityEntry> builder)
    {
        builder.ToTable("WindowsCapabilityEntry", "inventory", table =>
            table.HasCheckConstraint("CK_WindowsCapabilityEntry_RuleVersion", "[RuleVersion] > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CapabilityCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SupportStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReadinessStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RuleVersion).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired();
        builder.HasMany(x => x.Provenance).WithOne().HasForeignKey(x => x.CapabilityEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SnapshotId, x.CapabilityCode }).IsUnique()
            .HasDatabaseName("UX_WindowsCapabilityEntry_Snapshot_CapabilityCode");
    }
}

public sealed class WindowsCapabilityProvenanceConfiguration : IEntityTypeConfiguration<WindowsCapabilityProvenance>
{
    public void Configure(EntityTypeBuilder<WindowsCapabilityProvenance> builder)
    {
        builder.ToTable("WindowsCapabilityProvenance", "inventory", table =>
            table.HasCheckConstraint("CK_WindowsCapabilityProvenance_InventoryVersion", "[InventoryVersion] > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ModuleName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FactCategory).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FactKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.CapabilityEntryId, x.FactCategory, x.FactKey }).IsUnique();
    }
}
