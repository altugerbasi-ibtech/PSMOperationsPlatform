using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class ExecutionRunHistoryEntityConfiguration
    : IEntityTypeConfiguration<ExecutionRunHistoryEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionRunHistoryEntity> b)
    {
        b.ToTable("ExecutionRunHistory", "history", t =>
        {
            t.HasCheckConstraint("CK_ExecutionRunHistory_Versions",
                "[HistorySchemaVersion]>0 AND [StrategyVersion]>0 AND [PluginVersion]>0 AND [ExecutionPlanSchemaVersion]>0 AND [ExecutionStateSchemaVersion]>0 AND [ExecutionEventSchemaVersion]>0 AND [ExecutionMonitoringSchemaVersion]>0 AND [SourceInventoryVersion]>0");
            t.HasCheckConstraint("CK_ExecutionRunHistory_Counts",
                "[DurationTicks]>=0 AND [WarningCount]>=0 AND [AttemptCount]>=0 AND [RetryCount]>=0 AND [StepCount]>=0 AND [CompletedStepCount]>=0 AND [FailedStepCount]>=0 AND [TimedOutStepCount]>=0 AND [CancelledStepCount]>=0 AND [SkippedStepCount]>=0 AND [ArtifactFileCount]>=0 AND [ArtifactObjectCount]>=0 AND [ArtifactMetricCount]>=0 AND [ArtifactByteCount]>=0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => x.ExecutionRunId).IsUnique();
        b.HasIndex(x => x.CompletedAt);
        b.HasIndex(x => new { x.ManagedServerId, x.CompletedAt });
        b.HasIndex(x => new { x.StrategyCode, x.CompletedAt });
        b.HasIndex(x => new { x.PluginId, x.CompletedAt });
        b.HasIndex(x => new { x.ExecutionOutcome, x.CompletedAt });
        b.HasIndex(x => new { x.FailureCategory, x.CompletedAt });
        b.Property(x => x.QueuedAt).HasColumnType("datetime2(3)");
        b.Property(x => x.StartedAt).HasColumnType("datetime2(3)");
        b.Property(x => x.CompletedAt).HasColumnType("datetime2(3)");
        b.Property(x => x.RecordedAt).HasColumnType("datetime2(3)");
        Text(b, x => x.ExecutionOutcome, 40, true);
        Text(b, x => x.TerminalState, 40, true);
        Text(b, x => x.FailureCategory, 60, true);
        Text(b, x => x.ReasonCode, 100, false);
        Text(b, x => x.StrategyCode, 100, true);
        Text(b, x => x.PluginId, 100, true);
        Text(b, x => x.TargetSdkVersion, 20, true);
        Text(b, x => x.RuntimeContractVersion, 20, true);
        Text(b, x => x.Subject, 60, true);
        Text(b, x => x.ProjectionStatus, 20, true);
        Text(b, x => x.ProjectionFailureCategory, 60, true);
        Text(b, x => x.ProjectionReasonCode, 100, true);
    }

    private static void Text<T>(EntityTypeBuilder<ExecutionRunHistoryEntity> b,
        System.Linq.Expressions.Expression<Func<ExecutionRunHistoryEntity, T>> property,
        int length, bool required)
    {
        PropertyBuilder<T> p = b.Property(property).HasMaxLength(length);
        if (required) p.IsRequired();
    }
}

public sealed class ExecutionStepHistoryEntityConfiguration
    : IEntityTypeConfiguration<ExecutionStepHistoryEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionStepHistoryEntity> b)
    {
        b.ToTable("ExecutionStepHistory", "history", t =>
        {
            t.HasCheckConstraint("CK_ExecutionStepHistory_Versions",
                "[HistorySchemaVersion]>0 AND [StepOrdinal]>0 AND [StrategyVersion]>0 AND [PluginVersion]>0");
            t.HasCheckConstraint("CK_ExecutionStepHistory_Counts",
                "[DependencyCount]>=0 AND [QueueDurationTicks]>=0 AND [WaitDurationTicks]>=0 AND [ExecutionDurationTicks]>=0 AND [AttemptCount]>=0 AND [RetryCount]>=0 AND [ArtifactFileCount]>=0 AND [ArtifactObjectCount]>=0 AND [ArtifactMetricCount]>=0 AND [ArtifactByteCount]>=0 AND [WarningCount]>=0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.ExecutionRunId, x.StepOrdinal }).IsUnique();
        b.HasIndex(x => new { x.ExecutionRunId, x.ExecutionStepId }).IsUnique();
        Required(b.Property(x => x.StrategyCode), 100);
        Required(b.Property(x => x.PluginId), 100);
        Required(b.Property(x => x.Subject), 60);
        Required(b.Property(x => x.StepOutcome), 40);
        Required(b.Property(x => x.FailureCategory), 60);
        b.Property(x => x.ReasonCode).HasMaxLength(100);
        Dates(b.Property(x => x.QueuedAt), b.Property(x => x.StartedAt),
            b.Property(x => x.CompletedAt));
        b.HasOne<ExecutionRunHistoryEntity>().WithMany()
            .HasForeignKey(x => x.ExecutionRunId).HasPrincipalKey(x => x.ExecutionRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
    private static void Required(PropertyBuilder<string> p, int max) =>
        p.HasMaxLength(max).IsRequired();
    private static void Dates(params PropertyBuilder[] values)
    {
        foreach (PropertyBuilder value in values) value.HasColumnType("datetime2(3)");
    }
}

public sealed class ExecutionAttemptHistoryEntityConfiguration
    : IEntityTypeConfiguration<ExecutionAttemptHistoryEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionAttemptHistoryEntity> b)
    {
        b.ToTable("ExecutionAttemptHistory", "history", t =>
        {
            t.HasCheckConstraint("CK_ExecutionAttemptHistory_Versions",
                "[HistorySchemaVersion]>0 AND [AttemptNumber]>0");
            t.HasCheckConstraint("CK_ExecutionAttemptHistory_Counts",
                "[DurationTicks]>=0 AND ([RetryDelayTicks] IS NULL OR [RetryDelayTicks]>=0) AND [WarningCount]>=0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.ExecutionRunId, x.ExecutionStepId, x.AttemptNumber }).IsUnique();
        b.Property(x => x.AttemptOutcome).HasMaxLength(40).IsRequired();
        b.Property(x => x.FailureCategory).HasMaxLength(60).IsRequired();
        b.Property(x => x.ReasonCode).HasMaxLength(100);
        b.Property(x => x.StartedAt).HasColumnType("datetime2(3)");
        b.Property(x => x.CompletedAt).HasColumnType("datetime2(3)");
        b.HasOne<ExecutionRunHistoryEntity>().WithMany()
            .HasForeignKey(x => x.ExecutionRunId).HasPrincipalKey(x => x.ExecutionRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExecutionStateTransitionHistoryEntityConfiguration
    : IEntityTypeConfiguration<ExecutionStateTransitionHistoryEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionStateTransitionHistoryEntity> b)
    {
        b.ToTable("ExecutionStateTransitionHistory", "history", t =>
            t.HasCheckConstraint("CK_ExecutionStateTransitionHistory_Versions",
                "[HistorySchemaVersion]>0 AND [TransitionSequence]>0 AND [EventSchemaVersion]>0"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.ExecutionRunId, x.TransitionSequence }).IsUnique();
        b.HasIndex(x => new { x.ExecutionRunId, x.EntityType, x.ExecutionStepId, x.TransitionSequence }).IsUnique();
        b.Property(x => x.EntityType).HasMaxLength(20).IsRequired();
        b.Property(x => x.FromState).HasMaxLength(40);
        b.Property(x => x.ToState).HasMaxLength(40).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(80).IsRequired();
        b.Property(x => x.ReasonCode).HasMaxLength(100).IsRequired();
        b.Property(x => x.FailureCategory).HasMaxLength(60).IsRequired();
        b.Property(x => x.TransitionedAt).HasColumnType("datetime2(3)");
        b.HasOne<ExecutionRunHistoryEntity>().WithMany()
            .HasForeignKey(x => x.ExecutionRunId).HasPrincipalKey(x => x.ExecutionRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExecutionArtifactHistoryEntityConfiguration
    : IEntityTypeConfiguration<ExecutionArtifactHistoryEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionArtifactHistoryEntity> b)
    {
        b.ToTable("ExecutionArtifactHistory", "history", t =>
        {
            t.HasCheckConstraint("CK_ExecutionArtifactHistory_Versions",
                "[HistorySchemaVersion]>0 AND [ArtifactSchemaVersion]>0");
            t.HasCheckConstraint("CK_ExecutionArtifactHistory_Counts",
                "([ObjectCount] IS NULL OR [ObjectCount]>=0) AND ([MetricCount] IS NULL OR [MetricCount]>=0) AND [ByteCount]>=0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.ExecutionRunId, x.ExecutionStepId, x.ArtifactId }).IsUnique();
        b.HasIndex(x => x.ExecutionRunId);
        b.HasIndex(x => new { x.ExecutionRunId, x.ExecutionStepId });
        b.Property(x => x.ArtifactId).HasMaxLength(100).IsRequired();
        b.Property(x => x.ArtifactType).HasMaxLength(20).IsRequired();
        b.Property(x => x.LogicalName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        b.HasOne<ExecutionRunHistoryEntity>().WithMany()
            .HasForeignKey(x => x.ExecutionRunId).HasPrincipalKey(x => x.ExecutionRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExecutionPolicyHistoryEntityConfiguration
    : IEntityTypeConfiguration<ExecutionPolicyHistoryEntity>
{
    public void Configure(EntityTypeBuilder<ExecutionPolicyHistoryEntity> b)
    {
        b.ToTable("ExecutionPolicyHistory", "history", t =>
        {
            t.HasCheckConstraint("CK_ExecutionPolicyHistory_Versions",
                "[HistorySchemaVersion]>0 AND [TimeoutPolicyVersion]>0 AND [RetryPolicyVersion]>0 AND [ParallelPolicyVersion]>0 AND [ThrottlingPolicyVersion]>0 AND [BatchingPolicyVersion]>0");
            t.HasCheckConstraint("CK_ExecutionPolicyHistory_Values",
                "[TimeoutTicks]>0 AND [MaximumAttempts]>0 AND [ParallelMaximumConcurrency]>0 AND [ThrottlingMaximumConcurrency]>0");
        });
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => new { x.ExecutionRunId, x.ExecutionStepId }).IsUnique();
        Required(b.Property(x => x.TimeoutPolicyCode));
        Required(b.Property(x => x.RetryPolicyCode));
        Required(b.Property(x => x.RetryDelayClassification));
        Required(b.Property(x => x.ParallelPolicyCode));
        Required(b.Property(x => x.ThrottlingPolicyCode));
        Required(b.Property(x => x.BatchingPolicyCode));
        b.HasOne<ExecutionRunHistoryEntity>().WithMany()
            .HasForeignKey(x => x.ExecutionRunId).HasPrincipalKey(x => x.ExecutionRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
    private static void Required(PropertyBuilder<string> property) =>
        property.HasMaxLength(100).IsRequired();
}
