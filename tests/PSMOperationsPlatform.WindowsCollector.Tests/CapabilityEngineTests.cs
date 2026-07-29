using PSMOperationsPlatform.Application.Capabilities;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class CapabilityEngineTests
{
    private static readonly DateTime CapturedAt = new(2026, 7, 28, 12, 0, 0);

    [Fact]
    public void Complete_platform_prerequisites_produce_explicit_capabilities()
    {
        CapabilityEvaluationResult result = Engine().Evaluate(Input());

        Assert.Equal(1, result.CapabilitySchemaVersion);
        Assert.Equal(CapabilityStatus.Supported, Entry(result, CapabilityCodes.SupportsIis).SupportStatus);
        Assert.Equal(CapabilityStatus.Supported, Entry(result, CapabilityCodes.SupportsAspNetCore).SupportStatus);
        Assert.Equal(CapabilityStatus.Supported, Entry(result, CapabilityCodes.HasAspNetCoreHostingBundle).SupportStatus);
        Assert.Equal(CapabilityStatus.Supported, Entry(result, CapabilityCodes.CanCollectAspNetCoreIisLogs).ReadinessStatus);
        Assert.Equal(CapabilityStatus.Supported, Entry(result, CapabilityCodes.SupportsDotNet10).SupportStatus);
        Assert.Equal(CapabilityStatus.Supported, Entry(result, CapabilityCodes.SupportsWindowsPowerShell51).SupportStatus);
        Assert.Equal(CapabilityStatus.Supported, Entry(result, CapabilityCodes.SupportsPowerShell7).SupportStatus);
        Assert.Equal(CapabilityStatus.NotApplicable,
            Entry(result, CapabilityCodes.CanRunPowerShell7CollectorTooling).SupportStatus);
    }

    [Fact]
    public void Operational_readiness_is_unknown_when_access_facts_are_not_inventoried()
    {
        CapabilityEvaluationResult result = Engine().Evaluate(Input());
        CapabilityEntry logs = Entry(result, CapabilityCodes.CanCollectIisLogs);
        Assert.Equal(CapabilityStatus.Supported, logs.SupportStatus);
        Assert.Equal(CapabilityStatus.Unknown, logs.ReadinessStatus);
        Assert.Equal("OperationalPermissionUnknown", logs.ReasonCode);
    }

    [Fact]
    public void Missing_category_is_unknown_not_not_supported()
    {
        PlatformCapabilityInput input = Input() with { Iis = null };
        CapabilityEntry entry = Entry(Engine().Evaluate(input), CapabilityCodes.SupportsIis);
        Assert.Equal(CapabilityStatus.Unknown, entry.SupportStatus);
        Assert.Equal("RequiredFactMissing", entry.ReasonCode);
    }

    [Fact]
    public void Sdk_alone_does_not_imply_runtime_or_dotnet10()
    {
        PlatformCapabilityInput input = Input() with
        {
            DotNet = [new("SDK", "Sdk", "10.0.100", null)]
        };
        CapabilityEvaluationResult result = Engine().Evaluate(input);
        Assert.Equal(CapabilityStatus.NotSupported, Entry(result, CapabilityCodes.SupportsDotNetRuntime).SupportStatus);
        Assert.Equal(CapabilityStatus.NotSupported, Entry(result, CapabilityCodes.SupportsDotNet10).SupportStatus);
        Assert.Equal(CapabilityStatus.Supported, Entry(result, CapabilityCodes.SupportsDotNetSdk).SupportStatus);
    }

    [Fact]
    public void Hosting_bundle_without_iis_is_not_ready()
    {
        PlatformCapabilityInput input = Input() with
        {
            Iis = [],
            DotNet = [new("ASP", "AspNetRuntime", "10.0.0", null),
                new("HOST", "HostingBundle", "10.0.0", null)]
        };
        Assert.Equal(CapabilityStatus.NotSupported,
            Entry(Engine().Evaluate(input), CapabilityCodes.CanCollectAspNetCoreIisLogs).ReadinessStatus);
    }

    [Fact]
    public void Input_order_does_not_change_logical_output()
    {
        PlatformCapabilityInput input = Input();
        CapabilityEvaluationResult first = Engine().Evaluate(input);
        CapabilityEvaluationResult second = Engine().Evaluate(input with
        {
            DotNet = input.DotNet!.Reverse().ToArray(),
            PowerShell = input.PowerShell!.Reverse().ToArray()
        });
        Assert.Equal(
            first.Entries.Select(x => (x.CapabilityCode, x.SupportStatus,
                x.ReadinessStatus, x.ReasonCode, x.RuleVersion)),
            second.Entries.Select(x => (x.CapabilityCode, x.SupportStatus,
                x.ReadinessStatus, x.ReasonCode, x.RuleVersion)));
        Assert.Equal(first.Entries.Select(x => x.CapabilityCode),
            first.Entries.Select(x => x.CapabilityCode).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Invalid_source_inventory_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => Engine().Evaluate(Input() with
        {
            SourceInventoryValid = false
        }));
    }

    private static CapabilityEngine Engine() => new(new FixedTimeProvider());
    private static CapabilityEntry Entry(CapabilityEvaluationResult result, string code) =>
        Assert.Single(result.Entries, x => x.CapabilityCode == code);
    private static PlatformCapabilityInput Input() => new(
        Guid.NewGuid(), Guid.NewGuid(), 7, CapturedAt,
        [new("IIS:PLATFORM", true, "10.0")],
        [new("FRAMEWORK", "Framework", "4.8", 533325),
            new("RUNTIME", "Runtime", "10.0.0", null),
            new("ASP", "AspNetRuntime", "10.0.0", null),
            new("HOST", "HostingBundle", "10.0.0", null),
            new("SDK", "Sdk", "10.0.100", null)],
        [new("DESKTOP", "Desktop", "5.1"), new("CORE", "Core", "7.6")],
        [], [new("TRACE", "Web-Http-Tracing")]);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(CapturedAt, TimeSpan.FromHours(3)).ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone =>
            TimeZoneInfo.CreateCustomTimeZone("TRT", TimeSpan.FromHours(3), "TRT", "TRT");
    }
}
