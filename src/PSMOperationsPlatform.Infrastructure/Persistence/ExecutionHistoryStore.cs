using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PSMOperationsPlatform.Application.Runtime;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public sealed class ExecutionHistoryWriter(OperationsDbContext context)
    : IExecutionHistoryWriter
{
    public async Task<ExecutionHistoryWriteResult> WriteAsync(
        ExecutionHistoryProjection projection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projection);
        cancellationToken.ThrowIfCancellationRequested();
        if (projection.HistorySchemaVersion != ExecutionHistorySchemaVersion.Value
            || projection.Run.ExecutionRunId == Guid.Empty)
            return Failed(ExecutionHistoryFailureCategory.HistorySchemaUnsupported,
                "HistorySchemaUnsupported");
        if (await context.ExecutionRunHistory.AsNoTracking().AnyAsync(
                x => x.ExecutionRunId == projection.Run.ExecutionRunId, cancellationToken))
            return new(ExecutionHistoryWriteDisposition.Duplicate,
                ExecutionHistoryFailureCategory.HistoryDuplicateObserved,
                "HistoryDuplicateObserved");

        IDbContextTransaction? transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            AddProjection(projection);
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new(ExecutionHistoryWriteDisposition.Created,
                ExecutionHistoryFailureCategory.None, "HistoryProjectionCompleted");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (DbUpdateException)
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            if (await context.ExecutionRunHistory.AsNoTracking().AnyAsync(
                    x => x.ExecutionRunId == projection.Run.ExecutionRunId,
                    CancellationToken.None))
                return new(ExecutionHistoryWriteDisposition.Duplicate,
                    ExecutionHistoryFailureCategory.HistoryDuplicateObserved,
                    "HistoryDuplicateObserved");
            return Failed(ExecutionHistoryFailureCategory.HistoryPersistenceFailure,
                "HistoryPersistenceFailure");
        }
        catch (Exception)
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            return Failed(ExecutionHistoryFailureCategory.HistoryPersistenceFailure,
                "HistoryPersistenceFailure");
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private void AddProjection(ExecutionHistoryProjection value)
    {
        ExecutionRunHistoryItem r = value.Run;
        context.ExecutionRunHistory.Add(new(r.ExecutionRunId, r.ExecutionPlanId,
            r.ManagedServerId, value.HistorySchemaVersion, r.QueuedAt, r.StartedAt,
            r.CompletedAt, r.DurationTicks, r.RecordedAt, r.ExecutionOutcome,
            r.TerminalState, r.FailureCategory, r.ReasonCode, r.WarningCount,
            r.AttemptCount, r.RetryCount, r.StepCount, r.CompletedStepCount,
            r.FailedStepCount, r.TimedOutStepCount, r.CancelledStepCount,
            r.SkippedStepCount, r.StrategyCode, r.StrategyVersion, r.PluginId,
            r.PluginVersion, r.TargetSdkVersion, r.RuntimeContractVersion,
            r.ExecutionPlanSchemaVersion, r.ExecutionStateSchemaVersion,
            r.ExecutionEventSchemaVersion, r.ExecutionMonitoringSchemaVersion,
            r.Subject, r.IsReadOnly, r.ArtifactFileCount, r.ArtifactObjectCount,
            r.ArtifactMetricCount, r.ArtifactByteCount, r.ProjectionStatus.ToString(),
            r.ProjectionFailureCategory.ToString(), r.ProjectionReasonCode,
            r.SourceDecisionPlanId, r.SourceCapabilitySnapshotId,
            r.SourceInventoryRunId, r.SourceInventoryVersion));
        context.ExecutionStepHistory.AddRange(value.Steps.Select(x =>
            new ExecutionStepHistoryEntity(Guid.NewGuid(), r.ExecutionRunId,
                x.ExecutionStepId, value.HistorySchemaVersion, x.StepOrdinal,
                x.DependencyCount, x.StrategyCode, x.StrategyVersion, x.PluginId,
                x.PluginVersion, x.Subject, x.QueuedAt, x.StartedAt, x.CompletedAt,
                x.QueueDurationTicks, x.WaitDurationTicks, x.ExecutionDurationTicks,
                x.StepOutcome, x.FailureCategory, x.ReasonCode, x.AttemptCount,
                x.RetryCount, x.WasThrottled, x.WasSkipped, x.WasCancelled,
                x.WasTimedOut, x.ArtifactFileCount, x.ArtifactObjectCount,
                x.ArtifactMetricCount, x.ArtifactByteCount, x.WarningCount)));
        context.ExecutionAttemptHistory.AddRange(value.Attempts.Select(x =>
            new ExecutionAttemptHistoryEntity(Guid.NewGuid(), r.ExecutionRunId,
                x.ExecutionStepId, value.HistorySchemaVersion, x.AttemptNumber,
                x.StartedAt, x.CompletedAt, x.DurationTicks, x.AttemptOutcome,
                x.FailureCategory, x.ReasonCode, x.RetryScheduled,
                x.RetryDelayTicks, x.CancellationObserved, x.TimeoutObserved,
                x.WarningCount)));
        context.ExecutionStateTransitionHistory.AddRange(value.Transitions.Select(x =>
            new ExecutionStateTransitionHistoryEntity(Guid.NewGuid(), r.ExecutionRunId,
                x.ExecutionStepId, value.HistorySchemaVersion, x.TransitionSequence,
                x.EntityType, x.FromState, x.ToState, x.TransitionedAt, x.EventType,
                x.ReasonCode, x.FailureCategory, x.EventSchemaVersion)));
        context.ExecutionArtifactHistory.AddRange(value.Artifacts.Select(x =>
            new ExecutionArtifactHistoryEntity(Guid.NewGuid(), r.ExecutionRunId,
                x.ExecutionStepId, value.HistorySchemaVersion, x.ArtifactId,
                x.ArtifactSchemaVersion, x.ArtifactType, x.LogicalName, x.ContentType,
                x.ObjectCount, x.MetricCount, x.ByteCount, x.CreatedAt)));
        context.ExecutionPolicyHistory.AddRange(value.Policies.Select(x =>
            new ExecutionPolicyHistoryEntity(Guid.NewGuid(), r.ExecutionRunId,
                x.ExecutionStepId, value.HistorySchemaVersion, x.TimeoutPolicyCode,
                x.TimeoutPolicyVersion, x.TimeoutTicks, x.RetryPolicyCode,
                x.RetryPolicyVersion, x.MaximumAttempts,
                x.RetryDelayClassification, x.ParallelPolicyCode,
                x.ParallelPolicyVersion, x.ParallelMaximumConcurrency,
                x.ThrottlingPolicyCode, x.ThrottlingPolicyVersion,
                x.ThrottlingMaximumConcurrency, x.BatchingPolicyCode,
                x.BatchingPolicyVersion, x.BatchingEnabled)));
    }

    private static ExecutionHistoryWriteResult Failed(
        ExecutionHistoryFailureCategory category, string reason) =>
        new(ExecutionHistoryWriteDisposition.Failed, category, reason);
}

