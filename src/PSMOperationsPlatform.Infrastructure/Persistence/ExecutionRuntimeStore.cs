using Microsoft.EntityFrameworkCore;
using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.Application.Runtime;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public interface ICommittedExecutionPlanLoader
{
    Task<CollectorRuntimeInput> LoadAsync(Guid executionPlanId, DateTime requestedAt,
        string? correlationId, CancellationToken cancellationToken);
}

public sealed class CommittedExecutionPlanLoader(OperationsDbContext context)
    : ICommittedExecutionPlanLoader
{
    public async Task<CollectorRuntimeInput> LoadAsync(Guid executionPlanId,
        DateTime requestedAt, string? correlationId, CancellationToken cancellationToken)
    {
        ExecutionPlan plan = await context.ExecutionPlans.AsNoTracking()
            .Include(x => x.Steps).Include(x => x.Exclusions)
            .SingleOrDefaultAsync(x => x.Id == executionPlanId, cancellationToken)
            ?? throw new InvalidOperationException("ExecutionPlanUnavailable");

        CollectorRuntimeStep[] steps = plan.Steps.OrderBy(x => x.StepSequence)
            .Select(x => new CollectorRuntimeStep(x.LogicalStepId, x.StrategyCode,
                x.StrategyVersion, Enum.Parse<DecisionSubject>(x.Subject), x.StepSequence,
                x.Priority, x.ExecutionOrder, x.ParallelGroupCode, x.TimeoutPolicyCode,
                x.TimeoutPolicyVersion, x.RetryPolicyCode, x.RetryPolicyVersion,
                Enum.Parse<ThrottlingClass>(x.ThrottlingClass), x.BatchGroupCode,
                x.IsReadOnly, x.RequiresManualApproval,
                Array.AsReadOnly(Array.Empty<string>()))).ToArray();
        CollectorRuntimeExclusion[] exclusions = plan.Exclusions
            .OrderBy(x => x.StrategyCode, StringComparer.Ordinal)
            .Select(x => new CollectorRuntimeExclusion(x.StrategyCode,
                Enum.Parse<PlanningDisposition>(x.PlanningDisposition), x.ReasonCode)).ToArray();
        return new(plan.ManagedServerId, null, plan.Id, plan.ExecutionPlanSchemaVersion,
            Enum.Parse<ExecutionPlanStatus>(plan.PlanStatus), plan.DecisionPlanId,
            plan.CapabilitySnapshotId, plan.SourceInventoryRunId,
            plan.SourceInventoryVersion, plan.CreatedAt, requestedAt, correlationId,
            Array.AsReadOnly(steps), Array.AsReadOnly(exclusions));
    }
}

public sealed class ExecutionStateStore(OperationsDbContext context) : IExecutionStateStore
{
    public async Task CreateAsync(ExecutionRunState run, CancellationToken cancellationToken)
    {
        if (await context.ExecutionRunStates.AnyAsync(
                x => x.ExecutionPlanId == run.ExecutionPlanId, cancellationToken))
            throw new InvalidOperationException("ExecutionPlanAlreadyExecuted");
        ExecutionRunStateEntity entity = Create(run);
        context.ExecutionRunStates.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAsync(ExecutionRunState run, CancellationToken cancellationToken)
    {
        ExecutionRunStateEntity entity = context.ExecutionRunStates.Local
            .SingleOrDefault(x => x.Id == run.Id)
            ?? await context.ExecutionRunStates.Include(x => x.Steps)
                .ThenInclude(x => x.Attempts)
                .SingleAsync(x => x.Id == run.Id, cancellationToken);
        Apply(run, entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static ExecutionRunStateEntity Create(ExecutionRunState run)
    {
        var entity = new ExecutionRunStateEntity(run.Id, run.ManagedServerId,
            run.ExecutionPlanId, run.ExecutionPlanSchemaVersion,
            run.ExecutionStateSchemaVersion, run.SourceDecisionPlanId,
            run.SourceCapabilitySnapshotId, run.SourceInventoryRunId,
            run.SourceInventoryVersion, run.RuntimeVersion, run.Status.ToString(),
            run.CreatedAt);
        foreach (ExecutionStepState step in run.Steps)
            entity.Steps.Add(new ExecutionStepStateEntity(step.Id, run.Id,
                step.ExecutionPlanStepId, step.StrategyCode, step.StrategyVersion,
                step.QueueSequence, step.QueuedAt));
        Apply(run, entity);
        return entity;
    }

    private static void Apply(ExecutionRunState run, ExecutionRunStateEntity entity)
    {
        entity.Update(run.Status.ToString(), run.StartedAt, run.CompletedAt,
            run.CancelledAt, run.TotalDuration.Ticks, run.StepCount, run.AttemptCount,
            run.RetryCount, run.BytesCollected, run.ObjectsCollected,
            run.FailureCategory.ToString(), run.ReasonCode, run.FailureSummary);
        foreach (ExecutionStepState step in run.Steps)
        {
            ExecutionStepStateEntity target = entity.Steps.Single(x => x.Id == step.Id);
            target.Update(step.PluginVersion, step.Status.ToString(), step.EligibleAt,
                step.StartedAt, step.CompletedAt, step.CancelledAt, step.TimedOutAt,
                step.QueueDuration.Ticks, step.WaitDuration.Ticks,
                step.ExecutionDuration.Ticks, step.TotalDuration.Ticks,
                step.AttemptCount, step.RetryCount, step.BytesCollected,
                step.ObjectsCollected, step.FailureCategory.ToString(),
                step.ReasonCode, step.FailureSummary);
            foreach (ExecutionAttemptState attempt in step.Attempts)
            {
                ExecutionAttemptStateEntity? attemptEntity =
                    target.Attempts.SingleOrDefault(x => x.Id == attempt.Id);
                if (attemptEntity is null)
                {
                    attemptEntity = new ExecutionAttemptStateEntity(attempt.Id,
                        step.Id, attempt.AttemptNumber, attempt.IsRetry,
                        attempt.StartedAt);
                    target.Attempts.Add(attemptEntity);
                }
                attemptEntity.Update(attempt.Status.ToString(), attempt.CompletedAt,
                    attempt.Duration.Ticks, attempt.BytesCollected,
                    attempt.ObjectsCollected, attempt.FailureCategory.ToString(),
                    attempt.ReasonCode, attempt.FailureSummary);
            }
        }
    }
}

public interface ICollectorRuntimeOrchestrator
{
    Task<ExecutionDispatchResult> ExecuteCommittedPlanAsync(Guid executionPlanId,
        string? correlationId, CancellationToken cancellationToken);
}

public sealed class CollectorRuntimeOrchestrator(
    ICommittedExecutionPlanLoader loader,
    IExecutionDispatcher dispatcher,
    TimeProvider timeProvider) : ICollectorRuntimeOrchestrator
{
    public async Task<ExecutionDispatchResult> ExecuteCommittedPlanAsync(Guid executionPlanId,
        string? correlationId, CancellationToken cancellationToken)
    {
        CollectorRuntimeInput input = await loader.LoadAsync(executionPlanId,
            timeProvider.GetLocalNow().DateTime, correlationId, cancellationToken);
        return await dispatcher.DispatchAsync(new ExecutionDispatchRequest(input),
            cancellationToken);
    }
}
