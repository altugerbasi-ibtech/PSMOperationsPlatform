using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.CollectorSdk;
using PluginExecutionContext = PSMOperationsPlatform.CollectorSdk.ExecutionContext;

namespace PSMOperationsPlatform.Application.Runtime;

public interface ICollectorRuntime
{
    Task<CollectorRuntimeResult> ExecuteAsync(
        PreparedExecutionDispatch dispatch, CancellationToken cancellationToken);
}

public sealed class CollectorRuntime(
    IExecutionStateStore stateStore,
    IExecutionEventSink eventSink,
    TimeProvider timeProvider) : ICollectorRuntime
{
    private readonly SemaphoreSlim persistenceGate = new(1, 1);

    public async Task<CollectorRuntimeResult> ExecuteAsync(
        PreparedExecutionDispatch dispatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        CollectorRuntimeInput input = dispatch.Plan;
        Validate(input);
        Validate(dispatch);
        cancellationToken.ThrowIfCancellationRequested();
        DateTime now = timeProvider.GetLocalNow().DateTime;
        var run = new ExecutionRunState(dispatch.ExecutionRunId, input, now);
        var events = new RuntimeEventPublisher(eventSink, input, run.Id, timeProvider,
            dispatch.Steps);
        RuntimeFailureCategory eventFailure = RuntimeFailureCategory.None;
        var artifactMetadata = new ConcurrentDictionary<Guid, ExecutionArtifacts>();

        await CreateAsync(run, cancellationToken);
        eventFailure = await events.PublishAsync(ExecutionEventType.ExecutionRunCreated,
            null, null, run.Status.ToString(), RuntimeFailureCategory.None,
            CollectorRuntimeReasonCodes.RunCreated, "The execution run was created.",
            cancellationToken);
        run.Transition(ExecutionRunStatus.Queued);
        foreach (ExecutionStepState state in run.Steps)
        {
            state.Transition(ExecutionStepStatus.Queued);
            eventFailure = Combine(eventFailure, await events.PublishAsync(
                ExecutionEventType.ExecutionStepQueued, state, null, state.Status.ToString(),
                RuntimeFailureCategory.None, CollectorRuntimeReasonCodes.StepQueued,
                "The execution step was queued.", cancellationToken));
        }
        await SaveAsync(run, cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            run.Transition(ExecutionRunStatus.Running);
            run.StartedAt = timeProvider.GetLocalNow().DateTime;
            await SaveAsync(run, cancellationToken);
            eventFailure = Combine(eventFailure, await events.PublishAsync(
                ExecutionEventType.ExecutionRunStarted, null, null, run.Status.ToString(),
                RuntimeFailureCategory.None, CollectorRuntimeReasonCodes.RunStarted,
                "The execution run started.", cancellationToken));

            await ExecuteScheduledAsync(
                dispatch, run, events, artifactMetadata, cancellationToken);
            run.CompletedAt = timeProvider.GetLocalNow().DateTime;
            run.TotalDuration = run.CompletedAt.Value - run.QueuedAt;
            bool failures = run.Steps.Any(x => x.Status is ExecutionStepStatus.Failed
                or ExecutionStepStatus.TimedOut or ExecutionStepStatus.Skipped);
            run.Transition(failures
                ? ExecutionRunStatus.CompletedWithFailures : ExecutionRunStatus.Completed);
            run.ReasonCode = failures
                ? CollectorRuntimeReasonCodes.RunCompletedWithFailures
                : CollectorRuntimeReasonCodes.RunCompleted;
            await SaveAsync(run, cancellationToken);
            eventFailure = Combine(eventFailure, await events.PublishAsync(
                ExecutionEventType.ExecutionRunCompleted, null, null, run.Status.ToString(),
                RuntimeFailureCategory.None, run.ReasonCode,
                failures ? "The run completed with isolated step failures."
                    : "The run completed successfully.", cancellationToken));
            return Result(run, eventFailure, artifactMetadata);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            DateTime cancelledAt = timeProvider.GetLocalNow().DateTime;
            foreach (ExecutionStepState step in run.Steps.Where(x => !Terminal(x.Status)))
            {
                if (step.Status == ExecutionStepStatus.Running)
                    step.Transition(ExecutionStepStatus.Cancelled);
                else if (step.Status is ExecutionStepStatus.Queued
                         or ExecutionStepStatus.WaitingForDependency
                         or ExecutionStepStatus.WaitingForThrottle)
                    step.Transition(ExecutionStepStatus.Cancelled);
                step.CancelledAt = cancelledAt;
                step.CompletedAt = cancelledAt;
                step.FailureCategory = RuntimeFailureCategory.Cancellation;
                step.ReasonCode = CollectorRuntimeReasonCodes.StepCancelled;
            }
            if (run.Status is ExecutionRunStatus.Created or ExecutionRunStatus.Queued or ExecutionRunStatus.Running)
                run.Transition(ExecutionRunStatus.Cancelled);
            run.CancelledAt = cancelledAt;
            run.CompletedAt = cancelledAt;
            run.TotalDuration = cancelledAt - run.QueuedAt;
            run.FailureCategory = RuntimeFailureCategory.Cancellation;
            run.ReasonCode = CollectorRuntimeReasonCodes.RunCancelled;
            await SaveAsync(run, CancellationToken.None);
            await events.PublishAsync(ExecutionEventType.ExecutionRunCancelled, null, null,
                run.Status.ToString(), RuntimeFailureCategory.Cancellation,
                CollectorRuntimeReasonCodes.RunCancelled, "The execution run was cancelled.",
                CancellationToken.None);
            throw;
        }
        catch (Exception)
        {
            DateTime failedAt = timeProvider.GetLocalNow().DateTime;
            if (run.Status is ExecutionRunStatus.Created or ExecutionRunStatus.Queued
                or ExecutionRunStatus.Running)
                run.Transition(ExecutionRunStatus.Failed);
            run.CompletedAt = failedAt;
            run.TotalDuration = failedAt - run.QueuedAt;
            run.FailureCategory = RuntimeFailureCategory.Unexpected;
            run.ReasonCode = "ExecutionRunFailed";
            run.FailureSummary = "The runtime could not continue reliably.";
            try
            {
                await SaveAsync(run, CancellationToken.None);
                await events.PublishAsync(ExecutionEventType.ExecutionRunFailed, null,
                    null, run.Status.ToString(), RuntimeFailureCategory.Unexpected,
                    "ExecutionRunFailed", "The execution run failed safely.",
                    CancellationToken.None);
            }
            catch (Exception)
            {
                // The original orchestration failure remains authoritative.
            }
            return Result(run, eventFailure, artifactMetadata);
        }
    }

    private async Task ExecuteScheduledAsync(PreparedExecutionDispatch dispatch,
        ExecutionRunState run, RuntimeEventPublisher events,
        ConcurrentDictionary<Guid, ExecutionArtifacts> artifactMetadata,
        CancellationToken cancellationToken)
    {
        var source = dispatch.Steps.ToDictionary(x => x.Step.StrategyCode, StringComparer.Ordinal);
        while (run.Steps.Any(x => !Terminal(x.Status)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool progressed = false;
            var executable = new List<(PreparedExecutionStep Prepared, ExecutionStepState State)>();
            foreach (ExecutionStepState state in run.Steps
                         .Where(x => x.Status is ExecutionStepStatus.Queued or ExecutionStepStatus.WaitingForDependency)
                         .OrderBy(x => x.QueueSequence).ToArray())
            {
                PreparedExecutionStep prepared = source[state.StrategyCode];
                CollectorRuntimeStep step = prepared.Step;
                ExecutionStepState[] dependencyStates = step.DependencyStrategyCodes
                    .Select(code => run.Steps.SingleOrDefault(x => x.StrategyCode == code)
                        ?? throw new InvalidOperationException("DependencyMissing")).ToArray();
                if (dependencyStates.Any(x => x.Status is ExecutionStepStatus.Failed
                    or ExecutionStepStatus.TimedOut or ExecutionStepStatus.Cancelled
                    or ExecutionStepStatus.Skipped))
                {
                    state.Transition(ExecutionStepStatus.Skipped);
                    state.CompletedAt = timeProvider.GetLocalNow().DateTime;
                    state.FailureCategory = RuntimeFailureCategory.DependencyFailure;
                    state.ReasonCode = CollectorRuntimeReasonCodes.DependencyFailed;
                    await SaveAsync(run, cancellationToken);
                    await events.PublishAsync(ExecutionEventType.ExecutionStepSkipped, state,
                        null, state.Status.ToString(), state.FailureCategory, state.ReasonCode,
                        "The step was skipped because a required dependency did not complete.",
                        cancellationToken);
                    progressed = true;
                    continue;
                }
                if (dependencyStates.Any(x => x.Status != ExecutionStepStatus.Completed))
                {
                    if (state.Status == ExecutionStepStatus.Queued)
                    {
                        state.Transition(ExecutionStepStatus.WaitingForDependency);
                        await SaveAsync(run, cancellationToken);
                        await events.PublishAsync(ExecutionEventType.ExecutionStepWaiting,
                            state, null, state.Status.ToString(), RuntimeFailureCategory.None,
                            "WaitingForDependency", "The step is waiting for a dependency.",
                            cancellationToken);
                    }
                    continue;
                }
                executable.Add((prepared, state));
            }
            if (executable.Count > 0)
            {
                (PreparedExecutionStep Prepared, ExecutionStepState State) first = executable[0];
                int maximum = Math.Min(first.Prepared.Policy.Parallel.MaximumConcurrency,
                    first.Prepared.Policy.Throttling.MaximumConcurrency);
                (PreparedExecutionStep Prepared, ExecutionStepState State)[] batch = executable
                    .Where(x => string.Equals(x.Prepared.Step.ParallelGroupCode,
                        first.Prepared.Step.ParallelGroupCode, StringComparison.Ordinal))
                    .Take(maximum).ToArray();
                Task[] bounded = batch.Select(x => ExecuteStepAsync(x.Prepared,
                    x.State, run, events, artifactMetadata, cancellationToken)).ToArray();
                await Task.WhenAll(bounded);
                progressed = true;
            }
            if (!progressed)
                throw new InvalidOperationException("DependencyCycle");
        }
    }

    private async Task ExecuteStepAsync(PreparedExecutionStep prepared,
        ExecutionStepState state, ExecutionRunState run, RuntimeEventPublisher events,
        ConcurrentDictionary<Guid, ExecutionArtifacts> artifactMetadata,
        CancellationToken cancellationToken)
    {
        CollectorRuntimeStep step = prepared.Step;
        ExecutionPolicy policy = prepared.Policy;
        CollectorPluginDescriptor descriptor = prepared.Descriptor;
        ICollectorPlugin plugin = prepared.Plugin;

        state.PluginVersion = descriptor.PluginVersion;
        state.EligibleAt = timeProvider.GetLocalNow().DateTime;
        state.QueueDuration = state.EligibleAt.Value - state.QueuedAt;
        state.Transition(ExecutionStepStatus.WaitingForThrottle);
        await SaveAsync(run, cancellationToken);
        await events.PublishAsync(ExecutionEventType.ExecutionStepWaiting, state, null,
            state.Status.ToString(), RuntimeFailureCategory.None, "WaitingForThrottle",
            "The step is waiting for bounded runtime capacity.", cancellationToken);

        // Scheduling is deliberately conservative: one coordinator processes a plan at a
        // time. The explicit resolved limits are enforced as upper bounds and remain ready
        // for bounded parallel scheduling without changing the immutable plan.
        DateTime started = timeProvider.GetLocalNow().DateTime;
        state.WaitDuration = started - state.EligibleAt.Value;
        state.StartedAt = started;
        state.Transition(ExecutionStepStatus.Running);
        await SaveAsync(run, cancellationToken);
        await events.PublishAsync(ExecutionEventType.ExecutionStepStarted, state, null,
            state.Status.ToString(), RuntimeFailureCategory.None,
            CollectorRuntimeReasonCodes.StepStarted, "The execution step started.",
            cancellationToken);

        for (int attemptNumber = 1; attemptNumber <= policy.Retry.MaxAttempts; attemptNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attempt = new ExecutionAttemptState(Guid.NewGuid(), attemptNumber,
                attemptNumber > 1, timeProvider.GetLocalNow().DateTime);
            state.AddAttempt(attempt);
            await SaveAsync(run, cancellationToken);
            await events.PublishAsync(ExecutionEventType.ExecutionStepAttemptStarted,
                state, attempt, attempt.Status.ToString(), RuntimeFailureCategory.None,
                "AttemptStarted", "The handler attempt started.", cancellationToken);

            AttemptOutcome outcome = await InvokeAsync(prepared.Context, state, attempt,
                plugin, prepared.Policy, cancellationToken);
            await SaveAsync(run, cancellationToken);
            await events.PublishAsync(ExecutionEventType.ExecutionStepAttemptCompleted,
                state, attempt, attempt.Status.ToString(), outcome.FailureCategory,
                outcome.ReasonCode, outcome.Message, cancellationToken);
            if (outcome.Success)
            {
                artifactMetadata[state.ExecutionPlanStepId] = outcome.Artifacts;
                CompleteStep(state, outcome, timeProvider.GetLocalNow().DateTime);
                await SaveAsync(run, cancellationToken);
                await events.PublishAsync(ExecutionEventType.ExecutionStepCompleted,
                    state, attempt, state.Status.ToString(), RuntimeFailureCategory.None,
                    CollectorRuntimeReasonCodes.StepCompleted,
                    "The execution step completed successfully.", cancellationToken);
                return;
            }
            bool retry = attemptNumber < policy.Retry.MaxAttempts
                && policy.Retry.RetryableFailureCategories.Contains(
                    outcome.FailureCategory.ToString())
                && !cancellationToken.IsCancellationRequested;
            if (retry)
            {
                await events.PublishAsync(ExecutionEventType.ExecutionStepRetryScheduled,
                    state, attempt, state.Status.ToString(), outcome.FailureCategory,
                    CollectorRuntimeReasonCodes.RetryScheduled,
                    "A deterministic retry was scheduled.", cancellationToken);
                TimeSpan delay = policy.Retry.DelaySchedule[
                    Math.Min(attemptNumber - 1, policy.Retry.DelaySchedule.Count - 1)];
                await Task.Delay(delay, timeProvider, cancellationToken);
                continue;
            }
            FailStep(state, outcome, timeProvider.GetLocalNow().DateTime);
            await SaveAsync(run, cancellationToken);
            await events.PublishAsync(outcome.FailureCategory == RuntimeFailureCategory.Timeout
                    ? ExecutionEventType.ExecutionStepTimedOut : ExecutionEventType.ExecutionStepFailed,
                state, attempt, state.Status.ToString(), outcome.FailureCategory,
                outcome.ReasonCode, outcome.Message, cancellationToken);
            return;
        }
    }

    private async Task<AttemptOutcome> InvokeAsync(PluginExecutionContext context,
        ExecutionStepState state, ExecutionAttemptState attempt,
        ICollectorPlugin plugin, ExecutionPolicy policy,
        CancellationToken externalCancellation)
    {
        long started = timeProvider.GetTimestamp();
        using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation);
        try
        {
            Task<CollectorExecutionResult> operation =
                plugin.ExecuteAsync(context, policy, attemptCancellation.Token);
            CollectorExecutionResult result = await operation.WaitAsync(
                policy.Timeout.Timeout, timeProvider, externalCancellation);
            ValidateResult(result);
            TimeSpan duration = timeProvider.GetElapsedTime(started);
            bool success = result.Outcome is CollectorExecutionOutcome.Success
                or CollectorExecutionOutcome.NoData;
            RuntimeFailureCategory category = success
                ? RuntimeFailureCategory.None : result.Outcome == CollectorExecutionOutcome.Cancelled
                    ? RuntimeFailureCategory.Cancellation
                    : RuntimeFailureCategory.HandlerExecutionFailure;
            ExecutionAttemptStatus status = success ? ExecutionAttemptStatus.Completed
                : result.Outcome == CollectorExecutionOutcome.Cancelled
                    ? ExecutionAttemptStatus.Cancelled : ExecutionAttemptStatus.Failed;
            attempt.Complete(status, timeProvider.GetLocalNow().DateTime, duration, category,
                result.ReasonCode, success ? null : result.Summary,
                result.BytesCollected, result.ObjectsCollected);
            return new(success, category, result.ReasonCode, result.Summary,
                result.BytesCollected, result.ObjectsCollected, duration,
                result.Artifacts);
        }
        catch (TimeoutException)
        {
            attemptCancellation.Cancel();
            TimeSpan duration = timeProvider.GetElapsedTime(started);
            attempt.Complete(ExecutionAttemptStatus.TimedOut,
                timeProvider.GetLocalNow().DateTime, duration, RuntimeFailureCategory.Timeout,
                CollectorRuntimeReasonCodes.StepTimedOut,
                "The handler exceeded its assigned timeout.", null, null);
            return new(false, RuntimeFailureCategory.Timeout,
                CollectorRuntimeReasonCodes.StepTimedOut,
                "The handler exceeded its assigned timeout.", 0, 0, duration,
                ExecutionArtifacts.Empty);
        }
        catch (OperationCanceledException) when (externalCancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            TimeSpan duration = timeProvider.GetElapsedTime(started);
            attempt.Complete(ExecutionAttemptStatus.Failed,
                timeProvider.GetLocalNow().DateTime, duration,
                RuntimeFailureCategory.HandlerExecutionFailure,
                CollectorRuntimeReasonCodes.StepFailed,
                "The handler failed without exposing exception details.", null, null);
            return new(false, RuntimeFailureCategory.HandlerExecutionFailure,
                CollectorRuntimeReasonCodes.StepFailed,
                "The handler failed without exposing exception details.", 0, 0, duration,
                ExecutionArtifacts.Empty);
        }
    }

    private static void ValidateResult(CollectorExecutionResult result)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.ReasonCode)
            || string.IsNullOrWhiteSpace(result.Summary)
            || result.BytesCollected < 0 || result.ObjectsCollected < 0
            || result.Warnings is null || result.Diagnostics is null)
            throw new InvalidOperationException(CollectorRuntimeReasonCodes.InvalidHandlerResult);
        try { result.Validate(); }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(CollectorRuntimeReasonCodes.InvalidHandlerResult);
        }
    }

    private async Task FailBeforeAttempt(ExecutionStepState state, ExecutionRunState run,
        RuntimeEventPublisher events, RuntimeFailureCategory category, string reason,
        string message, CancellationToken cancellationToken)
    {
        state.Transition(ExecutionStepStatus.Running);
        state.Transition(ExecutionStepStatus.Failed);
        state.CompletedAt = timeProvider.GetLocalNow().DateTime;
        state.FailureCategory = category; state.ReasonCode = reason; state.FailureSummary = message;
        await SaveAsync(run, cancellationToken);
        await events.PublishAsync(ExecutionEventType.ExecutionStepFailed, state, null,
            state.Status.ToString(), category, reason, message, cancellationToken);
    }

    private static void CompleteStep(ExecutionStepState state, AttemptOutcome outcome, DateTime completed)
    {
        state.Transition(ExecutionStepStatus.Completed);
        state.CompletedAt = completed;
        state.ExecutionDuration = state.Attempts.Aggregate(TimeSpan.Zero, (total, x) => total + x.Duration);
        state.TotalDuration = completed - state.QueuedAt;
        state.BytesCollected = outcome.Bytes;
        state.ObjectsCollected = outcome.Objects;
        state.ReasonCode = CollectorRuntimeReasonCodes.StepCompleted;
    }

    private static void FailStep(ExecutionStepState state, AttemptOutcome outcome, DateTime completed)
    {
        ExecutionStepStatus status = outcome.FailureCategory == RuntimeFailureCategory.Timeout
            ? ExecutionStepStatus.TimedOut : outcome.FailureCategory == RuntimeFailureCategory.Cancellation
                ? ExecutionStepStatus.Cancelled : ExecutionStepStatus.Failed;
        state.Transition(status);
        state.CompletedAt = completed;
        if (status == ExecutionStepStatus.TimedOut) state.TimedOutAt = completed;
        if (status == ExecutionStepStatus.Cancelled) state.CancelledAt = completed;
        state.ExecutionDuration = state.Attempts.Aggregate(TimeSpan.Zero, (total, x) => total + x.Duration);
        state.TotalDuration = completed - state.QueuedAt;
        state.FailureCategory = outcome.FailureCategory;
        state.ReasonCode = outcome.ReasonCode;
        state.FailureSummary = outcome.Message;
    }

    private static void Validate(CollectorRuntimeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ManagedServerId == Guid.Empty || input.ExecutionPlanId == Guid.Empty
            || input.ExecutionPlanSchemaVersion != ExecutionPlanEngine.SchemaVersion
            || input.PlanStatus is ExecutionPlanStatus.Invalid
            || input.SourceDecisionPlanId == Guid.Empty
            || input.SourceCapabilitySnapshotId == Guid.Empty
            || input.SourceInventoryRunId == Guid.Empty || input.SourceInventoryVersion < 1
            || input.PlanCreatedAt == default || input.RequestedAt == default
            || input.Steps is null || input.Exclusions is null)
            throw new ArgumentException(CollectorRuntimeReasonCodes.InvalidRuntimeInput, nameof(input));
        CollectorRuntimeStep[] steps = input.Steps.ToArray();
        if (steps.Select(x => x.StrategyCode).Distinct(StringComparer.Ordinal).Count() != steps.Length
            || steps.Select(x => x.ExecutionPlanStepId).Distinct().Count() != steps.Length
            || steps.Any(x => x.ExecutionPlanStepId == Guid.Empty
                || string.IsNullOrWhiteSpace(x.StrategyCode) || x.StrategyVersion < 1
                || x.Subject != Decisions.DecisionSubject.ManagedTargetServer
                || x.StepSequence < 1 || x.Priority < 1 || x.ExecutionOrder < 1
                || !x.IsReadOnly || x.RequiresManualApproval
                || x.DependencyStrategyCodes is null)
            || steps.Select(x => x.StepSequence).Distinct().Count() != steps.Length
            || input.Exclusions.Select(x => x.StrategyCode)
                .Intersect(steps.Select(x => x.StrategyCode), StringComparer.Ordinal).Any())
            throw new ArgumentException(CollectorRuntimeReasonCodes.InvalidRuntimeInput, nameof(input));
        var codes = steps.Select(x => x.StrategyCode).ToHashSet(StringComparer.Ordinal);
        if (steps.Any(x => x.DependencyStrategyCodes.Contains(x.StrategyCode, StringComparer.Ordinal)
            || x.DependencyStrategyCodes.Any(dependency => !codes.Contains(dependency))))
            throw new ArgumentException(CollectorRuntimeReasonCodes.InvalidRuntimeInput, nameof(input));
    }

    private static void Validate(PreparedExecutionDispatch dispatch)
    {
        if (dispatch.ExecutionRunId == Guid.Empty || dispatch.Steps is null
            || dispatch.Steps.Count != dispatch.Plan.Steps.Count
            || dispatch.Steps.Select(x => x.Step.StrategyCode)
                .Distinct(StringComparer.Ordinal).Count() != dispatch.Steps.Count
            || dispatch.Steps.Any(x => x.Plugin is null || x.Descriptor is null
                || x.Policy is null || x.Context is null
                || x.Context.ExecutionRunId != dispatch.ExecutionRunId
                || x.Context.ExecutionPlanId != dispatch.Plan.ExecutionPlanId
                || !string.Equals(x.Step.StrategyCode, x.Descriptor.StrategyCode,
                    StringComparison.Ordinal)))
            throw new ArgumentException("PreparedExecutionDispatchInvalid", nameof(dispatch));
    }

    private static bool Terminal(ExecutionStepStatus status) =>
        status is ExecutionStepStatus.Completed or ExecutionStepStatus.Failed
            or ExecutionStepStatus.Cancelled or ExecutionStepStatus.TimedOut
            or ExecutionStepStatus.Skipped;
    private static RuntimeFailureCategory Combine(RuntimeFailureCategory current,
        RuntimeFailureCategory next) => current == RuntimeFailureCategory.None ? next : current;
    private async Task CreateAsync(ExecutionRunState run, CancellationToken cancellationToken)
    {
        await persistenceGate.WaitAsync(cancellationToken);
        try { await stateStore.CreateAsync(run, cancellationToken); }
        finally { persistenceGate.Release(); }
    }
    private async Task SaveAsync(ExecutionRunState run, CancellationToken cancellationToken)
    {
        await persistenceGate.WaitAsync(cancellationToken);
        try { await stateStore.SaveAsync(run, cancellationToken); }
        finally { persistenceGate.Release(); }
    }
    private sealed record AttemptOutcome(bool Success, RuntimeFailureCategory FailureCategory,
        string ReasonCode, string Message, long Bytes, long Objects, TimeSpan Duration,
        ExecutionArtifacts Artifacts);

    private static CollectorRuntimeResult Result(ExecutionRunState run,
        RuntimeFailureCategory eventFailure,
        ConcurrentDictionary<Guid, ExecutionArtifacts> artifacts) =>
        new(run, eventFailure, new ReadOnlyDictionary<Guid, ExecutionArtifacts>(
            artifacts.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value)));
}