public sealed class ExecutionHistoryQueryService(OperationsDbContext context)
    : IExecutionHistoryQueryService
{
    public async Task<ExecutionRunHistoryItem?> GetRunAsync(
        Guid executionRunId, CancellationToken cancellationToken)
    {
        if (executionRunId == Guid.Empty) throw new ArgumentException("HistoryQueryInvalid");
        ExecutionRunHistoryEntity? entity = await context.ExecutionRunHistory
            .AsNoTracking().SingleOrDefaultAsync(
                x => x.ExecutionRunId == executionRunId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<ExecutionHistoryPageResult<ExecutionRunHistoryItem>> ListRunsAsync(
        ExecutionHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Page.Validate();
        if (query.CompletedFrom > query.CompletedTo
            || query.CompletedFrom is null != (query.CompletedTo is null)
            || query.ManagedServerId == Guid.Empty
            || Invalid(query.StrategyCode, 100) || Invalid(query.PluginId, 100)
            || Invalid(query.Outcome, 40) || Invalid(query.FailureCategory, 60)
            || Invalid(query.ReasonCode, 100))
            throw new ArgumentException("HistoryQueryInvalid");
        IQueryable<ExecutionRunHistoryEntity> source =
            context.ExecutionRunHistory.AsNoTracking();
        if (query.CompletedFrom is not null)
            source = source.Where(x => x.CompletedAt >= query.CompletedFrom
                && x.CompletedAt <= query.CompletedTo);
        if (query.ManagedServerId is not null)
            source = source.Where(x => x.ManagedServerId == query.ManagedServerId);
        if (query.StrategyCode is not null)
            source = source.Where(x => x.StrategyCode == query.StrategyCode);
        if (query.PluginId is not null)
            source = source.Where(x => x.PluginId == query.PluginId);
        if (query.Outcome is not null)
            source = source.Where(x => x.ExecutionOutcome == query.Outcome);
        if (query.FailureCategory is not null)
            source = source.Where(x => x.FailureCategory == query.FailureCategory);
        if (query.ReasonCode is not null)
            source = source.Where(x => x.ReasonCode == query.ReasonCode);
        long total = await source.LongCountAsync(cancellationToken);
        ExecutionRunHistoryEntity[] entities = await source
            .OrderByDescending(x => x.CompletedAt).ThenBy(x => x.ExecutionRunId)
            .Skip((query.Page.PageNumber - 1) * query.Page.PageSize)
            .Take(query.Page.PageSize).ToArrayAsync(cancellationToken);
        return new(Array.AsReadOnly(entities.Select(Map).ToArray()),
            query.Page.PageNumber, query.Page.PageSize, total,
            query.Page.PageNumber * query.Page.PageSize < total);
    }

    public async Task<IReadOnlyList<ExecutionStepHistoryItem>> GetStepsAsync(
        Guid runId, CancellationToken token) => Array.AsReadOnly(
        (await context.ExecutionStepHistory.AsNoTracking()
            .Where(x => x.ExecutionRunId == runId).OrderBy(x => x.StepOrdinal)
            .ToArrayAsync(token)).Select(Map).ToArray());

    public async Task<IReadOnlyList<ExecutionAttemptHistoryItem>> GetAttemptsAsync(
        Guid runId, Guid stepId, CancellationToken token) => Array.AsReadOnly(
        (await context.ExecutionAttemptHistory.AsNoTracking()
            .Where(x => x.ExecutionRunId == runId && x.ExecutionStepId == stepId)
            .OrderBy(x => x.AttemptNumber).ToArrayAsync(token)).Select(Map).ToArray());

    public async Task<IReadOnlyList<ExecutionStateTransitionHistoryItem>> GetTransitionsAsync(
        Guid runId, CancellationToken token) => Array.AsReadOnly(
        (await context.ExecutionStateTransitionHistory.AsNoTracking()
            .Where(x => x.ExecutionRunId == runId).OrderBy(x => x.TransitionSequence)
            .ToArrayAsync(token)).Select(Map).ToArray());

    public async Task<IReadOnlyList<ExecutionArtifactHistoryItem>> GetArtifactsAsync(
        Guid runId, CancellationToken token) => Array.AsReadOnly(
        (await context.ExecutionArtifactHistory.AsNoTracking()
            .Where(x => x.ExecutionRunId == runId)
            .OrderBy(x => x.ExecutionStepId).ThenBy(x => x.ArtifactId)
            .ToArrayAsync(token)).Select(Map).ToArray());

    public async Task<IReadOnlyList<ExecutionHistoryPolicyProvenance>> GetPoliciesAsync(
        Guid runId, CancellationToken token) => Array.AsReadOnly(
        (await context.ExecutionPolicyHistory.AsNoTracking()
            .Where(x => x.ExecutionRunId == runId).OrderBy(x => x.ExecutionStepId)
            .ToArrayAsync(token)).Select(Map).ToArray());

    private static bool Invalid(string? value, int max) =>
        value is not null && (string.IsNullOrWhiteSpace(value) || value.Length > max);
    internal static ExecutionRunHistoryItem Map(ExecutionRunHistoryEntity x) =>
        new(x.ExecutionRunId, x.ExecutionPlanId, x.ManagedServerId,
            x.SourceDecisionPlanId, x.SourceCapabilitySnapshotId,
            x.SourceInventoryRunId, x.SourceInventoryVersion, x.QueuedAt,
            x.StartedAt, x.CompletedAt, x.DurationTicks, x.RecordedAt,
            x.ExecutionOutcome, x.TerminalState, x.FailureCategory, x.ReasonCode,
            x.WarningCount, x.AttemptCount, x.RetryCount, x.StepCount,
            x.CompletedStepCount, x.FailedStepCount, x.TimedOutStepCount,
            x.CancelledStepCount, x.SkippedStepCount, x.StrategyCode,
            x.StrategyVersion, x.PluginId, x.PluginVersion, x.TargetSdkVersion,
            x.RuntimeContractVersion, x.ExecutionPlanSchemaVersion,
            x.ExecutionStateSchemaVersion, x.ExecutionEventSchemaVersion,
            x.ExecutionMonitoringSchemaVersion, x.Subject, x.IsReadOnly,
            x.ArtifactFileCount, x.ArtifactObjectCount, x.ArtifactMetricCount,
            x.ArtifactByteCount, Enum.Parse<ExecutionHistoryProjectionStatus>(
                x.ProjectionStatus), Enum.Parse<ExecutionHistoryFailureCategory>(
                x.ProjectionFailureCategory), x.ProjectionReasonCode);
    private static ExecutionStepHistoryItem Map(ExecutionStepHistoryEntity x) =>
        new(x.ExecutionStepId, x.StepOrdinal, x.DependencyCount, x.StrategyCode,
            x.StrategyVersion, x.PluginId, x.PluginVersion, x.Subject, x.QueuedAt,
            x.StartedAt, x.CompletedAt, x.QueueDurationTicks, x.WaitDurationTicks,
            x.ExecutionDurationTicks, x.StepOutcome, x.FailureCategory, x.ReasonCode,
            x.AttemptCount, x.RetryCount, x.WasThrottled, x.WasSkipped,
            x.WasCancelled, x.WasTimedOut, x.ArtifactFileCount,
            x.ArtifactObjectCount, x.ArtifactMetricCount, x.ArtifactByteCount,
            x.WarningCount);
    private static ExecutionAttemptHistoryItem Map(ExecutionAttemptHistoryEntity x) =>
        new(x.ExecutionStepId, x.AttemptNumber, x.StartedAt, x.CompletedAt,
            x.DurationTicks, x.AttemptOutcome, x.FailureCategory, x.ReasonCode,
            x.RetryScheduled, x.RetryDelayTicks, x.CancellationObserved,
            x.TimeoutObserved, x.WarningCount);
    private static ExecutionStateTransitionHistoryItem Map(
        ExecutionStateTransitionHistoryEntity x) =>
        new(x.ExecutionStepId, x.TransitionSequence, x.EntityType, x.FromState,
            x.ToState, x.TransitionedAt, x.EventType, x.ReasonCode,
            x.FailureCategory, x.EventSchemaVersion);
    private static ExecutionArtifactHistoryItem Map(ExecutionArtifactHistoryEntity x) =>
        new(x.ExecutionStepId, x.ArtifactId, x.ArtifactSchemaVersion,
            x.ArtifactType, x.LogicalName, x.ContentType, x.ObjectCount,
            x.MetricCount, x.ByteCount, x.CreatedAt);
    private static ExecutionHistoryPolicyProvenance Map(ExecutionPolicyHistoryEntity x) =>
        new(x.ExecutionStepId, x.TimeoutPolicyCode, x.TimeoutPolicyVersion,
            x.TimeoutTicks, x.RetryPolicyCode, x.RetryPolicyVersion,
            x.MaximumAttempts, x.RetryDelayClassification, x.ParallelPolicyCode,
            x.ParallelPolicyVersion, x.ParallelMaximumConcurrency,
            x.ThrottlingPolicyCode, x.ThrottlingPolicyVersion,
            x.ThrottlingMaximumConcurrency, x.BatchingPolicyCode,
            x.BatchingPolicyVersion, x.BatchingEnabled);
}

public sealed class ExecutionHistoryRetentionService(
    OperationsDbContext context, TimeProvider timeProvider)
    : IExecutionHistoryRetentionService
{
    public ExecutionHistoryRetentionCutoffs GetCutoffs(
        ExecutionHistoryRetentionPolicy policy)
    {
        policy.Validate();
        DateTime now = timeProvider.GetLocalNow().DateTime;
        return new(now.AddDays(-policy.RunDays),
            now.AddDays(-policy.TransitionDays),
            now.AddDays(-policy.FailedProjectionDays));
    }

    public async Task<ExecutionHistoryRetentionResult> DeleteExpiredAsync(
        ExecutionHistoryRetentionPolicy policy, CancellationToken cancellationToken)
    {
        ExecutionHistoryRetentionCutoffs cutoffs = GetCutoffs(policy);
        Guid[] runIds = await context.ExecutionRunHistory.AsNoTracking()
            .Where(x => x.CompletedAt < cutoffs.RunCutoff)
            .OrderBy(x => x.CompletedAt).ThenBy(x => x.ExecutionRunId)
            .Select(x => x.ExecutionRunId).Take(policy.BatchSize)
            .ToArrayAsync(cancellationToken);
        if (runIds.Length == 0) return new(0, 0, 0, 0, 0, 0);
        ExecutionAttemptHistoryEntity[] attemptRows = await context.ExecutionAttemptHistory
            .Where(x => runIds.Contains(x.ExecutionRunId)).ToArrayAsync(cancellationToken);
        ExecutionStateTransitionHistoryEntity[] transitionRows =
            await context.ExecutionStateTransitionHistory
            .Where(x => runIds.Contains(x.ExecutionRunId)
                || x.TransitionedAt < cutoffs.TransitionCutoff)
            .Take(policy.BatchSize).ToArrayAsync(cancellationToken);
        ExecutionArtifactHistoryEntity[] artifactRows = await context.ExecutionArtifactHistory
            .Where(x => runIds.Contains(x.ExecutionRunId)).ToArrayAsync(cancellationToken);
        ExecutionPolicyHistoryEntity[] policyRows = await context.ExecutionPolicyHistory
            .Where(x => runIds.Contains(x.ExecutionRunId)).ToArrayAsync(cancellationToken);
        ExecutionStepHistoryEntity[] stepRows = await context.ExecutionStepHistory
            .Where(x => runIds.Contains(x.ExecutionRunId)).ToArrayAsync(cancellationToken);
        ExecutionRunHistoryEntity[] runRows = await context.ExecutionRunHistory
            .Where(x => runIds.Contains(x.ExecutionRunId)).ToArrayAsync(cancellationToken);
        context.RemoveRange(attemptRows); context.RemoveRange(transitionRows);
        context.RemoveRange(artifactRows); context.RemoveRange(policyRows);
        context.RemoveRange(stepRows); context.RemoveRange(runRows);
        await context.SaveChangesAsync(cancellationToken);
        return new(runRows.Length, stepRows.Length, attemptRows.Length,
            transitionRows.Length, artifactRows.Length, policyRows.Length);
    }
}
