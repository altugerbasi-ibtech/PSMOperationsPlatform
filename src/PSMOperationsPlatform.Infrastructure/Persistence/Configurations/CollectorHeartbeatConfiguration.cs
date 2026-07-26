using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class CollectorHeartbeatConfiguration : IEntityTypeConfiguration<CollectorHeartbeat>
{
    public void Configure(EntityTypeBuilder<CollectorHeartbeat> builder)
    {
        builder.ToTable("CollectorHeartbeat", "monitoring");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.CollectorNodeId).IsRequired();
        builder.Property(entity => entity.ObservedAt).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(entity => entity.Message).HasMaxLength(1000);
        builder.Property(entity => entity.ProcessId);
        builder.Property(entity => entity.WorkingSetBytes);
        builder.HasOne<CollectorNode>()
            .WithMany()
            .HasForeignKey(entity => entity.CollectorNodeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CollectorHeartbeat_CollectorNode_CollectorNodeId");
        builder.HasIndex(entity => new { entity.CollectorNodeId, entity.ObservedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_CollectorHeartbeat_Collector_ObservedAt");
    }
}
