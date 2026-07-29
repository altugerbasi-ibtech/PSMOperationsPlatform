using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PSMOperationsPlatform.Application.Capabilities;
using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Domain.Entities;
using DecisionPlan = PSMOperationsPlatform.Application.Decisions.CollectorDecisionPlan;
using DecisionEntity = PSMOperationsPlatform.Domain.Entities.CollectorDecisionPlan;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public interface ICollectorDecisionCoordinator
{
    Task EvaluateAndReplaceAsync(Guid capabilitySnapshotId,
        CapabilityEvaluationResult snapshot, CancellationToken cancellationToken);
}

public sealed class CollectorDecisionCoordinator(
    OperationsDbContext context,
    ICollectorDecisionEngine engine,
    TimeProvider timeProvider,
    ILogger<CollectorDecisionCoordinator> logger,
    IExecutionPlanCoordinator? executionPlanCoordinator = null) : ICollectorDecisionCoordinator
{
    public async Task EvaluateAndReplaceAsync(Guid capabilitySnapshotId,
        CapabilityEvaluationResult snapshot, CancellationToken cancellationToken)
    {
        long started = timeProvider.GetTimestamp();
        logger.LogInformation(
            "Collector decision evaluation started. ManagedServerId={ManagedServerId} CapabilitySnapshotId={CapabilitySnapshotId} SourceInventoryRunId={SourceInventoryRunId} SourceInventoryVersion={SourceInventoryVersion} CapabilitySchemaVersion={CapabilitySchemaVersion} DecisionSchemaVersion={DecisionSchemaVersion}",
            snapshot.ManagedServerId, capabilitySnapshotId, snapshot.InventoryRunId,
            snapshot.SourceInventoryVersion, snapshot.CapabilitySchemaVersion, CollectorDecisionEngine.SchemaVersion);
        var input = new CollectorDecisionInput(snapshot.ManagedServerId, null, capabilitySnapshotId,
            snapshot.CapabilitySchemaVersion, snapshot.InventoryRunId, snapshot.SourceInventoryVersion,
            CapabilitySubject.ManagedTargetServer, snapshot.EvaluationStatus, snapshot.EvaluatedAt,
            snapshot.Entries.ToArray());
        DecisionPlan plan = engine.Evaluate(input);

        Guid decisionPlanId = Guid.NewGuid();
        await InventoryStoreGuard.ExecuteTransactionAsync(context, async () =>
        {
            DecisionEntity? previous = await context.CollectorDecisionPlans
                .Include(x => x.Strategies).ThenInclude(x => x.CapabilityReferences)
                .SingleOrDefaultAsync(x => x.ManagedServerId == plan.ManagedServerId, cancellationToken);
            if (previous is not null)
            {
                context.CollectorDecisionCapabilityReferences.RemoveRange(
                    previous.Strategies.SelectMany(x => x.CapabilityReferences));
                context.CollectorStrategyDecisions.RemoveRange(previous.Strategies);
                context.CollectorDecisionPlans.Remove(previous);
            }
            var entity = new DecisionEntity(decisionPlanId, plan.ManagedServerId,
                plan.CapabilitySnapshotId, plan.SourceInventoryRunId, plan.SourceInventoryVersion,
                plan.CapabilitySchemaVersion, plan.DecisionSchemaVersion, plan.EvaluatedAt,
                plan.OverallStatus.ToString(), plan.StrategyCount, plan.EligibleCount,
                plan.BlockedCount, plan.IndeterminateCount, plan.NotApplicableCount,
                plan.DisabledCount, plan.InvalidCount);
            foreach (PSMOperationsPlatform.Application.Decisions.CollectorStrategyDecision decision in plan.Strategies)
            {
                var strategy = new PSMOperationsPlatform.Domain.Entities.CollectorStrategyDecision(
                    Guid.NewGuid(), entity.Id, decision.StrategyCode, decision.StrategyVersion,
                    decision.Subject.ToString(), decision.Category.ToString(),
                    decision.EligibilityStatus.ToString(), decision.ExecutionReadinessStatus.ToString(),
                    decision.DecisionStatus.ToString(), decision.Priority, decision.ExecutionOrder,
                    decision.IsReadOnly, decision.RequiresManualApproval,
                    decision.ReasonCode, decision.Explanation);
                foreach (CapabilityDecisionProvenance item in decision.Provenance)
                    strategy.CapabilityReferences.Add(new CollectorDecisionCapabilityReference(
                        Guid.NewGuid(), strategy.Id, item.CapabilityCode,
                        item.CapabilityCategory.ToString(), Classify(decision, item.CapabilityCode),
                        item.CapabilityRuleVersion,
                        item.SupportStatus.ToString(), item.ReadinessStatus.ToString(),
                        item.EvaluationStatus.ToString(), item.ReasonCode,
                        item.CapabilitySnapshotId, item.SourceInventoryRunId, item.SourceInventoryVersion));
                entity.Strategies.Add(strategy);
            }
            context.CollectorDecisionPlans.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        if (executionPlanCoordinator is not null)
        {
            try
            {
                await executionPlanCoordinator.BuildAndReplaceAsync(
                    decisionPlanId, plan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                logger.LogWarning(
                    "Execution planning failed after Decision Plan persistence. ManagedServerId={ManagedServerId} DecisionPlanId={DecisionPlanId} CapabilitySnapshotId={CapabilitySnapshotId} SourceInventoryRunId={SourceInventoryRunId} SourceInventoryVersion={SourceInventoryVersion} FailureCategory={FailureCategory} PersistenceStatus={PersistenceStatus}",
                    plan.ManagedServerId, decisionPlanId, plan.CapabilitySnapshotId,
                    plan.SourceInventoryRunId, plan.SourceInventoryVersion,
                    "ExecutionPlanGenerationOrPersistenceFailure", "PreservedPriorPlan");
            }
        }

        logger.LogInformation(
            "Collector decision evaluation completed. ManagedServerId={ManagedServerId} CapabilitySnapshotId={CapabilitySnapshotId} SourceInventoryRunId={SourceInventoryRunId} SourceInventoryVersion={SourceInventoryVersion} CapabilitySchemaVersion={CapabilitySchemaVersion} DecisionSchemaVersion={DecisionSchemaVersion} DurationMs={DurationMs} StrategyCount={StrategyCount} EligibleCount={EligibleCount} BlockedCount={BlockedCount} IndeterminateCount={IndeterminateCount} NotApplicableCount={NotApplicableCount} DisabledCount={DisabledCount} InvalidCount={InvalidCount} PersistenceStatus={PersistenceStatus}",
            plan.ManagedServerId, plan.CapabilitySnapshotId, plan.SourceInventoryRunId,
            plan.SourceInventoryVersion, plan.CapabilitySchemaVersion, plan.DecisionSchemaVersion,
            timeProvider.GetElapsedTime(started).TotalMilliseconds, plan.StrategyCount,
            plan.EligibleCount, plan.BlockedCount, plan.IndeterminateCount,
            plan.NotApplicableCount, plan.DisabledCount, plan.InvalidCount, "Succeeded");
    }

    private static string Classify(
        PSMOperationsPlatform.Application.Decisions.CollectorStrategyDecision decision,
        string capabilityCode) =>
        decision.SatisfiedCapabilities.Contains(capabilityCode, StringComparer.Ordinal) ? "Satisfied"
        : decision.BlockingCapabilities.Contains(capabilityCode, StringComparer.Ordinal) ? "Blocking"
        : decision.UnknownCapabilities.Contains(capabilityCode, StringComparer.Ordinal) ? "Unknown"
        : decision.InvalidCapabilities.Contains(capabilityCode, StringComparer.Ordinal) ? "Invalid"
        : decision.OptionalCapabilities.Contains(capabilityCode, StringComparer.Ordinal) ? "Optional"
        : "Evaluated";
}
