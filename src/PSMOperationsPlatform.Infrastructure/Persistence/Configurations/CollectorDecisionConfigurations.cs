using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence.Configurations;

public sealed class CollectorDecisionPlanConfiguration : IEntityTypeConfiguration<CollectorDecisionPlan>
{
    public void Configure(EntityTypeBuilder<CollectorDecisionPlan> builder)
    {
        builder.ToTable("CollectorDecisionPlan", "inventory", table =>
        {
            table.HasCheckConstraint("CK_CollectorDecisionPlan_SourceInventoryVersion", "[SourceInventoryVersion] > 0");
            table.HasCheckConstraint("CK_CollectorDecisionPlan_Versions", "[CapabilitySchemaVersion] > 0 AND [DecisionSchemaVersion] > 0");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OverallStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.EvaluatedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired();
        builder.HasOne<ManagedServer>().WithMany().HasForeignKey(x => x.ManagedServerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ManagedServerId).IsUnique().HasDatabaseName("UX_CollectorDecisionPlan_ManagedServer");
        builder.HasIndex(x => x.CapabilitySnapshotId).HasDatabaseName("IX_CollectorDecisionPlan_CapabilitySnapshotId");
        builder.HasIndex(x => new { x.ManagedServerId, x.SourceInventoryRunId }).HasDatabaseName("IX_CollectorDecisionPlan_SourceInventoryRunId");
        builder.HasMany(x => x.Strategies).WithOne().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectorStrategyDecisionConfiguration : IEntityTypeConfiguration<CollectorStrategyDecision>
{
    public void Configure(EntityTypeBuilder<CollectorStrategyDecision> builder)
    {
        builder.ToTable("CollectorStrategyDecision", "inventory", table =>
        {
            table.HasCheckConstraint("CK_CollectorStrategyDecision_StrategyVersion", "[StrategyVersion] > 0");
            table.HasCheckConstraint("CK_CollectorStrategyDecision_PriorityOrder", "[Priority] > 0 AND [ExecutionOrder] > 0");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.StrategyCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(30).IsRequired();
        builder.Property(x => x.EligibilityStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ExecutionReadinessStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DecisionStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Explanation).HasMaxLength(500).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion().IsRequired();
        builder.HasIndex(x => new { x.PlanId, x.StrategyCode }).IsUnique();
        builder.HasMany(x => x.CapabilityReferences).WithOne().HasForeignKey(x => x.StrategyDecisionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CollectorDecisionCapabilityReferenceConfiguration : IEntityTypeConfiguration<CollectorDecisionCapabilityReference>
{
    public void Configure(EntityTypeBuilder<CollectorDecisionCapabilityReference> builder)
    {
        builder.ToTable("CollectorDecisionCapabilityReference", "inventory", table =>
        {
            table.HasCheckConstraint("CK_CollectorDecisionCapabilityReference_RuleVersion", "[CapabilityRuleVersion] > 0");
            table.HasCheckConstraint("CK_CollectorDecisionCapabilityReference_InventoryVersion", "[SourceInventoryVersion] > 0");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CapabilityCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CapabilityCategory).HasMaxLength(30).IsRequired();
        builder.Property(x => x.PrerequisiteStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.SupportStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReadinessStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.EvaluationStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.StrategyDecisionId, x.CapabilityCode }).IsUnique();
    }
}
