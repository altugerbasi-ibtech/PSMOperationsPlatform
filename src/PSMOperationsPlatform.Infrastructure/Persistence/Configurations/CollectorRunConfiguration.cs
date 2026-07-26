using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class CollectorRunConfiguration : IEntityTypeConfiguration<CollectorRun>
{
    public void Configure(EntityTypeBuilder<CollectorRun> builder)
    {
        builder.ToTable("CollectorRun", "collection");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.CollectorNodeId).IsRequired();
        builder.Property(entity => entity.ManagedServerId).IsRequired();
        builder.Property(entity => entity.CollectionType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(entity => entity.StartedAt).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.CompletedAt).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.ErrorCode).HasMaxLength(100);
        builder.Property(entity => entity.ErrorMessage).HasMaxLength(2000);
        builder.Property(entity => entity.CreatedAt).HasColumnType("datetime2(3)").IsRequired();
        builder.HasOne<CollectorNode>()
            .WithMany()
            .HasForeignKey(entity => entity.CollectorNodeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CollectorRun_CollectorNode_CollectorNodeId");
        builder.HasOne<ManagedServer>()
            .WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CollectorRun_ManagedServer_ManagedServerId");
        builder.HasIndex(entity => new { entity.ManagedServerId, entity.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_CollectorRun_Server_CreatedAt");
        builder.HasIndex(entity => new
        {
            entity.CollectorNodeId,
            entity.Status,
            entity.CreatedAt
        })
            .HasDatabaseName("IX_CollectorRun_Collector_Status_CreatedAt");
    }
}
