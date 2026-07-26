using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable(
            "AuditLog",
            "audit",
            table => table.HasCheckConstraint(
                "CK_AuditLog_DetailJson_IsJson",
                "[DetailJson] IS NULL OR ISJSON([DetailJson]) = 1"));
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.OccurredAt).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(entity => entity.Actor).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.Action).HasMaxLength(150).IsRequired();
        builder.Property(entity => entity.EntityType).HasMaxLength(100);
        builder.Property(entity => entity.EntityId);
        builder.Property(entity => entity.CorrelationId);
        builder.Property(entity => entity.Outcome)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(entity => entity.DetailJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(entity => entity.OccurredAt)
            .IsDescending()
            .HasDatabaseName("IX_AuditLog_OccurredAt");
        builder.HasIndex(entity => new
        {
            entity.EntityType,
            entity.EntityId,
            entity.OccurredAt
        })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_AuditLog_Entity");
        builder.HasIndex(entity => entity.CorrelationId)
            .HasDatabaseName("IX_AuditLog_CorrelationId");
    }
}
