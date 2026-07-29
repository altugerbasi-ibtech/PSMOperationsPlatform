using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class NetworkInventoryCommands
{
    internal static readonly WinRmCommandDefinition Adapters = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_NetworkAdapter",
            ["Property"] = new[]
            {
                "GUID", "InterfaceIndex", "Name", "NetConnectionID",
                "Description", "MACAddress", "Manufacturer", "PhysicalAdapter",
                "NetConnectionStatus", "Speed", "PNPDeviceID",
            },
        },
        [
            "GUID", "InterfaceIndex", "Name", "NetConnectionID",
            "Description", "MACAddress", "Manufacturer", "PhysicalAdapter",
            "NetConnectionStatus", "Speed", "PNPDeviceID",
        ]);

    internal static readonly WinRmCommandDefinition Ipv4Addresses = new(
        "Get-CimInstance",
        new Dictionary<string, object?>
        {
            ["ClassName"] = "Win32_NetworkAdapterConfiguration",
            ["Filter"] = "IPEnabled = TRUE",
            ["Property"] = new[]
            {
                "SettingID", "InterfaceIndex", "MACAddress", "IPAddress",
                "IPSubnet", "DefaultIPGateway", "DHCPEnabled",
                "FullDNSRegistrationEnabledForAllAddresses",
            },
        },
        [
            "SettingID", "InterfaceIndex", "MACAddress", "IPAddress",
            "IPSubnet", "DefaultIPGateway", "DHCPEnabled",
            "FullDNSRegistrationEnabledForAllAddresses",
        ]);
}

internal static class NetworkAdapterInventoryNormalizer
{
    private static readonly HashSet<string> MacPlaceholders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "00:00:00:00:00:00", "00-00-00-00-00-00",
            "FF:FF:FF:FF:FF:FF", "FF-FF-FF-FF-FF-FF",
        };

    internal static NetworkAdapterInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var candidates = records.Select(record =>
        {
            string? interfaceGuid = NormalizeGuid(Optional(record, "GUID", 100));
            int? interfaceIndex =
                WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                    record, "InterfaceIndex");
            string? name = Optional(record, "Name", 255);
            string? friendlyName = Optional(record, "NetConnectionID", 255);
            string? description = Optional(record, "Description", 500);
            string? mac = NormalizeMac(Optional(record, "MACAddress", 20));
            string? manufacturer = Optional(record, "Manufacturer", 255);
            bool? physical = OptionalBoolean(record, "PhysicalAdapter");
            int? status = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                record, "NetConnectionStatus");
            if (status is > 12)
            {
                throw new WindowsInventoryValidationException(
                    "NetConnectionStatus contains an unsupported value.");
            }
            long? speed = WindowsInventoryRecordNormalizer.OptionalNonNegativeInt64(
                record, "Speed");
            string? pnp = Optional(record, "PNPDeviceID", 500);
            string hash = Hash(
                interfaceGuid, interfaceIndex, name, friendlyName, description,
                mac, manufacturer, physical, status, speed, pnp);
            string? key = interfaceGuid is not null
                ? $"GUID:{interfaceGuid}"
                : StableMac(mac)
                    ? $"MAC:{mac}"
                    : pnp is not null
                        ? $"PNP:{pnp.ToUpperInvariant()}"
                        : null;
            return (item: new NetworkAdapterInventoryItem(
                key ?? string.Empty, interfaceGuid, interfaceIndex, name,
                friendlyName, description, mac, manufacturer, physical, status,
                speed, pnp), key, hash);
        }).OrderBy(candidate => candidate.hash, StringComparer.Ordinal)
          .ThenBy(candidate => candidate.key, StringComparer.Ordinal)
          .ToArray();

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        return candidates.Select(candidate =>
        {
            string key = candidate.key ?? $"FALLBACK:{candidate.hash}:";
            if (candidate.key is null)
            {
                int occurrence = occurrences.GetValueOrDefault(candidate.hash) + 1;
                occurrences[candidate.hash] = occurrence;
                key += $"{occurrence:00}";
            }
            if (!keys.Add(key))
            {
                throw new WindowsInventoryValidationException(
                    "Network Adapter inventory contains an ambiguous duplicate AdapterKey.");
            }
            return candidate.item with { AdapterKey = key };
        }).OrderBy(item => item.AdapterKey, StringComparer.Ordinal).ToArray();
    }

    internal static string? NormalizeGuid(string? value)
    {
        if (value is null)
        {
            return null;
        }
        return Guid.TryParse(value, out Guid parsed) && parsed != Guid.Empty
            ? parsed.ToString("D")
            : throw new WindowsInventoryValidationException(
                "Network adapter GUID is malformed.");
    }

    internal static string? NormalizeMac(string? value)
    {
        if (value is null)
        {
            return null;
        }
        string compact = value.Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        if (compact.Length != 12 || !compact.All(Uri.IsHexDigit))
        {
            throw new WindowsInventoryValidationException(
                "Network adapter MACAddress is malformed.");
        }
        return string.Join(
            ":",
            Enumerable.Range(0, 6).Select(index => compact.Substring(index * 2, 2)));
    }

    internal static bool StableMac(string? value) =>
        value is not null && !MacPlaceholders.Contains(value);

    private static string? Optional(
        WinRmCommandRecord record, string property, int maxLength) =>
        WindowsInventoryRecordNormalizer.OptionalNormalizedString(
            record, property, maxLength);

    private static bool? OptionalBoolean(WinRmCommandRecord record, string property)
    {
        if (!record.Properties.TryGetValue(property, out object? value))
        {
            throw new WindowsInventoryValidationException(
                $"Expected property '{property}' was not projected.");
        }
        return value switch
        {
            null => null,
            bool boolean => boolean,
            _ => throw new WindowsInventoryValidationException(
                $"Property '{property}' must be Boolean."),
        };
    }

    private static string Hash(params object?[] fields)
    {
        string canonical = string.Join(
            '\u001F',
            fields.Select(field => field switch
            {
                null => "<NULL>",
                IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                _ => field.ToString()?.ToUpperInvariant(),
            }));
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..24];
    }
}

