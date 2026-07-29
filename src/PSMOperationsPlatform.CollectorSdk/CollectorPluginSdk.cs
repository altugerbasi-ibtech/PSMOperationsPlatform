using System.Collections.ObjectModel;
using System.Text.Json;

namespace PSMOperationsPlatform.CollectorSdk;

public readonly record struct CollectorPluginSdkVersion : IComparable<CollectorPluginSdkVersion>
{
    public static CollectorPluginSdkVersion Current { get; } = new(1, 0);

    public CollectorPluginSdkVersion(int major, int minor)
    {
        if (major < 1 || minor < 0)
            throw new ArgumentOutOfRangeException(nameof(major), "PluginSdkVersionInvalid");
        Major = major;
        Minor = minor;
    }

    public int Major { get; }
    public int Minor { get; }
    public int CompareTo(CollectorPluginSdkVersion other) =>
        Major != other.Major ? Major.CompareTo(other.Major) : Minor.CompareTo(other.Minor);
    public override string ToString() => $"{Major}.{Minor}";
}

public enum CollectorPluginSubject { ManagedTargetServer = 1 }
public enum CollectorEstimatedCost { Lightweight = 1, Standard = 2, Heavy = 3 }
public enum CollectorExecutionOutcome { Success = 1, Failed = 2, Cancelled = 3, NoData = 4 }
public enum PluginValidationStatus { Valid = 1, Invalid = 2 }
public enum PluginCompatibilityStatus
{
    Compatible = 1, Incompatible = 2, UnsupportedSdkVersion = 3,
    UnsupportedRuntimeVersion = 4, InvalidDescriptor = 5, Unknown = 6,
    NotApplicable = 7
}

public static class CollectorPluginContractVersions
{
    public const int DescriptorSchemaVersion = 1;
    public const int ArtifactSchemaVersion = 1;
    public const int CompatibilityBadgeSchemaVersion = 1;
    public const int CertificationSchemaVersion = 1;
    public const int PackageMetadataSchemaVersion = 1;
    public const int MonitoringReadinessSchemaVersion = 1;
    public const int MonitoringReadinessBadgeSchemaVersion = 1;
}

public enum PluginCertificationStatus { Experimental = 1, Verified = 2, Certified = 3 }

public sealed record PluginCertificationMetadata(
    int CertificationSchemaVersion,
    PluginCertificationStatus Status,
    string? CertificationAuthority,
    DateTime? CertifiedAt,
    string? CertificationReference,
    string? Notes)
{
    public static PluginCertificationMetadata Experimental { get; } =
        new(CollectorPluginContractVersions.CertificationSchemaVersion,
            PluginCertificationStatus.Experimental, null, null, null,
            "Repository-built non-production example.");

    public PluginCertificationMetadata Validate()
    {
        if (CertificationSchemaVersion != CollectorPluginContractVersions.CertificationSchemaVersion
            || !Enum.IsDefined(Status)
            || PluginMetadataValidation.AnyTooLong(256, CertificationAuthority, CertificationReference, Notes)
            || Status == PluginCertificationStatus.Certified
                && (string.IsNullOrWhiteSpace(CertificationAuthority) || CertifiedAt is null))
            throw new ArgumentException("PluginCertificationInvalid");
        return this;
    }
}

