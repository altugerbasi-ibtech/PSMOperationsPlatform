using Microsoft.Extensions.Logging.Abstractions;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class PlatformDiscoveryModuleTests
{
    [Fact]
    public void Roles_and_features_are_filtered_and_keyed_deterministically()
    {
        WinRmCommandRecord role = Record(
            ("Name", "Web-Server"), ("DisplayName", "Web Server"),
            ("Installed", true), ("Parent", null), ("FeatureType", "Role"),
            ("RestartNeeded", "No"));
        WinRmCommandRecord feature = Record(
            ("Name", "NET-Framework-45-Core"), ("DisplayName", ".NET Framework"),
            ("Installed", true), ("Parent", null), ("FeatureType", "Feature"),
            ("RestartNeeded", "No"));

        Assert.Equal("ROLE:WEB-SERVER",
            Assert.Single(WindowsRoleDiscoveryNormalizer.Normalize([feature, role])).RoleKey);
        Assert.Equal("FEATURE:NET-FRAMEWORK-45-CORE",
            Assert.Single(WindowsFeatureDiscoveryNormalizer.Normalize([role, feature])).FeatureKey);
        Assert.Empty(WindowsRoleDiscoveryNormalizer.Normalize([]));
        Assert.Empty(WindowsFeatureDiscoveryNormalizer.Normalize([]));
    }

    [Fact]
    public void Iis_discovery_supports_installed_and_valid_empty()
    {
        Assert.Empty(IisPlatformDiscoveryNormalizer.Normalize([]));
        IisPlatformInventoryItem item = Assert.Single(
            IisPlatformDiscoveryNormalizer.Normalize(
                [Record(("Install", 1), ("VersionString", null),
                    ("MajorVersion", 10), ("MinorVersion", 0))]));
        Assert.True(item.Installed);
        Assert.Equal("10.0", item.Version);
    }

    [Fact]
    public void DotNet_discovery_classifies_supported_platform_products()
    {
        DotNetPlatformInventoryItem[] items = DotNetPlatformDiscoveryNormalizer.Normalize(
        [
            Record(("DisplayName", null), ("DisplayVersion", null),
                ("Version", "4.8"), ("Release", 533325)),
            Record(("DisplayName", "Microsoft .NET Runtime - 10.0.0"),
                ("DisplayVersion", "10.0.0"), ("Version", null), ("Release", null)),
            Record(("DisplayName", "Unrelated Product"), ("DisplayVersion", "1"),
                ("Version", null), ("Release", null)),
        ]);

        Assert.Equal(["Framework", "Runtime"],
            items.Select(item => item.Category).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void PowerShell_discovery_is_order_independent_and_rejects_duplicates()
    {
        WinRmCommandRecord desktop = Record(("Name", "powershell.exe"),
            ("Source", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"),
            ("Version", "5.1"));
        WinRmCommandRecord core = Record(("Name", "pwsh.exe"),
            ("Source", @"C:\Program Files\PowerShell\7\pwsh.exe"), ("Version", "7.6"));

        string[] first = PowerShellPlatformDiscoveryNormalizer.Normalize([desktop, core])
            .Select(item => item.PowerShellKey).ToArray();
        string[] second = PowerShellPlatformDiscoveryNormalizer.Normalize([core, desktop])
            .Select(item => item.PowerShellKey).ToArray();
        Assert.Equal(first, second);
        Assert.Throws<WindowsInventoryValidationException>(
            () => PowerShellPlatformDiscoveryNormalizer.Normalize([desktop, desktop]));
    }

    [Fact]
    public async Task Modules_use_the_supplied_shared_session_and_valid_empty_is_explicit()
    {
        var session = new EmptySession();
        var context = new InventoryModuleContext(
            Guid.NewGuid(), "server.ae.local", Guid.NewGuid(), session,
            TimeProvider.System, NullLogger.Instance);

        InventoryModuleResult<WindowsRoleInventoryItem[]> result =
            await new WindowsRoleDiscoveryModule().CollectAsync(context, default);

        Assert.True(result.IsSuccessful);
        Assert.True(result.IsValidEmpty);
        Assert.Same(session, context.Session);
        Assert.Equal("Get-WindowsFeature", Assert.Single(session.Commands).CommandName);
    }

    private static WinRmCommandRecord Record(params (string Name, object? Value)[] values) =>
        new(new Dictionary<string, object?>(
            values.ToDictionary(value => value.Name, value => value.Value),
            StringComparer.OrdinalIgnoreCase));

    private sealed class EmptySession : IWinRmCommandSession
    {
        internal List<WinRmCommandDefinition> Commands { get; } = [];
        public bool IsUsable => true;
        public Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult<IReadOnlyList<WinRmCommandRecord>>([]);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
