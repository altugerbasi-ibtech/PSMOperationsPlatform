using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class InventorySnapshotConfiguration : IEntityTypeConfiguration<InventorySnapshot>
{
    public void Configure(EntityTypeBuilder<InventorySnapshot> builder)
    {
        builder.ToTable(
            "InventorySnapshot",
            "inventory",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_InventorySnapshot_PayloadJson_IsJson",
                    "ISJSON([PayloadJson]) = 1");
                table.HasCheckConstraint(
                    "CK_InventorySnapshot_SchemaVersion_Positive",
                    "[SchemaVersion] > 0");
            });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.CollectorRunId).IsRequired();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.SnapshotType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.SchemaVersion).IsRequired();
        builder.Property(entity => entity.CapturedAt).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(entity => entity.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(entity => entity.PayloadHash)
            .HasColumnType("char(64)")
            .HasMaxLength(64);
        builder.HasOne<CollectorRun>()
            .WithMany()
            .HasForeignKey(entity => entity.CollectorRunId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventorySnapshot_CollectorRun_CollectorRunId");
        builder.HasOne<ManagedServer>()
            .WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_InventorySnapshot_ManagedServer_ManagedServerId");
        builder.HasIndex(entity => new
        {
            entity.CollectorRunId,
            entity.SnapshotType,
            entity.SchemaVersion
        })
            .IsUnique()
            .HasDatabaseName("UX_InventorySnapshot_RunContract");
        builder.HasIndex(entity => new
        {
            entity.ManagedServerId,
            entity.SnapshotType,
            entity.CapturedAt
        })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_InventorySnapshot_Server_Type_CapturedAt");
    }
}
