using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class ManagedServerConfiguration : IEntityTypeConfiguration<ManagedServer>
{
    public void Configure(EntityTypeBuilder<ManagedServer> builder)
    {
        builder.ToTable("ManagedServer", "configuration");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.Fqdn).HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.DisplayName).HasMaxLength(255);
        builder.Property(entity => entity.Environment).HasMaxLength(50);
        builder.Property(entity => entity.IsEnabled).IsRequired();
        builder.Property(entity => entity.CreatedAt).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(entity => entity.UpdatedAt).HasColumnType("datetime2(3)").IsRequired();
        builder.HasIndex(entity => entity.Fqdn)
            .IsUnique()
            .HasDatabaseName("UX_ManagedServer_Fqdn");
    }
}
