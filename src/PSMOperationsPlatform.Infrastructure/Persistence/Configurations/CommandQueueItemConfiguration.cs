using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class CommandQueueItemConfiguration : IEntityTypeConfiguration<CommandQueueItem>
{
    public void Configure(EntityTypeBuilder<CommandQueueItem> builder)
    {
        builder.ToTable(
            "CommandQueueItem",
            "operations",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_CommandQueueItem_PayloadJson_IsJson",
                    "ISJSON([PayloadJson]) = 1");
                table.HasCheckConstraint(
                    "CK_CommandQueueItem_Priority_NonNegative",
                    "[Priority] >= 0");
            });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();
        builder.Property(entity => entity.CommandType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.TargetCollectorType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
        builder.Property(entity => entity.ManagedServerId);
        builder.Property(entity => entity.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(entity => entity.Priority).IsRequired();
        builder.Property(entity => entity.NotBefore).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.CreatedAt).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(entity => entity.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(entity => entity.CompletedAt).HasColumnType("datetime2(3)");
        builder.Property(entity => entity.FailureCode).HasMaxLength(100);
        builder.Property(entity => entity.FailureMessage).HasMaxLength(2000);
        builder.Property(entity => entity.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasOne<ManagedServer>()
            .WithMany()
            .HasForeignKey(entity => entity.ManagedServerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CommandQueueItem_ManagedServer_ManagedServerId");
        builder.HasIndex(entity => new
        {
            entity.Status,
            entity.TargetCollectorType,
            entity.Priority,
            entity.NotBefore,
            entity.CreatedAt
        })
            .HasDatabaseName("IX_CommandQueue_Status_Target_Priority");
    }
}
