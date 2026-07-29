using System.Collections.ObjectModel;
using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.CollectorSdk;
using PluginExecutionContext = PSMOperationsPlatform.CollectorSdk.ExecutionContext;

namespace PSMOperationsPlatform.Application.Runtime;

public sealed record ExecutionDispatchRequest(CollectorRuntimeInput Plan);

public sealed record PreparedExecutionStep(
    CollectorRuntimeStep Step,
    ICollectorPlugin Plugin,
    CollectorPluginDescriptor Descriptor,
    ExecutionPolicy Policy,
    PluginExecutionContext Context);

public sealed record PreparedExecutionDispatch(
    Guid ExecutionRunId,
    CollectorRuntimeInput Plan,
    IReadOnlyList<PreparedExecutionStep> Steps);

public sealed record ExecutionDispatchResult(
    ExecutionDispatchDisposition Disposition,
    DispatchFailureCategory FailureCategory,
    string ReasonCode,
    string Explanation,
    CollectorRuntimeResult? RuntimeResult,
    ExecutionDispatchDiagnostic Diagnostic);

public sealed record ExecutionDispatchDiagnostic(
    string DispatchStatus,
    string? StrategyCode,
    string? PluginId,
    int? PluginVersion,
    string? TargetSdkVersion,
    string RuntimeVersion,
    IReadOnlyList<string> PolicyReferences,
    string? IncompatibleCapability,
    IReadOnlyList<string> SatisfiedChecks,
    IReadOnlyList<string> FailedChecks);

public interface IExecutionDispatcher
{
    Task<ExecutionDispatchResult> DispatchAsync(
        ExecutionDispatchRequest request, CancellationToken cancellationToken);
}

public interface IPluginPolicyCompatibilityValidator
{
    DispatchFailureCategory Validate(CollectorPluginDescriptor descriptor, ExecutionPolicy policy);
}

public sealed class PluginPolicyCompatibilityValidator : IPluginPolicyCompatibilityValidator
{
    public DispatchFailureCategory Validate(
        CollectorPluginDescriptor descriptor, ExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(policy);
        if (!descriptor.IsReadOnly) return DispatchFailureCategory.HandlerReadOnlyViolation;
        if (!descriptor.SupportsCancellation) return DispatchFailureCategory.CancellationCapabilityUnsupported;
        if (policy.Timeout.Timeout > TimeSpan.Zero && !descriptor.SupportsTimeout)
            return DispatchFailureCategory.TimeoutCapabilityUnsupported;
        if (descriptor.SupportsTimeout && !descriptor.SupportsCancellation)
            return DispatchFailureCategory.CancellationCapabilityUnsupported;
        if (policy.Retry.MaxAttempts > 1 && !descriptor.SupportsRetry)
            return DispatchFailureCategory.RetryCapabilityUnsupported;
        if (policy.Parallel.MaximumConcurrency > 1 && !descriptor.SupportsParallelExecution)
            return DispatchFailureCategory.ParallelCapabilityUnsupported;
        if (policy.Batching.Enabled && !descriptor.SupportsBatchExecution)
            return DispatchFailureCategory.BatchCapabilityUnsupported;
        return DispatchFailureCategory.None;
    }
}