public sealed record PluginPackageMetadata(
    int PackageMetadataSchemaVersion,
    string? Author,
    string? Company,
    string? LicenseIdentifier,
    string? SupportReference,
    string? ProjectReference,
    string? RepositoryReference)
{
    public PluginPackageMetadata Normalize()
    {
        if (PackageMetadataSchemaVersion != CollectorPluginContractVersions.PackageMetadataSchemaVersion
            || PluginMetadataValidation.AnyTooLong(256, Author, Company, LicenseIdentifier, SupportReference,
                ProjectReference, RepositoryReference)
            || new[] { SupportReference, ProjectReference, RepositoryReference }
                .Where(x => !string.IsNullOrWhiteSpace(x)).Any(IsUnsafeReference)
            || new[] { Author, Company, LicenseIdentifier, SupportReference, ProjectReference,
                    RepositoryReference }.Any(ContainsSecretMarker))
            throw new ArgumentException("PluginPackageMetadataInvalid");
        return this with
        {
            Author = Clean(Author), Company = Clean(Company),
            LicenseIdentifier = Clean(LicenseIdentifier),
            SupportReference = Clean(SupportReference),
            ProjectReference = Clean(ProjectReference),
            RepositoryReference = Clean(RepositoryReference)
        };
    }

    public string ToDeterministicJson() =>
        JsonSerializer.Serialize(Normalize(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

    private static bool IsUnsafeReference(string? value)
    {
        string text = value!.Trim();
        if (Path.IsPathRooted(text) || text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return true;
        return Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is not ("https" or "mailto");
    }

    private static bool ContainsSecretMarker(string? value) =>
        value is not null && (value.Contains("token=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("apikey=", StringComparison.OrdinalIgnoreCase));
    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SdkCompatibilityBadge(
    int BadgeSchemaVersion,
    string PluginId,
    int PluginVersion,
    CollectorPluginSdkVersion TargetSdkVersion,
    string SupportedRuntimeRange,
    PluginCompatibilityStatus CompatibilityStatus,
    string Label,
    string Message,
    string ReasonCode)
{
    public string ToMarkdown() => $"![{Label}](badge:{Uri.EscapeDataString(Label)})";
}

public sealed class SdkCompatibilityBadgeGenerator(IRuntimePluginCompatibilityMatrix matrix)
{
    public SdkCompatibilityBadge Generate(string runtimeVersion, CollectorPluginDescriptor descriptor)
    {
        PluginCompatibilityResult result = matrix.Evaluate(runtimeVersion, descriptor);
        bool compatible = result.Status == PluginCompatibilityStatus.Compatible;
        string range = compatible ? $"PSM Runtime {result.RuntimeVersion}" : "No supported runtime";
        return new(CollectorPluginContractVersions.CompatibilityBadgeSchemaVersion,
            descriptor.PluginId, descriptor.PluginVersion, descriptor.TargetSdkVersion, range,
            result.Status, compatible ? $"Compatible with PSM Runtime {result.RuntimeVersion}"
                : "Not compatible with the selected PSM Runtime",
            result.Explanation, result.ReasonCode);
    }
}

public enum PluginMonitoringReadinessStatus
{
    Ready = 1, PartiallyReady = 2, NotReady = 3, Unknown = 4
}

public sealed record PluginMonitoringReadinessEvidence(
    bool ManifestAvailable,
    bool DocumentationReferenceAvailable,
    bool ContractTestsPassed,
    bool SafeFailureResultSupported,
    bool QualityAssessmentAvailable);

public sealed record PluginMonitoringReadinessDimensionResult(
    string Code, bool Satisfied, bool EvidenceAvailable, string ReasonCode);

public sealed record PluginMonitoringReadinessAssessment(
    int ReadinessSchemaVersion,
    string PluginId,
    int PluginVersion,
    CollectorPluginSdkVersion TargetSdkVersion,
    PluginMonitoringReadinessStatus Status,
    IReadOnlyList<PluginMonitoringReadinessDimensionResult> DimensionResults,
    IReadOnlyList<string> ReasonCodes);

public sealed record PluginMonitoringReadinessBadge(
    int BadgeSchemaVersion,
    string PluginId,
    int PluginVersion,
    CollectorPluginSdkVersion TargetSdkVersion,
    PluginMonitoringReadinessStatus MonitoringReadinessStatus,
    string Label,
    string Message,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<PluginMonitoringReadinessDimensionResult> DimensionResults)
{
    public string ToMarkdown() => $"`{Label}`";
}

public sealed class PluginMonitoringReadinessEvaluator(
    IRuntimePluginCompatibilityMatrix compatibilityMatrix)
{
    public PluginMonitoringReadinessAssessment Evaluate(
        string runtimeVersion,
        CollectorPluginDescriptor descriptor,
        PluginMonitoringReadinessEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(evidence);
        var dimensions = new List<PluginMonitoringReadinessDimensionResult>();
        try { descriptor.Normalize(); }
        catch (ArgumentException)
        {
            return Result(PluginMonitoringReadinessStatus.NotReady,
                [Dimension("DescriptorValid", false, true, "PluginDescriptorInvalid")]);
        }

        PluginCompatibilityResult compatibility =
            compatibilityMatrix.Evaluate(runtimeVersion, descriptor);
        dimensions.Add(Dimension("DescriptorValid", true, true, "DescriptorValid"));
        dimensions.Add(Dimension("SdkCompatibility",
            compatibility.Status == PluginCompatibilityStatus.Compatible, true,
            compatibility.ReasonCode));
        dimensions.Add(Dimension("CancellationSupport", descriptor.SupportsCancellation,
            true, descriptor.SupportsCancellation ? "CancellationSupported"
                : "CancellationUnsupported"));
        bool timeoutInvariant = !descriptor.SupportsTimeout || descriptor.SupportsCancellation;
        dimensions.Add(Dimension("TimeoutCancellationInvariant", timeoutInvariant, true,
            timeoutInvariant ? "TimeoutCancellationInvariantSatisfied"
                : "TimeoutCancellationInvariantFailed"));
        dimensions.Add(Dimension("ArtifactSchema",
            descriptor.SupportedArtifactSchemaVersions.Contains(
                CollectorPluginContractVersions.ArtifactSchemaVersion),
            true, "ArtifactSchemaEvaluated"));
        dimensions.Add(Dimension("SafeDescriptorMetadata",
            descriptor.PackageMetadata is null
                || IsSafePackageMetadata(descriptor.PackageMetadata),
            true, "DescriptorMetadataEvaluated"));
        dimensions.Add(Dimension("Manifest", evidence.ManifestAvailable,
            evidence.ManifestAvailable, evidence.ManifestAvailable
                ? "ManifestAvailable" : "ManifestEvidenceUnavailable"));
        dimensions.Add(Dimension("Documentation", evidence.DocumentationReferenceAvailable,
            evidence.DocumentationReferenceAvailable,
            evidence.DocumentationReferenceAvailable
                ? "DocumentationAvailable" : "DocumentationEvidenceUnavailable"));
        dimensions.Add(Dimension("ContractTests", evidence.ContractTestsPassed,
            evidence.ContractTestsPassed, evidence.ContractTestsPassed
                ? "ContractTestsPassed" : "ContractTestEvidenceUnavailable"));
        dimensions.Add(Dimension("SafeFailureResult", evidence.SafeFailureResultSupported,
            evidence.SafeFailureResultSupported, evidence.SafeFailureResultSupported
                ? "SafeFailureResultSupported" : "SafeFailureEvidenceUnavailable"));
        dimensions.Add(Dimension("QualityAssessment", evidence.QualityAssessmentAvailable,
            evidence.QualityAssessmentAvailable, evidence.QualityAssessmentAvailable
                ? "QualityAssessmentAvailable" : "QualityAssessmentEvidenceUnavailable"));

        if (compatibility.Status == PluginCompatibilityStatus.Unknown)
            return Result(PluginMonitoringReadinessStatus.Unknown, dimensions);
        if (compatibility.Status != PluginCompatibilityStatus.Compatible
            || dimensions.Any(x => x.EvidenceAvailable && !x.Satisfied))
            return Result(PluginMonitoringReadinessStatus.NotReady, dimensions);
        return Result(dimensions.All(x => x.Satisfied)
            ? PluginMonitoringReadinessStatus.Ready
            : PluginMonitoringReadinessStatus.PartiallyReady, dimensions);

        PluginMonitoringReadinessAssessment Result(
            PluginMonitoringReadinessStatus status,
            IEnumerable<PluginMonitoringReadinessDimensionResult> values)
        {
            PluginMonitoringReadinessDimensionResult[] ordered = values
                .OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
            return new(CollectorPluginContractVersions.MonitoringReadinessSchemaVersion,
                descriptor.PluginId ?? string.Empty, descriptor.PluginVersion,
                descriptor.TargetSdkVersion, status, Array.AsReadOnly(ordered),
                Array.AsReadOnly(ordered.Select(x => x.ReasonCode)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray()));
        }

        static PluginMonitoringReadinessDimensionResult Dimension(
            string code, bool satisfied, bool available, string reason) =>
            new(code, satisfied, available, reason);
    }

    public PluginMonitoringReadinessBadge GenerateBadge(
        PluginMonitoringReadinessAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        string label = assessment.Status switch
        {
            PluginMonitoringReadinessStatus.Ready => "Monitoring Ready",
            PluginMonitoringReadinessStatus.PartiallyReady => "Partially Monitoring Ready",
            PluginMonitoringReadinessStatus.NotReady => "Monitoring Not Ready",
            _ => "Unknown Monitoring Readiness"
        };
        return new(CollectorPluginContractVersions.MonitoringReadinessBadgeSchemaVersion,
            assessment.PluginId, assessment.PluginVersion, assessment.TargetSdkVersion,
            assessment.Status, label,
            $"{label}. The assessment is advisory and does not affect dispatch eligibility.",
            assessment.ReasonCodes, assessment.DimensionResults);
    }

    private static bool IsSafePackageMetadata(PluginPackageMetadata metadata)
    {
        try { metadata.Normalize(); return true; }
        catch (ArgumentException) { return false; }
    }
}

internal static class PluginMetadataValidation
{
    public static bool AnyTooLong(int limit, params string?[] values) =>
        values.Any(x => x is not null && x.Length > limit);
}

public sealed record CollectorPluginDescriptor(
    string PluginId,
    string StrategyCode,
    string DisplayName,
    string Description,
    int PluginVersion,
    int DescriptorSchemaVersion,
    CollectorPluginSdkVersion MinimumSupportedSdkVersion,
    CollectorPluginSdkVersion TargetSdkVersion,
    IReadOnlyList<CollectorPluginSubject> SupportedSubjects,
    bool IsReadOnly,
    CollectorEstimatedCost EstimatedCost,
    IReadOnlyList<string> RequiredCapabilityCodes,
    bool SupportsCancellation,
    bool SupportsRetry,
    bool SupportsTimeout,
    bool SupportsParallelExecution,
    bool SupportsBatchExecution,
    IReadOnlyList<int> SupportedArtifactSchemaVersions,
    PluginCertificationMetadata? Certification = null,
    PluginPackageMetadata? PackageMetadata = null)
{
    public CollectorPluginDescriptor Normalize()
    {
        Validate();
        return this with
        {
            SupportedSubjects = Array.AsReadOnly(SupportedSubjects
                .Distinct().OrderBy(x => x).ToArray()),
            RequiredCapabilityCodes = Array.AsReadOnly(RequiredCapabilityCodes
                .OrderBy(x => x, StringComparer.Ordinal).ToArray()),
            SupportedArtifactSchemaVersions = Array.AsReadOnly(
                SupportedArtifactSchemaVersions.OrderBy(x => x).ToArray()),
            Certification = Certification?.Validate(),
            PackageMetadata = PackageMetadata?.Normalize()
        };
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PluginId) || string.IsNullOrWhiteSpace(StrategyCode)
            || string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Description)
            || PluginVersion < 1
            || DescriptorSchemaVersion != CollectorPluginContractVersions.DescriptorSchemaVersion
            || MinimumSupportedSdkVersion.CompareTo(TargetSdkVersion) > 0
            || SupportedSubjects is null || SupportedSubjects.Count == 0
            || SupportedSubjects.Distinct().Count() != SupportedSubjects.Count
            || !SupportedSubjects.Contains(CollectorPluginSubject.ManagedTargetServer)
            || !IsReadOnly || !SupportsCancellation
            || SupportsTimeout && !SupportsCancellation
            || RequiredCapabilityCodes is null
            || RequiredCapabilityCodes.Any(string.IsNullOrWhiteSpace)
            || RequiredCapabilityCodes.Distinct(StringComparer.Ordinal).Count()
                != RequiredCapabilityCodes.Count
            || SupportedArtifactSchemaVersions is null
            || !SupportedArtifactSchemaVersions.Contains(
                CollectorPluginContractVersions.ArtifactSchemaVersion)
            || SupportedArtifactSchemaVersions.Any(x => x < 1)
            || SupportedArtifactSchemaVersions.Distinct().Count()
                != SupportedArtifactSchemaVersions.Count)
            throw new ArgumentException("PluginDescriptorInvalid");
        Certification?.Validate();
        PackageMetadata?.Normalize();
    }
}

public sealed record PluginCompatibilityResult(
    string RuntimeVersion,
    CollectorPluginSdkVersion MinimumRuntimeSupportedSdkVersion,
    CollectorPluginSdkVersion MaximumRuntimeSupportedSdkVersion,
    CollectorPluginSdkVersion PluginMinimumSdkVersion,
    CollectorPluginSdkVersion PluginTargetSdkVersion,
    PluginCompatibilityStatus Status,
    string ReasonCode,
    string Explanation);

public interface IRuntimePluginCompatibilityMatrix
{
    PluginCompatibilityResult Evaluate(
        string runtimeVersion, CollectorPluginDescriptor descriptor);
}

public sealed class RuntimePluginCompatibilityMatrix : IRuntimePluginCompatibilityMatrix
{
    public const string SupportedRuntimeVersion = "1.0";

    public PluginCompatibilityResult Evaluate(
        string runtimeVersion, CollectorPluginDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var current = CollectorPluginSdkVersion.Current;
        if (string.IsNullOrWhiteSpace(runtimeVersion))
            return Result(PluginCompatibilityStatus.Unknown, "RuntimeVersionUnknown",
                "Runtime compatibility cannot be determined because the runtime version is unavailable.");
        if (!string.Equals(runtimeVersion, SupportedRuntimeVersion, StringComparison.Ordinal))
            return Result(PluginCompatibilityStatus.UnsupportedRuntimeVersion,
                "RuntimeVersionUnsupported",
                "The runtime contract version is not supported by this SDK compatibility matrix.");
        try { descriptor.Validate(); }
        catch (ArgumentException)
        {
            return Result(PluginCompatibilityStatus.InvalidDescriptor,
                "PluginDescriptorInvalid", "The plugin descriptor is structurally invalid.");
        }
        if (descriptor.TargetSdkVersion != current
            || descriptor.MinimumSupportedSdkVersion.CompareTo(current) > 0)
            return Result(PluginCompatibilityStatus.UnsupportedSdkVersion,
                "PluginSdkVersionIncompatible",
                "The plugin SDK version is not supported by runtime contract version 1.0.");
        return Result(PluginCompatibilityStatus.Compatible, "PluginSdkCompatible",
            "The plugin SDK version is compatible with runtime contract version 1.0.");

        PluginCompatibilityResult Result(
            PluginCompatibilityStatus status, string reason, string explanation) =>
            new(runtimeVersion, current, current, descriptor.MinimumSupportedSdkVersion,
                descriptor.TargetSdkVersion, status, reason, explanation);
    }
}

public sealed record ExecutionContext(
    Guid ManagedServerId,
    string? TargetFqdn,
    Guid ExecutionPlanId,
    Guid ExecutionRunId,
    Guid ExecutionPlanStepId,
    string StrategyCode,
    int StrategyVersion,
    string PluginId,
    int PluginVersion,
    CollectorPluginSubject Subject,
    Guid SourceDecisionPlanId,
    Guid SourceCapabilitySnapshotId,
    Guid SourceInventoryRunId,
    long SourceInventoryVersion,
    int ExecutionPlanSchemaVersion,
    int PolicySchemaVersion,
    int DescriptorSchemaVersion,
    int EventSchemaVersion,
    TimeProvider TimeProvider);

public sealed record TimeoutExecutionPolicy(string Code, int Version, TimeSpan Timeout);
public sealed record RetryExecutionPolicy(
    string Code, int Version, int MaxAttempts,
    IReadOnlySet<string> RetryableFailureCategories,
    IReadOnlyList<TimeSpan> DelaySchedule);
public sealed record ParallelExecutionPolicy(string Code, int Version, int MaximumConcurrency);
public sealed record ThrottlingExecutionPolicy(string Code, int Version, int MaximumConcurrency);
public sealed record BatchingExecutionPolicy(string Code, int Version, bool Enabled);
public sealed record ExecutionPolicy(
    int PolicySchemaVersion,
    TimeoutExecutionPolicy Timeout,
    RetryExecutionPolicy Retry,
    ParallelExecutionPolicy Parallel,
    ThrottlingExecutionPolicy Throttling,
    BatchingExecutionPolicy Batching);

public sealed record CollectorPluginValidationContext(
    ExecutionContext ExecutionContext,
    ExecutionPolicy ExecutionPolicy,
    string RuntimeVersion,
    int RequiredArtifactSchemaVersion);

public sealed record PluginValidationIssue(string Code, string Explanation);
public sealed record ExecutionWarning(string Code, string Message);
public sealed record ExecutionDiagnostic(string Code, string Message);

public sealed record CollectorPluginValidationResult(
    PluginValidationStatus Status,
    string ReasonCode,
    string Explanation,
    IReadOnlyList<ExecutionWarning> Warnings,
    IReadOnlyList<PluginValidationIssue> Issues,
    PluginCompatibilityResult Compatibility)
{
    public bool IsValid => Status == PluginValidationStatus.Valid;
}

public sealed record CollectedFileArtifact(
    string ArtifactId, string LogicalReference, string ContentType,
    long SizeBytes, string? Sha256);
public sealed record CollectedObjectArtifact(
    string ArtifactId, string ObjectType, string StableKey, long ObjectCount);
public sealed record GeneratedMetricArtifact(
    string ArtifactId, string MetricName, double Value, string Unit);

public sealed record ExecutionArtifacts(
    int ArtifactSchemaVersion,
    IReadOnlyList<CollectedFileArtifact> Files,
    IReadOnlyList<CollectedObjectArtifact> Objects,
    IReadOnlyList<GeneratedMetricArtifact> Metrics,
    IReadOnlyList<ExecutionWarning> Warnings)
{
    public static ExecutionArtifacts Empty { get; } = Create([], [], [], []);

    public static ExecutionArtifacts Create(
        IEnumerable<CollectedFileArtifact> files,
        IEnumerable<CollectedObjectArtifact> objects,
        IEnumerable<GeneratedMetricArtifact> metrics,
        IEnumerable<ExecutionWarning> warnings)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(warnings);
        CollectedFileArtifact[] fileItems = files.OrderBy(x => x.ArtifactId, StringComparer.Ordinal).ToArray();
        CollectedObjectArtifact[] objectItems = objects.OrderBy(x => x.ArtifactId, StringComparer.Ordinal).ToArray();
        GeneratedMetricArtifact[] metricItems = metrics.OrderBy(x => x.ArtifactId, StringComparer.Ordinal).ToArray();
        ExecutionWarning[] warningItems = warnings.OrderBy(x => x.Code, StringComparer.Ordinal).ToArray();
        string[] ids = fileItems.Select(x => x.ArtifactId)
            .Concat(objectItems.Select(x => x.ArtifactId))
            .Concat(metricItems.Select(x => x.ArtifactId)).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace)
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length
            || fileItems.Any(x => string.IsNullOrWhiteSpace(x.LogicalReference)
                || string.IsNullOrWhiteSpace(x.ContentType) || x.SizeBytes < 0)
            || objectItems.Any(x => string.IsNullOrWhiteSpace(x.ObjectType)
                || string.IsNullOrWhiteSpace(x.StableKey) || x.ObjectCount < 0)
            || metricItems.Any(x => string.IsNullOrWhiteSpace(x.MetricName)
                || string.IsNullOrWhiteSpace(x.Unit) || !double.IsFinite(x.Value))
            || warningItems.Any(x => string.IsNullOrWhiteSpace(x.Code)
                || string.IsNullOrWhiteSpace(x.Message)))
            throw new ArgumentException("ArtifactContractFailure");
        return new(CollectorPluginContractVersions.ArtifactSchemaVersion,
            Array.AsReadOnly(fileItems), Array.AsReadOnly(objectItems),
            Array.AsReadOnly(metricItems), Array.AsReadOnly(warningItems));
    }
}

