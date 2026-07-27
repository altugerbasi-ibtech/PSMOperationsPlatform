using System.Net;
using System.Net.Sockets;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class NetworkInventoryCommands
{
    private const string StandardCimNamespace = "Root/StandardCimv2";

    internal static readonly WinRmCommandDefinition Adapters = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["Namespace"] = StandardCimNamespace,
            ["ClassName"] = "MSFT_NetAdapter",
            ["Property"] = new[]
            {
                "InterfaceGuid",
                "InterfaceIndex",
                "Name",
                "InterfaceDescription",
                "PermanentAddress",
                "Speed",
                "InterfaceOperationalStatus",
            },
        },
        [
            "InterfaceGuid",
            "InterfaceIndex",
            "Name",
            "InterfaceDescription",
            "PermanentAddress",
            "Speed",
            "InterfaceOperationalStatus",
        ]);

    internal static readonly WinRmCommandDefinition Ipv4Addresses = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["Namespace"] = StandardCimNamespace,
            ["ClassName"] = "MSFT_NetIPAddress",
            ["Filter"] = "AddressFamily = 2",
            ["Property"] = new[]
            {
                "InterfaceIndex",
                "IPAddress",
                "PrefixLength",
            },
        },
        [
            "InterfaceIndex",
            "IPAddress",
            "PrefixLength",
        ]);
}

internal sealed class NetworkInventoryModule(INetworkSnapshotStore store)
    : IWindowsInventoryModule
{
    public WindowsInventoryModuleKind Kind =>
        WindowsInventoryModuleKind.NetworkAdapter;

    public async Task ExecuteAsync(WindowsInventoryExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<WinRmCommandRecord> adapterRecords =
            await context.Session.InvokeAsync(
                NetworkInventoryCommands.Adapters,
                context.CancellationToken);
        IReadOnlyList<WinRmCommandRecord> addressRecords =
            await context.Session.InvokeAsync(
                NetworkInventoryCommands.Ipv4Addresses,
                context.CancellationToken);

        var adapterKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var adapterByIndex = new Dictionary<int, string>();
        var adapters = new List<NetworkAdapterInventoryItem>(
            adapterRecords.Count);
        foreach (WinRmCommandRecord record in adapterRecords)
        {
            string sourceInterfaceGuid =
                WindowsInventoryRecordNormalizer.RequiredString(
                    record,
                    "InterfaceGuid",
                    200);
            if (!Guid.TryParse(sourceInterfaceGuid, out Guid interfaceGuid))
            {
                throw new WindowsInventoryValidationException(
                    "Property 'InterfaceGuid' must contain a GUID.");
            }

            string stableSourceKey = interfaceGuid.ToString("D");
            if (!adapterKeys.Add(stableSourceKey))
            {
                throw new WindowsInventoryValidationException(
                    "Network Adapter inventory contains a duplicate InterfaceGuid.");
            }

            int interfaceIndex =
                WindowsInventoryRecordNormalizer.RequiredNonNegativeInt32(
                    record,
                    "InterfaceIndex");
            if (!adapterByIndex.TryAdd(interfaceIndex, stableSourceKey))
            {
                throw new WindowsInventoryValidationException(
                    "Network Adapter inventory contains a duplicate InterfaceIndex.");
            }

            adapters.Add(
                new NetworkAdapterInventoryItem(
                    stableSourceKey,
                    WindowsInventoryRecordNormalizer.OptionalString(
                        record,
                        "Name",
                        255),
                    WindowsInventoryRecordNormalizer.OptionalString(
                        record,
                        "InterfaceDescription",
                        500),
                    WindowsInventoryRecordNormalizer.OptionalString(
                        record,
                        "PermanentAddress",
                        20),
                    OperationalStatus(record),
                    WindowsInventoryRecordNormalizer.OptionalNonNegativeInt64(
                        record,
                        "Speed")));
        }

        var addressKeys = new HashSet<string>(StringComparer.Ordinal);
        var addresses = new List<Ipv4AddressInventoryItem>(
            addressRecords.Count);
        foreach (WinRmCommandRecord record in addressRecords)
        {
            int interfaceIndex =
                WindowsInventoryRecordNormalizer.RequiredNonNegativeInt32(
                    record,
                    "InterfaceIndex");
            if (!adapterByIndex.TryGetValue(
                interfaceIndex,
                out string? adapterStableSourceKey))
            {
                throw new WindowsInventoryValidationException(
                    "IPv4 address references an unknown InterfaceIndex.");
            }

            string address = CanonicalIpv4(record);
            int prefixLength =
                WindowsInventoryRecordNormalizer.RequiredNonNegativeInt32(
                    record,
                    "PrefixLength");
            if (prefixLength > 32)
            {
                throw new WindowsInventoryValidationException(
                    "IPv4 PrefixLength must be between 0 and 32.");
            }

            string addressKey =
                $"{adapterStableSourceKey}|{address}/{prefixLength}";
            if (!addressKeys.Add(addressKey))
            {
                throw new WindowsInventoryValidationException(
                    "Network Snapshot contains a duplicate IPv4 identity.");
            }

            addresses.Add(
                new Ipv4AddressInventoryItem(
                    adapterStableSourceKey,
                    address,
                    prefixLength));
        }

        await store.ReplaceAsync(
            context.ManagedServer.TargetId,
            new NetworkInventorySnapshot(adapters, addresses),
            context.CancellationToken);
    }

    private static string CanonicalIpv4(WinRmCommandRecord record)
    {
        string address = WindowsInventoryRecordNormalizer.RequiredString(
            record,
            "IPAddress",
            15);
        if (!IPAddress.TryParse(address, out IPAddress? parsedAddress)
            || parsedAddress.AddressFamily != AddressFamily.InterNetwork
            || !string.Equals(
                address,
                parsedAddress.ToString(),
                StringComparison.Ordinal))
        {
            throw new WindowsInventoryValidationException(
                "Property 'IPAddress' must contain canonical IPv4.");
        }

        return address;
    }

    private static string? OperationalStatus(WinRmCommandRecord record) =>
        WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
            record,
            "InterfaceOperationalStatus") switch
        {
            null => null,
            1 => "Up",
            2 => "Down",
            3 => "Testing",
            4 => "Unknown",
            5 => "Dormant",
            6 => "Not Present",
            7 => "Lower Layer Down",
            _ => throw new WindowsInventoryValidationException(
                "InterfaceOperationalStatus contains an unsupported value."),
        };
}
