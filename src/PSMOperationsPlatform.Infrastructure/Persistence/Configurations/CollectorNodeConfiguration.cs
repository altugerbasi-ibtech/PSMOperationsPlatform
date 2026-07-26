using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class CollectorNodeConfiguration : IEntityTypeConfiguration<CollectorNode>
{
    public void Configure(EntityTypeBuilder<CollectorNode> builder)
    {
        builder.ToTable("CollectorNode", "collection");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CollectorType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(entity => entity.HostFqdn).HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.InstanceKey).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(50);
        builder.Property(entity => entity.IsEnabled).IsRequired();
        builder.Property(entity => entity.RegisteredAt).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(entity => entity.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(entity => new
        {
            entity.HostFqdn,
            entity.CollectorType,
            entity.InstanceKey
        })
            .IsUnique()
            .HasDatabaseName("UX_CollectorNode_Registration");
    }
}
