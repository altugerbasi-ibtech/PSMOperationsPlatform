using Microsoft.Extensions.Logging.Abstractions;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class NetworkInventoryModuleTests
{
    private const string GuidValue = "4f36f9db-5c17-43f9-93e2-2c7ef9012364";

    [Fact]
    public void Adapter_prefers_guid_and_normalizes_mac()
    {
        var adapter = Assert.Single(NetworkAdapterInventoryNormalizer.Normalize(
            [Adapter(("GUID", $"{{{GuidValue.ToUpperInvariant()}}}"),
                ("MACAddress", "00-11-22-33-44-55"))]));

        Assert.Equal($"GUID:{GuidValue}", adapter.AdapterKey);
        Assert.Equal("00:11:22:33:44:55", adapter.MacAddress);
    }

    [Fact]
    public void Adapter_uses_mac_then_pnp_and_rejects_placeholder_mac()
    {
        Assert.Equal(
            "MAC:00:11:22:33:44:55",
            Assert.Single(NetworkAdapterInventoryNormalizer.Normalize(
                [Adapter(("GUID", null), ("MACAddress", "001122334455"))])).AdapterKey);
        Assert.Equal(
            @"PNP:PCI\VEN_1234",
            Assert.Single(NetworkAdapterInventoryNormalizer.Normalize(
                [Adapter(("GUID", null), ("MACAddress", "00-00-00-00-00-00"),
                    ("PNPDeviceID", @"PCI\VEN_1234"))])).AdapterKey);
    }

    [Fact]
    public void Adapter_fallback_is_deterministic_and_order_independent()
    {
        WinRmCommandRecord a = Adapter(
            ("GUID", null), ("MACAddress", null), ("PNPDeviceID", null),
            ("Name", "A"));
        WinRmCommandRecord b = Adapter(
            ("GUID", null), ("MACAddress", null), ("PNPDeviceID", null),
            ("Name", "B"));
        string[] first = NetworkAdapterInventoryNormalizer.Normalize([a, b])
            .Select(item => item.AdapterKey).ToArray();
        string[] second = NetworkAdapterInventoryNormalizer.Normalize([b, a])
            .Select(item => item.AdapterKey).ToArray();
        Assert.Equal(first, second);
        Assert.All(first, key => Assert.StartsWith("FALLBACK:", key));
    }

    [Fact]
    public void Adapter_duplicate_key_fails_and_empty_is_valid()
    {
        Assert.Empty(NetworkAdapterInventoryNormalizer.Normalize([]));
        Assert.Throws<WindowsInventoryValidationException>(() =>
            NetworkAdapterInventoryNormalizer.Normalize([Adapter(), Adapter()]));
    }

    [Fact]
    public void Ipv4_expands_arrays_and_uses_adapter_plus_address_identity()
    {
        Ipv4AddressInventoryItem[] items = Ipv4InventoryNormalizer.Normalize(
            [Configuration(
                ("IPAddress", new[] { "192.0.2.10", "198.51.100.7" }),
                ("IPSubnet", new[] { "255.255.255.0", "255.255.255.255" }),
                ("DefaultIPGateway", new[] { "192.0.2.1" }),
                ("DHCPEnabled", true))]);

        Assert.Equal(2, items.Length);
        Assert.All(items, item => Assert.Equal($"GUID:{GuidValue}", item.AdapterKey));
        Assert.Contains(items, item =>
            item.Ipv4Key == $"GUID:{GuidValue}|192.0.2.10" &&
            item.PrefixLength == 24 && item.DefaultGateway == "192.0.2.1" &&
            item.IsDhcp == true);
    }

    [Fact]
    public void Same_address_on_different_adapters_is_distinct()
    {
        Ipv4AddressInventoryItem[] items = Ipv4InventoryNormalizer.Normalize(
            [Configuration(), Configuration(("SettingID", Guid.NewGuid().ToString("D")))]);
        Assert.Equal(2, items.Length);
        Assert.Equal(2, items.Select(item => item.Ipv4Key).Distinct().Count());
    }

    [Fact]
    public void Ipv4_duplicate_gateway_prefix_and_empty_rules_are_validated()
    {
        Assert.Empty(Ipv4InventoryNormalizer.Normalize([]));
        Assert.Throws<WindowsInventoryValidationException>(() =>
            Ipv4InventoryNormalizer.Normalize(
                [Configuration(
                    ("IPAddress", new[] { "192.0.2.10", "192.0.2.10" }),
                    ("IPSubnet", new[] { "255.255.255.0", "255.255.255.0" }))]));
        Assert.Throws<WindowsInventoryValidationException>(() =>
            Ipv4InventoryNormalizer.Normalize(
                [Configuration(("IPSubnet", new[] { "255.0.255.0" }))]));
        Assert.Throws<WindowsInventoryValidationException>(() =>
            Ipv4InventoryNormalizer.Normalize(
                [Configuration(("DefaultIPGateway", new[] { "not-an-ip" }))]));
    }

    [Fact]
    public void Ipv4_order_does_not_change_logical_result()
    {
        WinRmCommandRecord a = Configuration();
        WinRmCommandRecord b = Configuration(
            ("SettingID", Guid.NewGuid().ToString("D")),
            ("IPAddress", new[] { "198.51.100.8" }));
        string[] first = Ipv4InventoryNormalizer.Normalize([a, b])
            .Select(item => item.Ipv4Key).ToArray();
        string[] second = Ipv4InventoryNormalizer.Normalize([b, a])
            .Select(item => item.Ipv4Key).ToArray();
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Independent_modules_reuse_supplied_session_and_ps51_CIM_contracts()
    {
        var session = new TestSession(
            new Dictionary<string, IReadOnlyList<WinRmCommandRecord>>
            {
                ["Win32_NetworkAdapter"] = [Adapter()],
                ["Win32_NetworkAdapterConfiguration"] = [Configuration()],
            });
        InventoryModuleContext context = Context(session);

        var adapterResult = await new NetworkAdapterInventoryModule()
            .CollectAsync(context, default);
        var ipv4Result = await new Ipv4InventoryModule()
            .CollectAsync(context, default);

        Assert.True(adapterResult.IsSuccessful);
        Assert.True(ipv4Result.IsSuccessful);
        Assert.Equal(
            ["Win32_NetworkAdapter", "Win32_NetworkAdapterConfiguration"],
            session.Classes);
        Assert.DoesNotContain("*", NetworkInventoryCommands.Adapters.PropertyNames);
        Assert.DoesNotContain("*", NetworkInventoryCommands.Ipv4Addresses.PropertyNames);
    }

    private static InventoryModuleContext Context(IWinRmCommandSession session) =>
        new(Guid.NewGuid(), "network.ae.local", Guid.NewGuid(), session,
            TimeProvider.System, NullLogger.Instance);

    private static WinRmCommandRecord Adapter(
        params (string Name, object? Value)[] overrides) =>
        With(new Dictionary<string, object?>
        {
            ["GUID"] = GuidValue, ["InterfaceIndex"] = 12, ["Name"] = "Ethernet",
            ["NetConnectionID"] = "Ethernet", ["Description"] = "Adapter",
            ["MACAddress"] = "00:11:22:33:44:55", ["Manufacturer"] = "Contoso",
            ["PhysicalAdapter"] = true, ["NetConnectionStatus"] = 2,
            ["Speed"] = 1_000_000_000L, ["PNPDeviceID"] = @"PCI\VEN_1234",
        }, overrides);

    private static WinRmCommandRecord Configuration(
        params (string Name, object? Value)[] overrides) =>
        With(new Dictionary<string, object?>
        {
            ["SettingID"] = GuidValue, ["InterfaceIndex"] = 12,
            ["MACAddress"] = "00:11:22:33:44:55",
            ["IPAddress"] = new[] { "192.0.2.10" },
            ["IPSubnet"] = new[] { "255.255.255.0" },
            ["DefaultIPGateway"] = Array.Empty<string>(),
            ["DHCPEnabled"] = false,
            ["FullDNSRegistrationEnabledForAllAddresses"] = true,
        }, overrides);

    private static WinRmCommandRecord With(
        Dictionary<string, object?> values,
        params (string Name, object? Value)[] overrides)
    {
        foreach (var (name, value) in overrides)
        {
            values[name] = value;
        }
        return new(values);
    }

    private sealed class TestSession(
        IReadOnlyDictionary<string, IReadOnlyList<WinRmCommandRecord>> results)
        : IWinRmCommandSession
    {
        internal List<string> Classes { get; } = [];
        public bool IsUsable => true;
        public Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string className = Assert.IsType<string>(command.Parameters["ClassName"]);
            Classes.Add(className);
            return Task.FromResult(results[className]);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