internal sealed class RuntimeEventPublisher(
    IExecutionEventSink sink, CollectorRuntimeInput input, Guid runId, TimeProvider timeProvider,
    IReadOnlyList<PreparedExecutionStep> preparedSteps)
{
    private long sequence;
    private readonly IReadOnlyDictionary<string, PreparedExecutionStep> prepared =
        preparedSteps.ToDictionary(x => x.Step.StrategyCode, StringComparer.Ordinal);

    public async Task<RuntimeFailureCategory> PublishAsync(ExecutionEventType type,
        ExecutionStepState? step, ExecutionAttemptState? attempt, string status,
        RuntimeFailureCategory category, string reason, string message,
        CancellationToken cancellationToken)
    {
        var executionEvent = new ExecutionEvent(Guid.NewGuid(),
            ExecutionEventSchemaVersion.Value, Interlocked.Increment(ref sequence),
            type, input.ManagedServerId, input.ExecutionPlanId, runId,
            step?.ExecutionPlanStepId, step?.StrategyCode,
            step is null ? null : prepared[step.StrategyCode].Descriptor.PluginId,
            step?.PluginVersion,
            attempt?.AttemptNumber,
            timeProvider.GetLocalNow().DateTime, attempt?.Duration, status, category,
            reason, message, input.SourceDecisionPlanId, input.SourceCapabilitySnapshotId,
            input.SourceInventoryRunId, input.SourceInventoryVersion);
        try
        {
            await sink.PublishAsync(executionEvent, cancellationToken);
            return RuntimeFailureCategory.None;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return RuntimeFailureCategory.EventPublicationFailure;
        }
    }
}
