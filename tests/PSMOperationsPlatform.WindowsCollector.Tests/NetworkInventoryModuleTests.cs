using Microsoft.Extensions.Logging.Abstractions;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class NetworkInventoryModuleTests
{
    private const string InterfaceGuid =
        "4f36f9db-5c17-43f9-93e2-2c7ef9012364";

    [Fact]
    public async Task Module_builds_one_related_network_snapshot()
    {
        var store = new NetworkStore();
        var session = new NetworkSession(
            [
                AdapterRecord(
                    $"{{{InterfaceGuid.ToUpperInvariant()}}}",
                    12),
            ],
            [
                AddressRecord(12, "192.0.2.10", 24),
                AddressRecord(12, "198.51.100.7", 32),
            ]);

        await new NetworkInventoryModule(store).ExecuteAsync(Context(session));

        Assert.Equal(
            [NetworkInventoryCommands.Adapters, NetworkInventoryCommands.Ipv4Addresses],
            session.Commands);
        NetworkAdapterInventoryItem adapter = Assert.Single(store.Snapshot!.Adapters);
        Assert.Equal(InterfaceGuid, adapter.StableSourceKey);
        Assert.Equal("Ethernet", adapter.Name);
        Assert.Equal("00-11-22-33-44-55", adapter.MacAddress);
        Assert.Equal("Up", adapter.OperationalStatus);
        Assert.Equal(1_000_000_000, adapter.LinkSpeedBitsPerSecond);
        Assert.All(
            store.Snapshot.Ipv4Addresses,
            address => Assert.Equal(
                InterfaceGuid,
                address.NetworkAdapterStableSourceKey));
    }

    [Fact]
    public async Task Successful_empty_collection_sends_one_empty_snapshot()
    {
        var store = new NetworkStore();

        await new NetworkInventoryModule(store).ExecuteAsync(
            Context(new NetworkSession([], [])));

        Assert.Empty(store.Snapshot!.Adapters);
        Assert.Empty(store.Snapshot.Ipv4Addresses);
        Assert.Equal(1, store.CallCount);
    }

    [Fact]
    public async Task Observable_down_disabled_virtual_and_special_ipv4_state_is_included()
    {
        const string disconnectedGuid =
            "a6f1e1b7-8ac8-45b6-9d64-b69427be4770";
        const string disabledGuid =
            "55353bbc-f084-4438-9e85-31f195f19948";
        const string virtualGuid =
            "7b690503-50f6-4886-a876-6e68c53f1e26";
        var store = new NetworkStore();
        var session = new NetworkSession(
            [
                AdapterRecord(
                    disconnectedGuid,
                    20,
                    name: "Disconnected",
                    description: "Disconnected Adapter",
                    operationalStatus: 2),
                AdapterRecord(
                    disabledGuid,
                    21,
                    name: "Disabled",
                    description: "Disabled Adapter",
                    operationalStatus: 2),
                AdapterRecord(
                    virtualGuid,
                    22,
                    name: "vEthernet",
                    description: "Hyper-V Virtual Ethernet Adapter",
                    operationalStatus: 1),
            ],
            [
                AddressRecord(20, "169.254.10.20", 16),
                AddressRecord(22, "127.0.0.1", 8),
            ]);

        await new NetworkInventoryModule(store).ExecuteAsync(Context(session));

        Assert.Collection(
            store.Snapshot!.Adapters,
            adapter =>
            {
                Assert.Equal(disconnectedGuid, adapter.StableSourceKey);
                Assert.Equal("Down", adapter.OperationalStatus);
            },
            adapter =>
            {
                Assert.Equal(disabledGuid, adapter.StableSourceKey);
                Assert.Equal("Disabled", adapter.Name);
            },
            adapter =>
            {
                Assert.Equal(virtualGuid, adapter.StableSourceKey);
                Assert.Contains("Virtual", adapter.InterfaceDescription);
            });
        Assert.Contains(
            store.Snapshot.Ipv4Addresses,
            address => address.Address == "169.254.10.20");
        Assert.Contains(
            store.Snapshot.Ipv4Addresses,
            address => address.Address == "127.0.0.1");
    }

    [Fact]
    public async Task Duplicate_adapter_guid_fails_before_store()
    {
        var store = new NetworkStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new NetworkInventoryModule(store).ExecuteAsync(
                Context(
                    new NetworkSession(
                        [
                            AdapterRecord(InterfaceGuid, 12),
                            AdapterRecord(
                                $"{{{InterfaceGuid.ToUpperInvariant()}}}",
                                13),
                        ],
                        []))));

        Assert.Null(store.Snapshot);
    }

    [Fact]
    public async Task Unknown_adapter_reference_fails_before_store()
    {
        var store = new NetworkStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new NetworkInventoryModule(store).ExecuteAsync(
                Context(
                    new NetworkSession(
                        [AdapterRecord(InterfaceGuid, 12)],
                        [AddressRecord(99, "192.0.2.10", 24)]))));

        Assert.Null(store.Snapshot);
    }

    [Fact]
    public async Task Duplicate_ipv4_identity_fails_before_store()
    {
        var store = new NetworkStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new NetworkInventoryModule(store).ExecuteAsync(
                Context(
                    new NetworkSession(
                        [AdapterRecord(InterfaceGuid, 12)],
                        [
                            AddressRecord(12, "192.0.2.10", 24),
                            AddressRecord(12, "192.0.2.10", 24),
                        ]))));

        Assert.Null(store.Snapshot);
    }

    [Theory]
    [InlineData("2001:db8::1", 64)]
    [InlineData("::ffff:192.0.2.1", 24)]
    [InlineData("192.000.002.001", 24)]
    [InlineData("192.0.2.1", 33)]
    public async Task Invalid_ipv4_or_prefix_fails_before_store(
        string address,
        int prefixLength)
    {
        var store = new NetworkStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new NetworkInventoryModule(store).ExecuteAsync(
                Context(
                    new NetworkSession(
                        [AdapterRecord(InterfaceGuid, 12)],
                        [AddressRecord(12, address, prefixLength)]))));

        Assert.Null(store.Snapshot);
    }

    [Fact]
    public async Task Cancellation_propagates_without_store_call()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new NetworkStore();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new NetworkInventoryModule(store).ExecuteAsync(
                Context(new NetworkSession([], []), cancellation.Token)));

        Assert.Null(store.Snapshot);
    }

    [Fact]
    public void Projections_are_explicit_and_ipv4_query_excludes_ipv6()
    {
        Assert.Equal(
            [
                "InterfaceGuid",
                "InterfaceIndex",
                "Name",
                "InterfaceDescription",
                "PermanentAddress",
                "Speed",
                "InterfaceOperationalStatus",
            ],
            NetworkInventoryCommands.Adapters.PropertyNames);
        Assert.Equal(
            ["InterfaceIndex", "IPAddress", "PrefixLength"],
            NetworkInventoryCommands.Ipv4Addresses.PropertyNames);
        Assert.Equal(
            "AddressFamily = 2",
            NetworkInventoryCommands.Ipv4Addresses.Parameters["Filter"]);
        Assert.Equal(
            "MSFT_NetAdapter",
            NetworkInventoryCommands.Adapters.Parameters["ClassName"]);
        Assert.Equal(
            "MSFT_NetIPAddress",
            NetworkInventoryCommands.Ipv4Addresses.Parameters["ClassName"]);
        Assert.DoesNotContain(
            "AddressFamily",
            NetworkInventoryCommands.Ipv4Addresses.PropertyNames);
        Assert.DoesNotContain("*", NetworkInventoryCommands.Adapters.PropertyNames);
        Assert.DoesNotContain("*", NetworkInventoryCommands.Ipv4Addresses.PropertyNames);
    }

    private static WindowsInventoryExecutionContext Context(
        IWinRmCommandSession session,
        CancellationToken cancellationToken = default) =>
        new(
            new WindowsTarget(
                Guid.NewGuid(),
                "network.ae.local",
                WinRmTransportMode.Auto,
                5986,
                5985,
                TimeSpan.FromSeconds(10)),
            session,
            cancellationToken,
            TimeProvider.System,
            NullLogger.Instance,
            Guid.NewGuid());

    private static WinRmCommandRecord AdapterRecord(
        string interfaceGuid,
        int interfaceIndex,
        string name = "Ethernet",
        string description = "Network Adapter",
        uint operationalStatus = 1) =>
        Record(
            ("InterfaceGuid", interfaceGuid),
            ("InterfaceIndex", interfaceIndex),
            ("Name", name),
            ("InterfaceDescription", description),
            ("PermanentAddress", "00-11-22-33-44-55"),
            ("Speed", 1_000_000_000UL),
            ("InterfaceOperationalStatus", operationalStatus));

    private static WinRmCommandRecord AddressRecord(
        int interfaceIndex,
        string address,
        int prefixLength) =>
        Record(
            ("InterfaceIndex", interfaceIndex),
            ("IPAddress", address),
            ("PrefixLength", prefixLength));

    private static WinRmCommandRecord Record(
        params (string Name, object? Value)[] values) =>
        new(new Dictionary<string, object?>(
            values.ToDictionary(value => value.Name, value => value.Value),
            StringComparer.OrdinalIgnoreCase));

    private sealed class NetworkSession(
        IReadOnlyList<WinRmCommandRecord> adapters,
        IReadOnlyList<WinRmCommandRecord> addresses) : IWinRmCommandSession
    {
        internal List<WinRmCommandDefinition> Commands { get; } = [];

        public bool IsUsable => true;

        public Task OpenAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(
                ReferenceEquals(command, NetworkInventoryCommands.Adapters)
                    ? adapters
                    : addresses);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NetworkStore : INetworkSnapshotStore
    {
        internal NetworkInventorySnapshot? Snapshot { get; private set; }
        internal int CallCount { get; private set; }

        public Task ReplaceAsync(
            Guid managedServerId,
            NetworkInventorySnapshot snapshot,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Snapshot = snapshot;
            CallCount++;
            return Task.CompletedTask;
        }
    }
}
