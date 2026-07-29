using System.Collections.ObjectModel;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.CollectorSdk;

namespace PSMOperationsPlatform.Application.Runtime;

public interface IExecutionPolicyCatalog
{
    ExecutionPolicy Resolve(CollectorRuntimeStep step);
}

public sealed class ExecutionPolicyCatalog : IExecutionPolicyCatalog
{
    private static readonly IReadOnlyDictionary<string, TimeoutExecutionPolicy> Timeouts =
        new ReadOnlyDictionary<string, TimeoutExecutionPolicy>(
            new Dictionary<string, TimeoutExecutionPolicy>(StringComparer.Ordinal)
            {
                [ExecutionPolicyCodes.ShortReadOnly] = new(ExecutionPolicyCodes.ShortReadOnly, 1, TimeSpan.FromMinutes(1)),
                [ExecutionPolicyCodes.StandardReadOnly] = new(ExecutionPolicyCodes.StandardReadOnly, 1, TimeSpan.FromMinutes(5)),
                [ExecutionPolicyCodes.LongReadOnly] = new(ExecutionPolicyCodes.LongReadOnly, 1, TimeSpan.FromMinutes(15))
            });

    private static readonly IReadOnlyDictionary<string, RetryExecutionPolicy> Retries =
        new ReadOnlyDictionary<string, RetryExecutionPolicy>(
            new Dictionary<string, RetryExecutionPolicy>(StringComparer.Ordinal)
            {
                [ExecutionPolicyCodes.NoRetry] = Retry(ExecutionPolicyCodes.NoRetry, 1),
                [ExecutionPolicyCodes.StandardReadOnlyRetry] = Retry(
                    ExecutionPolicyCodes.StandardReadOnlyRetry, 2,
                    RuntimeFailureCategory.HandlerExecutionFailure, RuntimeFailureCategory.Timeout)
            });

    private static readonly IReadOnlyDictionary<string, ParallelExecutionPolicy> Parallel =
        new ReadOnlyDictionary<string, ParallelExecutionPolicy>(
            new Dictionary<string, ParallelExecutionPolicy>(StringComparer.Ordinal)
            {
                [ExecutionPolicyCodes.SerialCore] = new(ExecutionPolicyCodes.SerialCore, 1, 1),
                [ExecutionPolicyCodes.ParallelReadOnlyA] = new(ExecutionPolicyCodes.ParallelReadOnlyA, 1, 2)
            });

    public ExecutionPolicy Resolve(CollectorRuntimeStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        TimeoutExecutionPolicy timeout = Resolve(Timeouts, step.TimeoutPolicyCode,
            step.TimeoutPolicyVersion);
        RetryExecutionPolicy retry = Resolve(Retries, step.RetryPolicyCode,
            step.RetryPolicyVersion);
        ParallelExecutionPolicy parallel = Resolve(Parallel, step.ParallelGroupCode, 1);
        int limit = step.ThrottlingClass switch
        {
            ThrottlingClass.Lightweight => 4,
            ThrottlingClass.Standard => 2,
            ThrottlingClass.Heavy => 1,
            _ => throw new ExecutionPolicyException(RuntimeFailureCategory.ExecutionPolicyNotFound)
        };
        if (step.BatchGroupCode is not null)
            throw new ExecutionPolicyException(RuntimeFailureCategory.ExecutionPolicyNotFound);
        return new(1, timeout, retry, parallel,
            new ThrottlingExecutionPolicy(step.ThrottlingClass.ToString(), 1, limit),
            new BatchingExecutionPolicy("NoBatch", 1, false));
    }

    private static T Resolve<T>(IReadOnlyDictionary<string, T> policies, string code,
        int requestedVersion) where T : notnull
    {
        if (!policies.TryGetValue(code, out T? policy))
            throw new ExecutionPolicyException(RuntimeFailureCategory.ExecutionPolicyNotFound);
        int version = policy switch
        {
            TimeoutExecutionPolicy timeout => timeout.Version,
            RetryExecutionPolicy retry => retry.Version,
            ParallelExecutionPolicy parallel => parallel.Version,
            _ => 0
        };
        if (version != requestedVersion)
            throw new ExecutionPolicyException(RuntimeFailureCategory.ExecutionPolicyVersionUnsupported);
        return policy;
    }

    private static RetryExecutionPolicy Retry(string code, int maxAttempts,
        params RuntimeFailureCategory[] categories) =>
        new(code, 1, maxAttempts,
            new HashSet<string>(categories.Select(x => x.ToString()), StringComparer.Ordinal),
            Array.AsReadOnly(maxAttempts > 1 ? [TimeSpan.FromSeconds(1)] : Array.Empty<TimeSpan>()));
}

public sealed class ExecutionPolicyException(RuntimeFailureCategory category) : Exception(category.ToString())
{
    public RuntimeFailureCategory Category { get; } = category;
}