internal static class Ipv4InventoryNormalizer
{
    internal static Ipv4AddressInventoryItem[] Normalize(
        IReadOnlyList<WinRmCommandRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var result = new List<Ipv4AddressInventoryItem>();
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (WinRmCommandRecord record in records)
        {
            string? guid = NetworkAdapterInventoryNormalizer.NormalizeGuid(
                Optional(record, "SettingID", 100));
            string? mac = NetworkAdapterInventoryNormalizer.NormalizeMac(
                Optional(record, "MACAddress", 20));
            string adapterKey = guid is not null
                ? $"GUID:{guid}"
                : NetworkAdapterInventoryNormalizer.StableMac(mac)
                    ? $"MAC:{mac}"
                    : throw new WindowsInventoryValidationException(
                        "IPv4 inventory cannot determine AdapterKey.");
            string[] addresses = StringArray(record, "IPAddress");
            string[] subnets = StringArray(record, "IPSubnet");
            if (subnets.Length != addresses.Length)
            {
                throw new WindowsInventoryValidationException(
                    "IPv4 IPAddress and IPSubnet arrays must align.");
            }
            string? gateway = StringArray(record, "DefaultIPGateway")
                .Select(CanonicalIpv4)
                .FirstOrDefault();
            bool? dhcp = OptionalBoolean(record, "DHCPEnabled");
            bool? dnsRegistration = OptionalBoolean(
                record, "FullDNSRegistrationEnabledForAllAddresses");

            for (int index = 0; index < addresses.Length; index++)
            {
                if (!IPAddress.TryParse(addresses[index], out IPAddress? parsed))
                {
                    throw new WindowsInventoryValidationException(
                        "IPAddress contains a malformed value.");
                }
                if (parsed.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }
                string address = CanonicalIpv4(addresses[index]);
                int prefix = PrefixLength(subnets[index]);
                string key = $"{adapterKey}|{address}";
                if (!keys.Add(key))
                {
                    throw new WindowsInventoryValidationException(
                        "IPv4 inventory contains a duplicate IPv4Key.");
                }
                result.Add(new(
                    key, adapterKey, address, prefix, gateway, dhcp,
                    dnsRegistration));
            }
        }
        return result.OrderBy(item => item.AdapterKey, StringComparer.Ordinal)
            .ThenBy(item => item.Address, StringComparer.Ordinal)
            .ToArray();
    }