public sealed class ExecutionDispatcher(
    ICollectorPluginRegistry plugins,
    IExecutionPolicyCatalog policies,
    IRuntimePluginCompatibilityMatrix sdkCompatibility,
    IPluginPolicyCompatibilityValidator compatibility,
    ICollectorRuntime runtime,
    IExecutionEventSink events,
    TimeProvider timeProvider) : IExecutionDispatcher
{
    public async Task<ExecutionDispatchResult> DispatchAsync(
        ExecutionDispatchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        CollectorRuntimeInput input = request.Plan
            ?? throw new ArgumentException("DispatchRequestInvalid", nameof(request));
        Guid runId = Guid.NewGuid();
        long sequence = 0;
        await Publish(ExecutionEventType.ExecutionDispatchRequested, null, null, null,
            "Requested", "ExecutionDispatchRequested", "Execution dispatch was requested.");

        try
        {
            Validate(input);
            var prepared = new List<PreparedExecutionStep>(input.Steps.Count);
            foreach (CollectorRuntimeStep step in input.Steps.OrderBy(x => x.StepSequence))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!plugins.TryResolve(step.StrategyCode, out ICollectorPlugin? plugin))
                    return await Reject(DispatchFailureCategory.HandlerNotFound,
                        "HandlerNotFound", "No explicitly registered handler matches the strategy.", step);
                CollectorPluginDescriptor descriptor = plugin!.Describe().Normalize();
                try { descriptor.Validate(); }
                catch (ArgumentException)
                {
                    return await Reject(DispatchFailureCategory.HandlerDescriptorInvalid,
                        "HandlerDescriptorInvalid", "The handler descriptor is invalid.", step);
                }
                if (!string.Equals(descriptor.StrategyCode, step.StrategyCode, StringComparison.Ordinal))
                    return await Reject(DispatchFailureCategory.HandlerDescriptorInvalid,
                        "HandlerStrategyMismatch", "The handler descriptor does not match the strategy.", step);
                if (!descriptor.SupportedSubjects.Contains(ToPluginSubject(step.Subject)))
                    return await Reject(DispatchFailureCategory.HandlerSubjectMismatch,
                        "HandlerSubjectMismatch", "The handler does not support the plan subject.", step);
                await Publish(ExecutionEventType.ExecutionHandlerResolved, step,
                    descriptor.PluginId, descriptor.PluginVersion, "Resolved",
                    "ExecutionHandlerResolved", "The execution handler was resolved.");
                PluginCompatibilityResult sdkResult = sdkCompatibility.Evaluate(
                    CollectorRuntimeVersions.RuntimeVersion, descriptor);
                if (sdkResult.Status != PluginCompatibilityStatus.Compatible)
                    return await Reject(DispatchFailureCategory.PluginSdkVersionUnsupported,
                        sdkResult.ReasonCode, sdkResult.Explanation, step, descriptor,
                        "SdkCompatibility");

                ExecutionPolicy policy;
                try { policy = policies.Resolve(step); }
                catch (ExecutionPolicyException exception)
                {
                    DispatchFailureCategory category = exception.Category
                        == RuntimeFailureCategory.ExecutionPolicyVersionUnsupported
                        ? DispatchFailureCategory.ExecutionPolicyVersionUnsupported
                        : DispatchFailureCategory.ExecutionPolicyNotFound;
                    return await Reject(category, category.ToString(),
                        "The referenced execution policy could not be resolved.", step);
                }
                await Publish(ExecutionEventType.ExecutionPolicyResolved, step,
                    descriptor.PluginId, descriptor.PluginVersion, "Resolved",
                    "ExecutionPolicyResolved", "The execution policy was resolved.");
                DispatchFailureCategory incompatible = compatibility.Validate(descriptor, policy);
                if (incompatible != DispatchFailureCategory.None)
                    return await Reject(incompatible, incompatible.ToString(),
                        "The plugin capabilities are incompatible with the execution policy.",
                        step, descriptor, incompatible.ToString());
                var context = new PluginExecutionContext(input.ManagedServerId, input.TargetFqdn,
                    input.ExecutionPlanId, runId, step.ExecutionPlanStepId, step.StrategyCode,
                    step.StrategyVersion, descriptor.PluginId, descriptor.PluginVersion,
                    ToPluginSubject(step.Subject), input.SourceDecisionPlanId,
                    input.SourceCapabilitySnapshotId,
                    input.SourceInventoryRunId, input.SourceInventoryVersion,
                    input.ExecutionPlanSchemaVersion, policy.PolicySchemaVersion,
                    descriptor.DescriptorSchemaVersion, ExecutionEventSchemaVersion.Value,
                    timeProvider);
                var validationContext = new CollectorPluginValidationContext(context, policy,
                    CollectorRuntimeVersions.RuntimeVersion,
                    CollectorPluginContractVersions.ArtifactSchemaVersion);
                CollectorPluginValidationResult validation = plugin.Validate(validationContext);
                if (!validation.IsValid)
                    return await Reject(DispatchFailureCategory.PluginValidationFailure,
                        "PluginValidationFailed",
                        "The plugin did not pass deterministic pre-execution validation.",
                        step, descriptor, "PluginValidation");
                prepared.Add(new(step, plugin, descriptor, policy, context));
            }

            var dispatch = new PreparedExecutionDispatch(runId, input,
                new ReadOnlyCollection<PreparedExecutionStep>(prepared));
            await Publish(ExecutionEventType.ExecutionDispatchPrepared, null, null, null,
                "Prepared", "ExecutionDispatchPrepared", "The execution dispatch was prepared.");
            cancellationToken.ThrowIfCancellationRequested();
            await Publish(ExecutionEventType.ExecutionDispatchSubmitted, null, null, null,
                "Submitted", "ExecutionDispatchSubmitted", "The execution dispatch was submitted.");
            CollectorRuntimeResult result = await runtime.ExecuteAsync(dispatch, cancellationToken);
            return new(ExecutionDispatchDisposition.Submitted, DispatchFailureCategory.None,
                "ExecutionDispatchSubmitted", "The prepared dispatch was submitted.", result,
                Diagnostic("Submitted", null, null, null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArgumentException)
        {
            return await Reject(DispatchFailureCategory.DispatchRequestInvalid,
                "DispatchRequestInvalid", "The dispatch request is invalid.", null);
        }
        catch (Exception)
        {
            return await Reject(DispatchFailureCategory.DispatchPreparationFailure,
                "DispatchPreparationFailure", "The dispatch could not be prepared safely.", null);
        }

        async Task<ExecutionDispatchResult> Reject(DispatchFailureCategory category,
            string reason, string explanation, CollectorRuntimeStep? step,
            CollectorPluginDescriptor? descriptor = null, string? failedCheck = null)
        {
            await Publish(ExecutionEventType.ExecutionDispatchRejected, step, null, null,
                "Rejected", reason, explanation);
            return new(ExecutionDispatchDisposition.Rejected, category, reason, explanation, null,
                Diagnostic("Rejected", step, descriptor, failedCheck));
        }

        ExecutionDispatchDiagnostic Diagnostic(string status, CollectorRuntimeStep? step,
            CollectorPluginDescriptor? descriptor, string? failedCheck) =>
            new(status, step?.StrategyCode, descriptor?.PluginId, descriptor?.PluginVersion,
                descriptor?.TargetSdkVersion.ToString(), CollectorRuntimeVersions.RuntimeVersion,
                step is null ? Array.AsReadOnly(Array.Empty<string>()) : Array.AsReadOnly([
                    $"{step.TimeoutPolicyCode}:{step.TimeoutPolicyVersion}",
                    $"{step.RetryPolicyCode}:{step.RetryPolicyVersion}",
                    $"{step.ParallelGroupCode}:1",
                    $"{step.ThrottlingClass}:1"]),
                failedCheck, Array.AsReadOnly(descriptor is null
                    ? Array.Empty<string>() : ["DescriptorValidated"]),
                Array.AsReadOnly(failedCheck is null ? Array.Empty<string>() : [failedCheck]));

        async Task Publish(ExecutionEventType type, CollectorRuntimeStep? step,
            string? pluginId, int? pluginVersion, string status, string reason, string message)
        {
            var value = new ExecutionEvent(Guid.NewGuid(), ExecutionEventSchemaVersion.Value,
                Interlocked.Increment(ref sequence), type, input.ManagedServerId,
                input.ExecutionPlanId, runId, step?.ExecutionPlanStepId, step?.StrategyCode,
                pluginId, pluginVersion, null, timeProvider.GetLocalNow().DateTime, null, status,
                RuntimeFailureCategory.None, reason, message, input.SourceDecisionPlanId,
                input.SourceCapabilitySnapshotId, input.SourceInventoryRunId,
                input.SourceInventoryVersion);
            try { await events.PublishAsync(value, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { /* State/result remains authoritative; delivery is best effort. */ }
        }
    }

    private static CollectorPluginSubject ToPluginSubject(DecisionSubject subject) =>
        subject == DecisionSubject.ManagedTargetServer
            ? CollectorPluginSubject.ManagedTargetServer
            : throw new ArgumentException("HandlerSubjectMismatch", nameof(subject));

    private static void Validate(CollectorRuntimeInput input)
    {
        if (input.ManagedServerId == Guid.Empty || input.ExecutionPlanId == Guid.Empty
            || input.ExecutionPlanSchemaVersion != ExecutionPlanEngine.SchemaVersion
            || input.PlanStatus is ExecutionPlanStatus.Invalid
            || input.SourceDecisionPlanId == Guid.Empty
            || input.SourceCapabilitySnapshotId == Guid.Empty
            || input.SourceInventoryRunId == Guid.Empty || input.SourceInventoryVersion < 1
            || input.Steps is null || input.Exclusions is null)
            throw new ArgumentException("DispatchRequestInvalid", nameof(input));
        CollectorRuntimeStep[] steps = input.Steps.ToArray();
        if (steps.Select(x => x.StrategyCode).Distinct(StringComparer.Ordinal).Count() != steps.Length
            || steps.Any(x => x.ExecutionPlanStepId == Guid.Empty
                || string.IsNullOrWhiteSpace(x.StrategyCode) || x.StrategyVersion < 1
                || x.Subject != DecisionSubject.ManagedTargetServer || !x.IsReadOnly
                || x.RequiresManualApproval)
            || input.Exclusions.Select(x => x.StrategyCode)
                .Intersect(steps.Select(x => x.StrategyCode), StringComparer.Ordinal).Any())
            throw new ArgumentException("ExecutionPlanStepInvalid", nameof(input));
    }
}
