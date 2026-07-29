using PSMOperationsPlatform.CollectorSdk;
using PluginExecutionContext = PSMOperationsPlatform.CollectorSdk.ExecutionContext;

namespace PSMOperationsPlatform.HelloCollector;

/// <summary>Deterministic, infrastructure-neutral SDK example. Not a production collector.</summary>
public sealed class HelloCollectorPlugin : ICollectorPlugin
{
    public const string PluginIdentifier = "psm.example.hello";
    public const string Strategy = "HelloCollectorExampleStrategy";

    public CollectorPluginDescriptor Describe() =>
        new(PluginIdentifier, Strategy, "Hello Collector",
            "Demonstrates the repository-built Collector Plugin SDK without target access.",
            1, CollectorPluginContractVersions.DescriptorSchemaVersion,
            CollectorPluginSdkVersion.Current, CollectorPluginSdkVersion.Current,
            Array.AsReadOnly([CollectorPluginSubject.ManagedTargetServer]), true,
            CollectorEstimatedCost.Lightweight,
            Array.AsReadOnly(Array.Empty<string>()),
            SupportsCancellation: true, SupportsRetry: false, SupportsTimeout: true,
            SupportsParallelExecution: false, SupportsBatchExecution: false,
            Array.AsReadOnly([CollectorPluginContractVersions.ArtifactSchemaVersion]),
            PluginCertificationMetadata.Experimental,
            new(CollectorPluginContractVersions.PackageMetadataSchemaVersion,
                "PSM Engineering", "PSM", "Repository-Sample",
                "docs/sdk/Hello-Collector.md",
                "samples/PSMOperationsPlatform.HelloCollector",
                "docs/sdk/CollectorPluginSDK.md"));

    public CollectorPluginValidationResult Validate(CollectorPluginValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        PluginCompatibilityResult compatibility = new RuntimePluginCompatibilityMatrix()
            .Evaluate(context.RuntimeVersion, Describe());
        var issues = new List<PluginValidationIssue>();
        if (context.ExecutionContext.Subject != CollectorPluginSubject.ManagedTargetServer)
            issues.Add(new("SubjectMismatch", "Hello Collector supports managed target servers only."));
        if (context.RequiredArtifactSchemaVersion
            != CollectorPluginContractVersions.ArtifactSchemaVersion)
            issues.Add(new("ArtifactSchemaUnsupported",
                "Hello Collector supports artifact schema version 1 only."));
        if (context.ExecutionPolicy.Retry.MaxAttempts > 1)
            issues.Add(new("PluginRetryUnsupported",
                "Hello Collector does not declare retry capability."));
        bool valid = compatibility.Status == PluginCompatibilityStatus.Compatible
            && issues.Count == 0;
        return new(valid ? PluginValidationStatus.Valid : PluginValidationStatus.Invalid,
            valid ? "PluginValid" : "PluginValidationFailed",
            valid ? "Hello Collector validation succeeded."
                : "Hello Collector validation did not satisfy the fixed SDK contract.",
            Array.AsReadOnly(Array.Empty<ExecutionWarning>()),
            Array.AsReadOnly(issues.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray()),
            compatibility);
    }

    public Task<CollectorExecutionResult> ExecuteAsync(
        PluginExecutionContext context, ExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();
        ExecutionArtifacts artifacts = ExecutionArtifacts.Create([], [
            new CollectedObjectArtifact("hello-object", "Hello",
                "managed-target-server", 1)
        ], [], []);
        return Task.FromResult(CollectorExecutionResult.Success(artifacts));
    }
}

public abstract class DeterministicSamplePlugin(
    string pluginId, string strategyCode) : ICollectorPlugin
{
    public virtual CollectorPluginDescriptor Describe() =>
        new(pluginId, strategyCode, strategyCode,
            "Non-production deterministic SDK example.", 1, 1,
            CollectorPluginSdkVersion.Current, CollectorPluginSdkVersion.Current,
            Array.AsReadOnly([CollectorPluginSubject.ManagedTargetServer]), true,
            CollectorEstimatedCost.Lightweight, Array.AsReadOnly(Array.Empty<string>()),
            true, false, true, false, false, Array.AsReadOnly([1]),
            PluginCertificationMetadata.Experimental);

    public CollectorPluginValidationResult Validate(CollectorPluginValidationContext context)
    {
        PluginCompatibilityResult compatibility = new RuntimePluginCompatibilityMatrix()
            .Evaluate(context.RuntimeVersion, Describe());
        bool valid = compatibility.Status == PluginCompatibilityStatus.Compatible
            && context.ExecutionContext.Subject == CollectorPluginSubject.ManagedTargetServer;
        return new(valid ? PluginValidationStatus.Valid : PluginValidationStatus.Invalid,
            valid ? "PluginValid" : "PluginValidationFailed",
            valid ? "The sample plugin validation succeeded."
                : "The sample plugin validation failed.",
            Array.AsReadOnly(Array.Empty<ExecutionWarning>()),
            Array.AsReadOnly(valid ? Array.Empty<PluginValidationIssue>()
                : [new("ContextMismatch", "The sample context is incompatible.")]),
            compatibility);
    }

    public abstract Task<CollectorExecutionResult> ExecuteAsync(
        PluginExecutionContext context, ExecutionPolicy policy,
        CancellationToken cancellationToken);
}

public sealed class NoDataCollectorPlugin()
    : DeterministicSamplePlugin("psm.sample.nodata", "SampleNoDataStrategy")
{
    public override Task<CollectorExecutionResult> ExecuteAsync(
        PluginExecutionContext context, ExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CollectorExecutionResult(CollectorExecutionOutcome.NoData,
            "NoData", "The sample completed with no data.", ExecutionArtifacts.Empty, 0, 0,
            Array.AsReadOnly(Array.Empty<ExecutionWarning>()),
            Array.AsReadOnly(Array.Empty<ExecutionDiagnostic>())));
    }
}

public sealed class FailureCollectorPlugin()
    : DeterministicSamplePlugin("psm.sample.failure", "SampleFailureStrategy")
{
    public override Task<CollectorExecutionResult> ExecuteAsync(
        PluginExecutionContext context, ExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CollectorExecutionResult(CollectorExecutionOutcome.Failed,
            "SampleFailure", "The deterministic sample reported failure.",
            ExecutionArtifacts.Empty, 0, 0, Array.AsReadOnly(Array.Empty<ExecutionWarning>()),
            Array.AsReadOnly(Array.Empty<ExecutionDiagnostic>())));
    }
}

public sealed class LongRunningCollectorPlugin()
    : DeterministicSamplePlugin("psm.sample.longrunning", "SampleLongRunningStrategy")
{
    public override async Task<CollectorExecutionResult> ExecuteAsync(
        PluginExecutionContext context, ExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(5), context.TimeProvider, cancellationToken);
        return CollectorExecutionResult.Success();
    }
}

public sealed class CancellationCollectorPlugin()
    : DeterministicSamplePlugin("psm.sample.cancellation", "SampleCancellationStrategy")
{
    public override async Task<CollectorExecutionResult> ExecuteAsync(
        PluginExecutionContext context, ExecutionPolicy policy,
        CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, context.TimeProvider, cancellationToken);
        return CollectorExecutionResult.Success();
    }
}
