using PSMOperationsPlatform.CollectorSdk;
using PSMOperationsPlatform.HelloCollector;
using PluginExecutionContext = PSMOperationsPlatform.CollectorSdk.ExecutionContext;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class CollectorPluginSdkTests
{
    [Fact]
    public void Current_sdk_version_is_stable_and_comparable()
    {
        Assert.Equal("1.0", CollectorPluginSdkVersion.Current.ToString());
        Assert.Equal(0, CollectorPluginSdkVersion.Current.CompareTo(new(1, 0)));
        Assert.True(new CollectorPluginSdkVersion(1, 1).CompareTo(new(1, 0)) > 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => new CollectorPluginSdkVersion(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CollectorPluginSdkVersion(1, -1));
    }

    [Theory]
    [InlineData("1.0", 1, 0, PluginCompatibilityStatus.Compatible)]
    [InlineData("2.0", 1, 0, PluginCompatibilityStatus.UnsupportedRuntimeVersion)]
    [InlineData("", 1, 0, PluginCompatibilityStatus.Unknown)]
    [InlineData("1.0", 2, 0, PluginCompatibilityStatus.UnsupportedSdkVersion)]
    public void Compatibility_matrix_is_explicit(
        string runtime, int major, int minor, PluginCompatibilityStatus expected)
    {
        CollectorPluginDescriptor descriptor = Descriptor() with
        {
            MinimumSupportedSdkVersion = new(major, minor),
            TargetSdkVersion = new(major, minor)
        };
        PluginCompatibilityResult first =
            new RuntimePluginCompatibilityMatrix().Evaluate(runtime, descriptor);
        PluginCompatibilityResult second =
            new RuntimePluginCompatibilityMatrix().Evaluate(runtime, descriptor);
        Assert.Equal(expected, first.Status);
        Assert.Equal(first, second);
        Assert.False(string.IsNullOrWhiteSpace(first.ReasonCode));
        Assert.False(string.IsNullOrWhiteSpace(first.Explanation));
    }

    [Fact]
    public void Descriptor_normalization_is_ordinal_and_immutable()
    {
        CollectorPluginDescriptor normalized = Descriptor() with
        {
            RequiredCapabilityCodes = Array.AsReadOnly(["Zulu", "Alpha"])
        };
        normalized = normalized.Normalize();
        Assert.Equal(["Alpha", "Zulu"], normalized.RequiredCapabilityCodes);
        Assert.True(typeof(CollectorPluginDescriptor).IsSealed);
    }

    [Fact]
    public void Descriptor_rejects_duplicates_and_invalid_security_contracts()
    {
        Assert.Throws<ArgumentException>(() =>
            (Descriptor() with { PluginId = "" }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Descriptor() with { StrategyCode = "" }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Descriptor() with { PluginVersion = 0 }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Descriptor() with { DescriptorSchemaVersion = 2 }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Descriptor() with { IsReadOnly = false }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Descriptor() with
            {
                SupportsCancellation = false,
                SupportsTimeout = true
            }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Descriptor() with
            {
                RequiredCapabilityCodes = Array.AsReadOnly(["A", "A"])
            }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Descriptor() with
            {
                SupportedSubjects = Array.AsReadOnly([
                    CollectorPluginSubject.ManagedTargetServer,
                    CollectorPluginSubject.ManagedTargetServer])
            }).Validate());
        Assert.Throws<ArgumentException>(() =>
            (Descriptor() with
            {
                SupportedArtifactSchemaVersions = Array.AsReadOnly([1, 1])
            }).Validate());
    }

    [Fact]
    public void Registration_is_explicit_ordinal_and_duplicate_safe()
    {
        var matrix = new RuntimePluginCompatibilityMatrix();
        var alpha = new TestPlugin(Descriptor("plugin.alpha", "Alpha"));
        var beta = new TestPlugin(Descriptor("plugin.beta", "Beta"));
        var registry = new CollectorPluginRegistry([beta, alpha], matrix, "1.0");
        Assert.True(registry.TryResolve("Alpha", out _));
        Assert.False(registry.TryResolve("alpha", out _));
        Assert.Equal(["Alpha", "Beta"], registry.Descriptors.Select(x => x.StrategyCode));
        Assert.Throws<ArgumentException>(() =>
            new CollectorPluginRegistry([alpha, alpha], matrix, "1.0"));
        Assert.Throws<ArgumentException>(() =>
            new CollectorPluginRegistry([
                alpha, new TestPlugin(Descriptor("plugin.alpha", "Different"))
            ], matrix, "1.0"));
    }

    [Fact]
    public void Artifacts_are_bounded_sorted_and_validate_counts()
    {
        ExecutionArtifacts value = ExecutionArtifacts.Create(
            [new("b", "b.txt", "text/plain", 2, null),
                new("a", "a.txt", "text/plain", 1, null)],
            [new("c", "Type", "key", 3)],
            [new("d", "metric", 1.0, "count")],
            [new("Warning", "Safe warning.")]);
        Assert.Equal(["a", "b"], value.Files.Select(x => x.ArtifactId));
        Assert.Throws<ArgumentException>(() => ExecutionArtifacts.Create(
            [new("a", "a", "text/plain", -1, null)], [], [], []));
        Assert.Throws<ArgumentException>(() => ExecutionArtifacts.Create(
            [new("a", "a", "text/plain", 1, null)],
            [new("a", "Type", "key", 1)], [], []));
    }

    [Theory]
    [InlineData(CollectorExecutionOutcome.Success)]
    [InlineData(CollectorExecutionOutcome.NoData)]
    [InlineData(CollectorExecutionOutcome.Failed)]
    [InlineData(CollectorExecutionOutcome.Cancelled)]
    public void Results_are_immutable_and_validate_all_outcomes(
        CollectorExecutionOutcome outcome)
    {
        var result = new CollectorExecutionResult(outcome, outcome.ToString(),
            "Safe deterministic summary.", ExecutionArtifacts.Empty, 0, 0,
            Array.AsReadOnly(Array.Empty<ExecutionWarning>()),
            Array.AsReadOnly(Array.Empty<ExecutionDiagnostic>()));
        Assert.Same(result, result.Validate());
        Assert.True(typeof(CollectorExecutionResult).IsSealed);
    }

    [Fact]
    public void Result_rejects_negative_or_inconsistent_metrics()
    {
        Assert.Throws<ArgumentException>(() => new CollectorExecutionResult(
            CollectorExecutionOutcome.Success, "Success", "Safe.",
            ExecutionArtifacts.Empty, -1, 0, [], []).Validate());
        ExecutionArtifacts artifacts = ExecutionArtifacts.Create([],
            [new("object", "Type", "key", 2)], [], []);
        Assert.Throws<ArgumentException>(() => new CollectorExecutionResult(
            CollectorExecutionOutcome.Success, "Success", "Safe.",
            artifacts, 0, 1, [], []).Validate());
    }

    [Fact]
    public async Task Hello_collector_is_valid_deterministic_and_infrastructure_neutral()
    {
        var plugin = new HelloCollectorPlugin();
        CollectorPluginDescriptor descriptor = plugin.Describe();
        descriptor.Validate();
        CollectorPluginValidationResult validation = plugin.Validate(ValidationContext());
        Assert.True(validation.IsValid);
        CollectorExecutionResult first = await plugin.ExecuteAsync(
            Context(), Policy(), CancellationToken.None);
        CollectorExecutionResult second = await plugin.ExecuteAsync(
            Context(), Policy(), CancellationToken.None);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.ReasonCode, second.ReasonCode);
        Assert.Equal(first.ObjectsCollected, second.ObjectsCollected);
        Assert.Equal(first.Artifacts.Objects.Select(x => x.ArtifactId),
            second.Artifacts.Objects.Select(x => x.ArtifactId));
        Assert.Equal("hello-object", Assert.Single(first.Artifacts.Objects).ArtifactId);
        Assert.Equal(1, first.ObjectsCollected);
    }

    [Fact]
    public async Task Hello_collector_honors_cancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new HelloCollectorPlugin().ExecuteAsync(Context(), Policy(), source.Token));
    }

    [Fact]
    public void Hello_validation_returns_normal_failures_without_throwing()
    {
        CollectorPluginValidationContext artifactMismatch = ValidationContext() with
        {
            RequiredArtifactSchemaVersion = 2
        };
        CollectorPluginValidationResult artifactResult =
            new HelloCollectorPlugin().Validate(artifactMismatch);
        Assert.False(artifactResult.IsValid);
        Assert.Contains(artifactResult.Issues,
            x => x.Code == "ArtifactSchemaUnsupported");

        CollectorPluginValidationContext retryMismatch = ValidationContext() with
        {
            ExecutionPolicy = Policy() with
            {
                Retry = new("StandardReadOnlyRetry", 1, 2,
                    new HashSet<string>(StringComparer.Ordinal)
                    {
                        "HandlerExecutionFailure"
                    }, [TimeSpan.Zero])
            }
        };
        CollectorPluginValidationResult retryResult =
            new HelloCollectorPlugin().Validate(retryMismatch);
        Assert.False(retryResult.IsValid);
        Assert.Contains(retryResult.Issues,
            x => x.Code == "PluginRetryUnsupported");
    }

    [Fact]
    public void Sdk_surface_has_no_infrastructure_or_service_locator_types()
    {
        string[] forbidden = ["DbContext", "IServiceProvider", "IConfiguration",
            "WinRM", "PowerShell", "ConnectionString", "Credential"];
        Type[] types = typeof(ICollectorPlugin).Assembly.GetExportedTypes();
        Assert.DoesNotContain(types.SelectMany(x => x.GetProperties())
            .Select(x => x.PropertyType.Name), name =>
                forbidden.Any(value => name.Contains(value, StringComparison.Ordinal)));
        Assert.DoesNotContain(typeof(ICollectorPlugin).Assembly.GetReferencedAssemblies(),
            x => x.Name is not null && (x.Name.Contains("Infrastructure",
                StringComparison.Ordinal) || x.Name.Contains("EntityFramework",
                StringComparison.Ordinal) || x.Name.Contains("SqlClient",
                StringComparison.Ordinal)));
    }

    private static CollectorPluginDescriptor Descriptor(
        string pluginId = "plugin.test", string strategy = "Strategy") =>
        new(pluginId, strategy, "Test Plugin", "Deterministic test plugin.", 1, 1,
            CollectorPluginSdkVersion.Current, CollectorPluginSdkVersion.Current,
            Array.AsReadOnly([CollectorPluginSubject.ManagedTargetServer]), true,
            CollectorEstimatedCost.Lightweight, Array.AsReadOnly(Array.Empty<string>()),
            true, false, true, false, false, Array.AsReadOnly([1]));

    private static PluginExecutionContext Context() =>
        new(Guid.Parse("10000000-0000-0000-0000-000000000001"), "target.example.test",
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            Guid.Parse("30000000-0000-0000-0000-000000000003"),
            Guid.Parse("40000000-0000-0000-0000-000000000004"),
            HelloCollectorPlugin.Strategy, 1, HelloCollectorPlugin.PluginIdentifier, 1,
            CollectorPluginSubject.ManagedTargetServer,
            Guid.Parse("50000000-0000-0000-0000-000000000005"),
            Guid.Parse("60000000-0000-0000-0000-000000000006"),
            Guid.Parse("70000000-0000-0000-0000-000000000007"),
            1, 1, 1, 1, 1, TimeProvider.System);

    private static ExecutionPolicy Policy() =>
        new(1, new("ShortReadOnly", 1, TimeSpan.FromMinutes(1)),
            new("NoRetry", 1, 1, new HashSet<string>(StringComparer.Ordinal), []),
            new("SerialCore", 1, 1), new("Lightweight", 1, 4),
            new("NoBatch", 1, false));

    private static CollectorPluginValidationContext ValidationContext() =>
        new(Context(), Policy(), "1.0", 1);

    private sealed class TestPlugin(CollectorPluginDescriptor descriptor) : ICollectorPlugin
    {
        public CollectorPluginDescriptor Describe() => descriptor;
        public CollectorPluginValidationResult Validate(CollectorPluginValidationContext context)
        {
            PluginCompatibilityResult compatibility =
                new RuntimePluginCompatibilityMatrix().Evaluate(context.RuntimeVersion, descriptor);
            return new(PluginValidationStatus.Valid, "PluginValid",
                "The plugin validation succeeded.", [], [], compatibility);
        }
        public Task<CollectorExecutionResult> ExecuteAsync(
            PluginExecutionContext context, ExecutionPolicy policy,
            CancellationToken cancellationToken) =>
            Task.FromResult(CollectorExecutionResult.Success());
    }
}
