namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class ProcessorMemoryInventoryContractTests
{
    [Fact]
    public void Processor_uses_socket_then_valid_processor_id_fallbacks()
    {
        var socket = Processor(" ", socket: " CPU Socket 1 ", processorId: "ID-1");
        var processorId = Processor(" ", socket: null, processorId: "BFEBFBFF000906EA");

        Assert.Equal(
            ["PROCESSORID:BFEBFBFF000906EA", "SOCKET:CPU SOCKET 1"],
            ProcessorInventoryNormalizer.Normalize([socket, processorId])
                .Select(item => item.ProcessorKey));
    }

    [Theory]
    [InlineData("0000000000000000")]
    [InlineData("FFFFFFFFFFFFFFFF")]
    [InlineData("Unknown")]
    [InlineData("Default string")]
    public void Placeholder_processor_id_uses_deterministic_fallback(string processorId)
    {
        string key = Assert.Single(
            ProcessorInventoryNormalizer.Normalize(
                [Processor(null, socket: null, processorId: processorId)]))
            .ProcessorKey;
        Assert.StartsWith("FALLBACK:", key, StringComparison.Ordinal);
    }

    [Fact]
    public void Processor_fallback_keys_are_stable_when_input_order_changes()
    {
        WinRmCommandRecord first = Processor(null, name: "Processor A");
        WinRmCommandRecord second = Processor(null, name: "Processor B");

        string[] forward = ProcessorInventoryNormalizer.Normalize([first, second])
            .Select(item => item.ProcessorKey).ToArray();
        string[] reverse = ProcessorInventoryNormalizer.Normalize([second, first])
            .Select(item => item.ProcessorKey).ToArray();

        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void Duplicate_processor_key_is_ambiguous() =>
        Assert.Throws<WindowsInventoryValidationException>(
            () => ProcessorInventoryNormalizer.Normalize(
                [Processor("CPU0"), Processor("cpu0")]));

    [Fact]
    public void Exact_duplicate_fallback_processor_rows_receive_stable_occurrences()
    {
        WinRmCommandRecord record = Processor(null);
        Assert.Equal(
            2,
            ProcessorInventoryNormalizer.Normalize([record, record])
                .Select(item => item.ProcessorKey)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void Logical_count_less_than_core_count_is_invalid()
    {
        WinRmCommandRecord record = Processor("CPU0", cores: 8, logical: 4);
        Assert.Throws<WindowsInventoryValidationException>(
            () => ProcessorInventoryNormalizer.Normalize([record]));
    }

    [Fact]
    public void Malformed_processor_numeric_value_is_invalid()
    {
        WinRmCommandRecord record = With(
            Processor("CPU0"), "CurrentClockSpeed", "fast");
        Assert.Throws<WindowsInventoryValidationException>(
            () => ProcessorInventoryNormalizer.Normalize([record]));
    }

    [Fact]
    public void Processor_capability_flags_are_parsed_without_virtualization_inference()
    {
        var item = Assert.Single(ProcessorInventoryNormalizer.Normalize(
            [Processor("CPU0", name: "Virtual CPU", virtualization: false)]));
        Assert.False(item.VirtualizationFirmwareEnabled);
        Assert.True(item.SecondLevelAddressTranslationExtensions);
        Assert.True(item.VmMonitorModeExtensions);
    }

    [Fact]
    public void Memory_prefers_device_bank_then_valid_serial_keys()
    {
        var items = PhysicalMemoryInventoryNormalizer.Normalize(
            [
                Memory(device: " DIMM 0 ", bank: "BANK 0", serial: "SERIAL-A"),
                Memory(device: null, bank: " BANK 1 ", serial: "SERIAL-B"),
                Memory(device: null, bank: null, serial: " SERIAL-C "),
            ]);

        Assert.Equal(
            ["BANK:BANK 1", "DEVICE:DIMM 0", "SERIAL:SERIAL-C"],
            items.Select(item => item.ModuleKey));
    }

    [Theory]
    [InlineData("00000000")]
    [InlineData("FFFFFFFF")]
    [InlineData("Unknown")]
    [InlineData("Default string")]
    [InlineData("To Be Filled By O.E.M.")]
    [InlineData("System Serial Number")]
    [InlineData("Not Specified")]
    public void Placeholder_memory_serial_uses_fallback(string serial)
    {
        string key = Assert.Single(
            PhysicalMemoryInventoryNormalizer.Normalize(
                [Memory(device: null, bank: null, serial: serial)]))
            .ModuleKey;
        Assert.StartsWith("FALLBACK:", key, StringComparison.Ordinal);
    }

    [Fact]
    public void Memory_fallback_keys_are_stable_when_input_order_changes()
    {
        WinRmCommandRecord first = Memory(device: null, bank: null, serial: null, capacity: 1024);
        WinRmCommandRecord second = Memory(device: null, bank: null, serial: null, capacity: 2048);

        string[] forward = PhysicalMemoryInventoryNormalizer.Normalize([first, second])
            .Select(item => item.ModuleKey).ToArray();
        string[] reverse = PhysicalMemoryInventoryNormalizer.Normalize([second, first])
            .Select(item => item.ModuleKey).ToArray();

        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void Duplicate_memory_key_is_ambiguous() =>
        Assert.Throws<WindowsInventoryValidationException>(
            () => PhysicalMemoryInventoryNormalizer.Normalize(
                [Memory("DIMM0"), Memory("dimm0")]));

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Non_positive_memory_capacity_is_invalid(long capacity) =>
        Assert.Throws<WindowsInventoryValidationException>(
            () => PhysicalMemoryInventoryNormalizer.Normalize(
                [Memory("DIMM0", capacity: capacity)]));

    [Fact]
    public void Malformed_memory_speed_fails_the_complete_collection()
    {
        WinRmCommandRecord invalid = With(Memory("DIMM1"), "Speed", "fast");
        Assert.Throws<WindowsInventoryValidationException>(
            () => PhysicalMemoryInventoryNormalizer.Normalize(
                [Memory("DIMM0"), invalid]));
    }

    [Fact]
    public void Memory_allows_null_and_zero_speed_values()
    {
        var nullSpeed = Assert.Single(PhysicalMemoryInventoryNormalizer.Normalize(
            [Memory("DIMM0", speed: null, configuredSpeed: null)]));
        var zeroSpeed = Assert.Single(PhysicalMemoryInventoryNormalizer.Normalize(
            [Memory("DIMM1", speed: 0, configuredSpeed: 0)]));
        Assert.Null(nullSpeed.SpeedMHz);
        Assert.Equal(0, zeroSpeed.SpeedMHz);
        Assert.Equal(0, zeroSpeed.ConfiguredClockSpeedMHz);
    }

    [Fact]
    public void Successful_empty_memory_is_valid() =>
        Assert.Empty(PhysicalMemoryInventoryNormalizer.Normalize([]));

    private static WinRmCommandRecord Processor(
        string? deviceId,
        string? socket = null,
        string? processorId = null,
        string? name = "Processor",
        int cores = 4,
        int logical = 8,
        bool virtualization = true) =>
        Record(
            ("DeviceID", deviceId), ("Name", name),
            ("Manufacturer", "Contoso"), ("Description", "64-bit processor"),
            ("SocketDesignation", socket), ("ProcessorId", processorId),
            ("NumberOfCores", cores), ("NumberOfLogicalProcessors", logical),
            ("MaxClockSpeed", 3200), ("CurrentClockSpeed", 2800),
            ("AddressWidth", 64), ("DataWidth", 64), ("Architecture", 9),
            ("VirtualizationFirmwareEnabled", virtualization),
            ("SecondLevelAddressTranslationExtensions", true),
            ("VMMonitorModeExtensions", true));

    private static WinRmCommandRecord Memory(
        string? device,
        string? bank = null,
        string? serial = "SERIAL",
        long capacity = 1024,
        int? speed = 3200,
        int? configuredSpeed = 2933) =>
        Record(
            ("DeviceLocator", device), ("BankLabel", bank),
            ("Capacity", capacity), ("Speed", speed),
            ("ConfiguredClockSpeed", configuredSpeed),
            ("Manufacturer", null), ("PartNumber", null),
            ("SerialNumber", serial), ("FormFactor", 8), ("MemoryType", 26));

    private static WinRmCommandRecord Record(
        params (string Name, object? Value)[] values) =>
        new(new Dictionary<string, object?>(
            values.ToDictionary(value => value.Name, value => value.Value),
            StringComparer.OrdinalIgnoreCase));

    private static WinRmCommandRecord With(
        WinRmCommandRecord record,
        string property,
        object? value)
    {
        var properties = new Dictionary<string, object?>(
            record.Properties,
            StringComparer.OrdinalIgnoreCase)
        {
            [property] = value,
        };
        return new(properties);
    }
}