public sealed record CollectorExecutionResult(
    CollectorExecutionOutcome Outcome,
    string ReasonCode,
    string Summary,
    ExecutionArtifacts Artifacts,
    long BytesCollected,
    long ObjectsCollected,
    IReadOnlyList<ExecutionWarning> Warnings,
    IReadOnlyList<ExecutionDiagnostic> Diagnostics)
{
    public CollectorExecutionResult Validate()
    {
        if (string.IsNullOrWhiteSpace(ReasonCode) || string.IsNullOrWhiteSpace(Summary)
            || BytesCollected < 0 || ObjectsCollected < 0
            || Warnings is null || Diagnostics is null || Artifacts is null
            || (Artifacts.Files.Count > 0 && BytesCollected != Artifacts.Files.Sum(x => x.SizeBytes))
            || (Artifacts.Objects.Count > 0
                && ObjectsCollected != Artifacts.Objects.Sum(x => x.ObjectCount)))
            throw new ArgumentException("PluginContractFailure");
        return this;
    }

    public static CollectorExecutionResult Success(ExecutionArtifacts? artifacts = null)
    {
        ExecutionArtifacts value = artifacts ?? ExecutionArtifacts.Empty;
        return new(CollectorExecutionOutcome.Success, "Success",
            "The plugin completed successfully.", value,
            value.Files.Sum(x => x.SizeBytes), value.Objects.Sum(x => x.ObjectCount),
            Array.AsReadOnly(Array.Empty<ExecutionWarning>()),
            Array.AsReadOnly(Array.Empty<ExecutionDiagnostic>()));
    }

    public static CollectorExecutionResult Success(long bytes, long objects) =>
        new(CollectorExecutionOutcome.Success, "Success",
            "The plugin completed successfully.", ExecutionArtifacts.Empty,
            bytes, objects, Array.AsReadOnly(Array.Empty<ExecutionWarning>()),
            Array.AsReadOnly(Array.Empty<ExecutionDiagnostic>()));
}

