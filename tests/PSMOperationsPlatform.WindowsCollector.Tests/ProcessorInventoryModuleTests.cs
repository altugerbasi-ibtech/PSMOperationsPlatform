using Microsoft.Extensions.Logging.Abstractions;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class ProcessorInventoryModuleTests
{
    [Fact]
    public async Task Module_uses_shared_session_and_returns_normalized_result()
    {
        var session = new TestSession([ValidRecord("CPU0")]);
        var context = new InventoryModuleContext(
            Guid.NewGuid(), "processor.ae.local", Guid.NewGuid(), session,
            TimeProvider.System, NullLogger.Instance);

        InventoryModuleResult<Infrastructure.Persistence.ProcessorInventoryItem[]> result =
            await new ProcessorInventoryModule().CollectAsync(context, default);

        Assert.True(result.IsSuccessful);
        Assert.Equal("DEVICE:CPU0", Assert.Single(result.NormalizedResult!).ProcessorKey);
        Assert.Same(session, context.Session);
        Assert.Equal("Win32_Processor", session.Command!.Parameters["ClassName"]);
    }

    [Fact]
    public void Empty_processor_collection_is_invalid() =>
        Assert.Throws<WindowsInventoryValidationException>(
            () => ProcessorInventoryNormalizer.Normalize([]));

    [Fact]
    public void Input_order_does_not_change_processor_keys()
    {
        string[] first = ProcessorInventoryNormalizer.Normalize(
            [ValidRecord("CPU1"), ValidRecord("CPU0")]).Select(x => x.ProcessorKey).ToArray();
        string[] second = ProcessorInventoryNormalizer.Normalize(
            [ValidRecord("CPU0"), ValidRecord("CPU1")]).Select(x => x.ProcessorKey).ToArray();
        Assert.Equal(first, second);
    }

    private static WinRmCommandRecord ValidRecord(string deviceId) => Record(
        ("DeviceID", deviceId), ("Name", "Processor"), ("Manufacturer", "Contoso"),
        ("Description", "64-bit processor"), ("SocketDesignation", null),
        ("ProcessorId", null), ("NumberOfCores", 8U),
        ("NumberOfLogicalProcessors", 16U), ("MaxClockSpeed", 3200U),
        ("CurrentClockSpeed", 2800U), ("AddressWidth", 64), ("DataWidth", 64),
        ("Architecture", 9), ("VirtualizationFirmwareEnabled", true),
        ("SecondLevelAddressTranslationExtensions", true),
        ("VMMonitorModeExtensions", true));

    private static WinRmCommandRecord Record(params (string Name, object? Value)[] values) =>
        new(new Dictionary<string, object?>(
            values.ToDictionary(value => value.Name, value => value.Value),
            StringComparer.OrdinalIgnoreCase));

    private sealed class TestSession(IReadOnlyList<WinRmCommandRecord> records)
        : IWinRmCommandSession
    {
        internal WinRmCommandDefinition? Command { get; private set; }
        public bool IsUsable => true;
        public Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command, CancellationToken cancellationToken)
        {
            Command = command;
            return Task.FromResult(records);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
