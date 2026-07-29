using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class ExecutionPlanConfiguration : IEntityTypeConfiguration<ExecutionPlan>
{
    public void Configure(EntityTypeBuilder<ExecutionPlan> builder)
    {
        builder.ToTable("ExecutionPlan", "inventory", table =>
        {
            table.HasCheckConstraint("CK_ExecutionPlan_SourceInventoryVersion", "[SourceInventoryVersion] > 0");
            table.HasCheckConstraint("CK_ExecutionPlan_SchemaVersions",
                "[CapabilitySchemaVersion] > 0 AND [DecisionSchemaVersion] > 0 AND [ExecutionPlanSchemaVersion] > 0");
            table.HasCheckConstraint("CK_ExecutionPlan_Counts", "[StepCount] >= 0 AND [ExclusionCount] >= 0");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.PlanStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired();
        builder.HasOne<ManagedServer>().WithMany().HasForeignKey(x => x.ManagedServerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ManagedServerId).IsUnique().HasDatabaseName("UX_ExecutionPlan_ManagedServer");
        builder.HasIndex(x => new { x.ManagedServerId, x.DecisionPlanId }).HasDatabaseName("IX_ExecutionPlan_DecisionPlanId");
        builder.HasIndex(x => x.CapabilitySnapshotId).HasDatabaseName("IX_ExecutionPlan_CapabilitySnapshotId");
        builder.HasIndex(x => new { x.ManagedServerId, x.SourceInventoryRunId }).HasDatabaseName("IX_ExecutionPlan_SourceInventoryRunId");
        builder.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.ExecutionPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Exclusions).WithOne().HasForeignKey(x => x.ExecutionPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExecutionPlanStepConfiguration : IEntityTypeConfiguration<ExecutionPlanStep>
{
    public void Configure(EntityTypeBuilder<ExecutionPlanStep> builder)
    {
        builder.ToTable("ExecutionPlanStep", "inventory", table =>
        {
            table.HasCheckConstraint("CK_ExecutionPlanStep_PositiveValues",
                "[StrategyVersion] > 0 AND [StepSequence] > 0 AND [Priority] > 0 AND [ExecutionOrder] > 0 AND [TimeoutSeconds] > 0");
            table.HasCheckConstraint("CK_ExecutionPlanStep_PolicyVersions",
                "[TimeoutPolicyVersion] > 0 AND [RetryPolicyVersion] > 0");
            table.HasCheckConstraint("CK_ExecutionPlanStep_TimeoutBound", "[TimeoutSeconds] <= 3600");
            table.HasCheckConstraint("CK_ExecutionPlanStep_ReadOnly", "[IsReadOnly] = 1");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.StrategyCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ParallelGroupCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TimeoutPolicyCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RetryPolicyCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ThrottlingClass).HasMaxLength(30).IsRequired();
        builder.Property(x => x.BatchGroupCode).HasMaxLength(50);
        builder.Property(x => x.SourceDecisionStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SourceDecisionReasonCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.InclusionReasonCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Explanation).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired();
        builder.HasIndex(x => new { x.ExecutionPlanId, x.StrategyCode }).IsUnique();
        builder.HasIndex(x => new { x.ExecutionPlanId, x.StepSequence }).IsUnique();
        builder.HasIndex(x => new { x.ExecutionPlanId, x.LogicalStepId }).IsUnique();
    }
}

public sealed class ExecutionPlanExclusionConfiguration : IEntityTypeConfiguration<ExecutionPlanExclusion>
{
    public void Configure(EntityTypeBuilder<ExecutionPlanExclusion> builder)
    {
        builder.ToTable("ExecutionPlanExclusion", "inventory", table =>
            table.HasCheckConstraint("CK_ExecutionPlanExclusion_StrategyVersion", "[StrategyVersion] > 0"));
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.StrategyCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SourceDecisionStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.PlanningDisposition).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Explanation).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired();
        builder.HasIndex(x => new { x.ExecutionPlanId, x.StrategyCode }).IsUnique();
        builder.HasMany(x => x.Capabilities).WithOne().HasForeignKey(x => x.ExclusionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExecutionPlanExclusionCapabilityConfiguration : IEntityTypeConfiguration<ExecutionPlanExclusionCapability>
{
    public void Configure(EntityTypeBuilder<ExecutionPlanExclusionCapability> builder)
    {
        builder.ToTable("ExecutionPlanExclusionCapability", "inventory", table =>
        {
            table.HasCheckConstraint("CK_ExecutionPlanExclusionCapability_RuleVersion", "[CapabilityRuleVersion] > 0");
            table.HasCheckConstraint("CK_ExecutionPlanExclusionCapability_InventoryVersion", "[SourceInventoryVersion] > 0");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CapabilityCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Classification).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.ExclusionId, x.CapabilityCode }).IsUnique();
    }
}
