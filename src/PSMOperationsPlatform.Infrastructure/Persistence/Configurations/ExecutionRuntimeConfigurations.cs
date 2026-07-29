using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class ExecutionRunStateEntityConfiguration : IEntityTypeConfiguration<ExecutionRunStateEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionRunStateEntity> builder)
    {
        builder.ToTable("ExecutionRunState", "runtime", table =>
        {
            table.HasCheckConstraint("CK_ExecutionRunState_Versions",
                "[ExecutionPlanSchemaVersion] > 0 AND [ExecutionStateSchemaVersion] > 0 AND [SourceInventoryVersion] > 0");
            table.HasCheckConstraint("CK_ExecutionRunState_Metrics",
                "[TotalDurationTicks] >= 0 AND [StepCount] >= 0 AND [AttemptCount] >= 0 AND [RetryCount] >= 0 AND [BytesCollected] >= 0 AND [ObjectsCollected] >= 0");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RuntimeVersion).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.QueuedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.StartedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.CompletedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.CancelledAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.FailureCategory).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(100);
        builder.Property(x => x.FailureSummary).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired();
        builder.HasOne<ExecutionPlan>().WithMany().HasForeignKey(x => x.ExecutionPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ExecutionPlanId).IsUnique();
        builder.HasIndex(x => new { x.ManagedServerId, x.Status });
        builder.HasIndex(x => new { x.ManagedServerId, x.SourceInventoryRunId });
        builder.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.ExecutionRunId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExecutionStepStateEntityConfiguration : IEntityTypeConfiguration<ExecutionStepStateEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionStepStateEntity> builder)
    {
        builder.ToTable("ExecutionStepState", "runtime", table =>
        {
            table.HasCheckConstraint("CK_ExecutionStepState_Versions",
                "[StrategyVersion] > 0 AND [QueueSequence] > 0 AND ([PluginVersion] IS NULL OR [PluginVersion] > 0)");
            table.HasCheckConstraint("CK_ExecutionStepState_Metrics",
                "[QueueDurationTicks] >= 0 AND [WaitDurationTicks] >= 0 AND [ExecutionDurationTicks] >= 0 AND [TotalDurationTicks] >= 0 AND [AttemptCount] >= 0 AND [RetryCount] >= 0 AND [BytesCollected] >= 0 AND [ObjectsCollected] >= 0");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.StrategyCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.QueuedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.EligibleAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.StartedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.CompletedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.CancelledAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.TimedOutAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.StartedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.CompletedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.FailureCategory).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(100);
        builder.Property(x => x.FailureSummary).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired();
        builder.HasIndex(x => new { x.ExecutionRunId, x.ExecutionPlanStepId }).IsUnique();
        builder.HasIndex(x => new { x.ExecutionRunId, x.StrategyCode }).IsUnique();
        builder.HasMany(x => x.Attempts).WithOne().HasForeignKey(x => x.ExecutionStepStateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExecutionAttemptStateEntityConfiguration : IEntityTypeConfiguration<ExecutionAttemptStateEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionAttemptStateEntity> builder)
    {
        builder.ToTable("ExecutionAttemptState", "runtime", table =>
        {
            table.HasCheckConstraint("CK_ExecutionAttemptState_Number", "[AttemptNumber] > 0");
            table.HasCheckConstraint("CK_ExecutionAttemptState_Metrics",
                "[DurationTicks] >= 0 AND ([BytesCollected] IS NULL OR [BytesCollected] >= 0) AND ([ObjectsCollected] IS NULL OR [ObjectsCollected] >= 0)");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.StartedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.CompletedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.FailureCategory).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(100);
        builder.Property(x => x.FailureSummary).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired();
        builder.HasIndex(x => new { x.ExecutionStepStateId, x.AttemptNumber }).IsUnique();
    }
}