    internal static string CanonicalIpv4(string address)
    {
        string normalized = address.Trim();
        if (!IPAddress.TryParse(normalized, out IPAddress? parsed) ||
            parsed.AddressFamily != AddressFamily.InterNetwork ||
            !string.Equals(normalized, parsed.ToString(), StringComparison.Ordinal))
        {
            throw new WindowsInventoryValidationException(
                "Address must contain canonical IPv4.");
        }
        return normalized;
    }

    private static int PrefixLength(string subnet)
    {
        IPAddress address = IPAddress.TryParse(subnet, out IPAddress? parsed) &&
            parsed.AddressFamily == AddressFamily.InterNetwork
                ? parsed
                : throw new WindowsInventoryValidationException(
                    "IPv4 subnet mask is malformed.");
        byte[] bytes = address.GetAddressBytes();
        uint mask = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) |
                    ((uint)bytes[2] << 8) | bytes[3];
        bool zeroSeen = false;
        int prefix = 0;
        for (int bit = 31; bit >= 0; bit--)
        {
            bool one = (mask & (1u << bit)) != 0;
            if (one && zeroSeen)
            {
                throw new WindowsInventoryValidationException(
                    "IPv4 subnet mask must be contiguous.");
            }
            zeroSeen |= !one;
            prefix += one ? 1 : 0;
        }
        return prefix;
    }

    private static string[] StringArray(WinRmCommandRecord record, string property)
    {
        if (!record.Properties.TryGetValue(property, out object? value))
        {
            throw new WindowsInventoryValidationException(
                $"Expected property '{property}' was not projected.");
        }
        return value switch
        {
            null => [],
            string single => [single],
            string[] strings => strings,
            object[] objects when objects.All(item => item is string) =>
                objects.Cast<string>().ToArray(),
            _ => throw new WindowsInventoryValidationException(
                $"Property '{property}' must be a string array."),
        };
    }

    private static string? Optional(
        WinRmCommandRecord record, string property, int maxLength) =>
        WindowsInventoryRecordNormalizer.OptionalNormalizedString(
            record, property, maxLength);

    private static bool? OptionalBoolean(WinRmCommandRecord record, string property)
    {
        if (!record.Properties.TryGetValue(property, out object? value))
        {
            throw new WindowsInventoryValidationException(
                $"Expected property '{property}' was not projected.");
        }
        return value switch
        {
            null => null,
            bool boolean => boolean,
            _ => throw new WindowsInventoryValidationException(
                $"Property '{property}' must be Boolean."),
        };
    }
}

internal sealed class NetworkAdapterInventoryModule
    : IInventoryModule<NetworkAdapterInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.NetworkAdapter;

    public async Task<InventoryModuleResult<NetworkAdapterInventoryItem[]>> CollectAsync(
        InventoryModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        long started = context.TimeProvider.GetTimestamp();
        int raw = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records =
                await context.Session.InvokeAsync(
                    NetworkInventoryCommands.Adapters, cancellationToken);
            raw = records.Count;
            NetworkAdapterInventoryItem[] items =
                NetworkAdapterInventoryNormalizer.Normalize(records);
            return InventoryModuleResult<NetworkAdapterInventoryItem[]>.Success(
                items, items.Length == 0, raw, items.Length,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return InventoryModuleResult<NetworkAdapterInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), raw,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

internal sealed class Ipv4InventoryModule
    : IInventoryModule<Ipv4AddressInventoryItem[]>
{
    public WindowsInventoryModuleKind Kind => WindowsInventoryModuleKind.Ipv4Address;

    public async Task<InventoryModuleResult<Ipv4AddressInventoryItem[]>> CollectAsync(
        InventoryModuleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        long started = context.TimeProvider.GetTimestamp();
        int raw = 0;
        try
        {
            IReadOnlyList<WinRmCommandRecord> records =
                await context.Session.InvokeAsync(
                    NetworkInventoryCommands.Ipv4Addresses, cancellationToken);
            raw = records.Count;
            Ipv4AddressInventoryItem[] items = Ipv4InventoryNormalizer.Normalize(records);
            return InventoryModuleResult<Ipv4AddressInventoryItem[]>.Success(
                items, items.Length == 0, raw, items.Length,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return InventoryModuleResult<Ipv4AddressInventoryItem[]>.Failure(
                InventoryModuleFailure.Category(exception), raw,
                context.TimeProvider.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}