public interface ICollectorPlugin
{
    CollectorPluginDescriptor Describe();
    CollectorPluginValidationResult Validate(CollectorPluginValidationContext context);
    Task<CollectorExecutionResult> ExecuteAsync(
        ExecutionContext context, ExecutionPolicy policy,
        CancellationToken cancellationToken);
}

public interface ICollectorPluginRegistry
{
    bool TryResolve(string strategyCode, out ICollectorPlugin? plugin);
    IReadOnlyList<CollectorPluginDescriptor> Descriptors { get; }
}

public sealed class CollectorPluginRegistry : ICollectorPluginRegistry
{
    private readonly IReadOnlyDictionary<string, ICollectorPlugin> plugins;
    public CollectorPluginRegistry(
        IEnumerable<ICollectorPlugin> registrations,
        IRuntimePluginCompatibilityMatrix compatibility,
        string runtimeVersion)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(compatibility);
        var byStrategy = new Dictionary<string, ICollectorPlugin>(StringComparer.Ordinal);
        var pluginIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ICollectorPlugin plugin in registrations)
        {
            CollectorPluginDescriptor descriptor = plugin.Describe().Normalize();
            if (!pluginIds.Add(descriptor.PluginId))
                throw new ArgumentException("DuplicatePluginId", nameof(registrations));
            if (!byStrategy.TryAdd(descriptor.StrategyCode, plugin))
                throw new ArgumentException("DuplicateStrategyCode", nameof(registrations));
            PluginCompatibilityResult result = compatibility.Evaluate(runtimeVersion, descriptor);
            if (result.Status != PluginCompatibilityStatus.Compatible)
                throw new ArgumentException(result.ReasonCode, nameof(registrations));
        }
        plugins = new ReadOnlyDictionary<string, ICollectorPlugin>(byStrategy);
        Descriptors = Array.AsReadOnly(byStrategy.Values.Select(x => x.Describe().Normalize())
            .OrderBy(x => x.StrategyCode, StringComparer.Ordinal).ToArray());
    }

    public IReadOnlyList<CollectorPluginDescriptor> Descriptors { get; }
    public bool TryResolve(string strategyCode, out ICollectorPlugin? plugin) =>
        plugins.TryGetValue(strategyCode, out plugin);
}
