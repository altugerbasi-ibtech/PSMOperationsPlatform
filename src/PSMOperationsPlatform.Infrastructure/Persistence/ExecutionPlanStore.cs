using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.Domain.Entities;
using DecisionPlan = PSMOperationsPlatform.Application.Decisions.CollectorDecisionPlan;
using PlanEntity = PSMOperationsPlatform.Domain.Entities.ExecutionPlan;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public interface IExecutionPlanCoordinator
{
    Task BuildAndReplaceAsync(Guid decisionPlanId, DecisionPlan decisionPlan,
        CancellationToken cancellationToken);
}

public sealed class ExecutionPlanCoordinator(
    OperationsDbContext context,
    IExecutionPlanEngine engine,
    TimeProvider timeProvider,
    ILogger<ExecutionPlanCoordinator> logger) : IExecutionPlanCoordinator
{
    public async Task BuildAndReplaceAsync(Guid decisionPlanId, DecisionPlan decisionPlan,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long started = timeProvider.GetTimestamp();
        logger.LogInformation(
            "Execution planning started. ManagedServerId={ManagedServerId} DecisionPlanId={DecisionPlanId} CapabilitySnapshotId={CapabilitySnapshotId} SourceInventoryRunId={SourceInventoryRunId} SourceInventoryVersion={SourceInventoryVersion} DecisionSchemaVersion={DecisionSchemaVersion} CapabilitySchemaVersion={CapabilitySchemaVersion} ExecutionPlanSchemaVersion={ExecutionPlanSchemaVersion} InputStrategyCount={InputStrategyCount}",
            decisionPlan.ManagedServerId, decisionPlanId, decisionPlan.CapabilitySnapshotId,
            decisionPlan.SourceInventoryRunId, decisionPlan.SourceInventoryVersion,
            decisionPlan.DecisionSchemaVersion, decisionPlan.CapabilitySchemaVersion,
            ExecutionPlanEngine.SchemaVersion, decisionPlan.StrategyCount);

        ExecutionPlanInput input = ToInput(decisionPlanId, decisionPlan);
        ExecutionPlanResult result = engine.Build(input);
        cancellationToken.ThrowIfCancellationRequested();

        await InventoryStoreGuard.ExecuteTransactionAsync(context, async () =>
        {
            PlanEntity? previous = await context.ExecutionPlans
                .Include(x => x.Steps)
                .Include(x => x.Exclusions).ThenInclude(x => x.Capabilities)
                .SingleOrDefaultAsync(x => x.ManagedServerId == result.ManagedServerId, cancellationToken);
            if (previous is not null)
            {
                ExecutionRunStateEntity? priorRun = await context.ExecutionRunStates
                    .Include(x => x.Steps).ThenInclude(x => x.Attempts)
                    .SingleOrDefaultAsync(x => x.ExecutionPlanId == previous.Id, cancellationToken);
                if (priorRun is not null)
                {
                    context.ExecutionAttemptStates.RemoveRange(
                        priorRun.Steps.SelectMany(x => x.Attempts));
                    context.ExecutionStepStates.RemoveRange(priorRun.Steps);
                    context.ExecutionRunStates.Remove(priorRun);
                }
                context.ExecutionPlanExclusionCapabilities.RemoveRange(
                    previous.Exclusions.SelectMany(x => x.Capabilities));
                context.ExecutionPlanSteps.RemoveRange(previous.Steps);
                context.ExecutionPlanExclusions.RemoveRange(previous.Exclusions);
                context.ExecutionPlans.Remove(previous);
            }

            Guid planId = Guid.NewGuid();
            var entity = new PlanEntity(planId, result.ManagedServerId, result.DecisionPlanId,
                result.CapabilitySnapshotId, result.SourceInventoryRunId,
                result.SourceInventoryVersion, result.CapabilitySchemaVersion,
                result.DecisionSchemaVersion, result.ExecutionPlanSchemaVersion,
                result.CreatedAt, result.PlanStatus.ToString(), result.StepCount,
                result.ExclusionCount);

            foreach (PSMOperationsPlatform.Application.ExecutionPlanning.ExecutionPlanStep step in result.Steps)
                entity.Steps.Add(new PSMOperationsPlatform.Domain.Entities.ExecutionPlanStep(
                    Guid.NewGuid(), planId, step.StepId, step.StrategyCode,
                    step.StrategyVersion, step.Subject.ToString(), step.Category.ToString(),
                    step.StepSequence, step.Priority, step.ExecutionOrder,
                    step.ParallelGroupCode, step.TimeoutPolicyCode,
                    step.TimeoutPolicyVersion, checked((int)step.Timeout.TotalSeconds),
                    step.RetryPolicyCode, step.RetryPolicyVersion,
                    step.ThrottlingClass.ToString(), step.BatchGroupCode,
                    step.IsReadOnly, step.RequiresManualApproval,
                    step.SourceDecisionStatus.ToString(), step.SourceDecisionReasonCode,
                    step.InclusionReasonCode, step.Explanation));

            foreach (PSMOperationsPlatform.Application.ExecutionPlanning.ExecutionPlanExclusion exclusion in result.Exclusions)
            {
                var exclusionEntity = new PSMOperationsPlatform.Domain.Entities.ExecutionPlanExclusion(
                    Guid.NewGuid(), planId, exclusion.StrategyCode, exclusion.StrategyVersion,
                    exclusion.SourceDecisionStatus.ToString(), exclusion.PlanningDisposition.ToString(),
                    exclusion.ReasonCode, exclusion.Explanation);
                foreach (CapabilityDecisionProvenance provenance in exclusion.Provenance)
                    exclusionEntity.Capabilities.Add(new ExecutionPlanExclusionCapability(
                        Guid.NewGuid(), exclusionEntity.Id, provenance.CapabilityCode,
                        Classification(exclusion, provenance.CapabilityCode),
                        provenance.CapabilityRuleVersion, provenance.CapabilitySnapshotId,
                        provenance.SourceInventoryRunId, provenance.SourceInventoryVersion));
                entity.Exclusions.Add(exclusionEntity);
            }

            string[] stepCodes = entity.Steps.Select(x => x.StrategyCode).ToArray();
            if (stepCodes.Intersect(entity.Exclusions.Select(x => x.StrategyCode),
                    StringComparer.Ordinal).Any())
                throw new InvalidOperationException("A strategy cannot be both executable and excluded.");

            context.ExecutionPlans.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        logger.LogInformation(
            "Execution planning completed. ManagedServerId={ManagedServerId} DecisionPlanId={DecisionPlanId} CapabilitySnapshotId={CapabilitySnapshotId} SourceInventoryRunId={SourceInventoryRunId} SourceInventoryVersion={SourceInventoryVersion} DecisionSchemaVersion={DecisionSchemaVersion} CapabilitySchemaVersion={CapabilitySchemaVersion} ExecutionPlanSchemaVersion={ExecutionPlanSchemaVersion} DurationMs={DurationMs} InputStrategyCount={InputStrategyCount} ExecutableStepCount={ExecutableStepCount} ExclusionCount={ExclusionCount} PlanStatus={PlanStatus} PersistenceStatus={PersistenceStatus}",
            result.ManagedServerId, result.DecisionPlanId, result.CapabilitySnapshotId,
            result.SourceInventoryRunId, result.SourceInventoryVersion,
            result.DecisionSchemaVersion, result.CapabilitySchemaVersion,
            result.ExecutionPlanSchemaVersion, timeProvider.GetElapsedTime(started).TotalMilliseconds,
            decisionPlan.StrategyCount, result.StepCount, result.ExclusionCount,
            result.PlanStatus, "Succeeded");
    }

    private static ExecutionPlanInput ToInput(Guid decisionPlanId, DecisionPlan plan) =>
        new(plan.ManagedServerId, null, decisionPlanId, plan.DecisionSchemaVersion,
            plan.CapabilitySnapshotId, plan.CapabilitySchemaVersion,
            plan.SourceInventoryRunId, plan.SourceInventoryVersion, plan.EvaluatedAt,
            DecisionSubject.ManagedTargetServer,
            Array.AsReadOnly(plan.Strategies.Select(strategy => new ExecutionPlanStrategyInput(
                strategy.StrategyCode, strategy.StrategyVersion, strategy.Subject,
                strategy.Category, strategy.DecisionStatus, strategy.EligibilityStatus,
                strategy.ExecutionReadinessStatus, strategy.Priority, strategy.ExecutionOrder,
                strategy.IsReadOnly, strategy.RequiresManualApproval, strategy.ReasonCode,
                strategy.Explanation, Array.AsReadOnly(strategy.BlockingCapabilities.ToArray()),
                Array.AsReadOnly(strategy.UnknownCapabilities.ToArray()),
                Array.AsReadOnly(strategy.Provenance.ToArray()),
                Array.AsReadOnly(strategy.Warnings.ToArray()))).ToArray()),
            Array.AsReadOnly(plan.Warnings.ToArray()));

    private static string Classification(
        PSMOperationsPlatform.Application.ExecutionPlanning.ExecutionPlanExclusion exclusion,
        string capabilityCode) =>
        exclusion.BlockingCapabilities.Contains(capabilityCode, StringComparer.Ordinal) ? "Blocking"
        : exclusion.UnknownCapabilities.Contains(capabilityCode, StringComparer.Ordinal) ? "Unknown"
        : "Evaluated";
}
