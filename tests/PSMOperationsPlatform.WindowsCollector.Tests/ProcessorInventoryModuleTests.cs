using Microsoft.Extensions.Logging.Abstractions;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class ProcessorInventoryModuleTests
{
    [Fact]
    public async Task Module_normalizes_complete_snapshot_and_uses_device_id_key()
    {
        var store = new RecordingStore();
        var module = new ProcessorInventoryModule(store);
        var session = new ProcessorSession(
            [
                Record(
                    ("DeviceID", "CPU0"),
                    ("Name", "Processor A"),
                    ("Manufacturer", "Contoso"),
                    ("NumberOfCores", 8U),
                    ("NumberOfLogicalProcessors", 16U),
                    ("MaxClockSpeed", 3200U)),
                Record(
                    ("DeviceID", "CPU1"),
                    ("Name", null),
                    ("Manufacturer", null),
                    ("NumberOfCores", 4),
                    ("NumberOfLogicalProcessors", 8),
                    ("MaxClockSpeed", 2800)),
            ]);

        await module.ExecuteAsync(Context(session));

        Assert.Same(ProcessorInventoryCommand.Definition, session.Command);
        Assert.Collection(
            store.Items!,
            item =>
            {
                Assert.Equal("CPU0", item.StableSourceKey);
                Assert.Equal("Processor A", item.Name);
                Assert.Equal(8, item.CoreCount);
                Assert.Equal(16, item.LogicalProcessorCount);
                Assert.Equal(3200, item.MaxClockSpeedMhz);
            },
            item =>
            {
                Assert.Equal("CPU1", item.StableSourceKey);
                Assert.Null(item.Name);
                Assert.Equal(4, item.CoreCount);
            });
    }

    [Fact]
    public async Task Successful_empty_collection_calls_replace_with_empty_snapshot()
    {
        var store = new RecordingStore();

        await new ProcessorInventoryModule(store).ExecuteAsync(
            Context(new ProcessorSession([])));

        Assert.NotNull(store.Items);
        Assert.Empty(store.Items);
    }

    [Fact]
    public async Task Duplicate_device_id_fails_before_store()
    {
        var store = new RecordingStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new ProcessorInventoryModule(store).ExecuteAsync(
                Context(
                    new ProcessorSession(
                        [
                            ValidRecord("CPU0"),
                            ValidRecord("cpu0"),
                        ]))));

        Assert.Null(store.Items);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" CPU0")]
    public async Task Invalid_device_id_fails_before_store(string deviceId)
    {
        var store = new RecordingStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new ProcessorInventoryModule(store).ExecuteAsync(
                Context(new ProcessorSession([ValidRecord(deviceId)]))));

        Assert.Null(store.Items);
    }

    [Theory]
    [InlineData("NumberOfCores")]
    [InlineData("NumberOfLogicalProcessors")]
    [InlineData("MaxClockSpeed")]
    public async Task Non_positive_numeric_property_fails_before_store(
        string propertyName)
    {
        WinRmCommandRecord record = ValidRecord("CPU0");
        var properties = new Dictionary<string, object?>(
            record.Properties,
            StringComparer.OrdinalIgnoreCase)
        {
            [propertyName] = -1,
        };
        var store = new RecordingStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new ProcessorInventoryModule(store).ExecuteAsync(
                Context(new ProcessorSession([new WinRmCommandRecord(properties)]))));

        Assert.Null(store.Items);
    }

    [Fact]
    public async Task Whitespace_name_fails_before_store()
    {
        WinRmCommandRecord record = ValidRecord("CPU0");
        var properties = new Dictionary<string, object?>(
            record.Properties,
            StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = " ",
        };
        var store = new RecordingStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new ProcessorInventoryModule(store).ExecuteAsync(
                Context(new ProcessorSession([new WinRmCommandRecord(properties)]))));

        Assert.Null(store.Items);
    }

    [Fact]
    public async Task Cancellation_from_session_propagates_without_store_call()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new RecordingStore();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ProcessorInventoryModule(store).ExecuteAsync(
                Context(new ProcessorSession([]), cancellation.Token)));

        Assert.Null(store.Items);
    }

    [Fact]
    public void Projection_is_allowlisted_and_contains_only_persisted_fields()
    {
        WinRmCommandDefinition command = ProcessorInventoryCommand.Definition;

        Assert.Equal("Get-CimInstance", command.CommandName);
        Assert.Equal("Win32_Processor", command.Parameters["ClassName"]);
        Assert.Equal(
            [
                "DeviceID",
                "Name",
                "Manufacturer",
                "NumberOfCores",
                "NumberOfLogicalProcessors",
                "MaxClockSpeed",
            ],
            command.PropertyNames);
        Assert.Equal(
            command.PropertyNames,
            Assert.IsType<string[]>(command.Parameters["Property"]));
        Assert.DoesNotContain("SocketDesignation", command.PropertyNames);
        Assert.DoesNotContain("ProcessorId", command.PropertyNames);
        Assert.DoesNotContain("*", command.PropertyNames);
    }

    private static WindowsInventoryExecutionContext Context(
        IWinRmCommandSession session,
        CancellationToken cancellationToken = default) =>
        new(
            new WindowsTarget(
                Guid.NewGuid(),
                "processor.ae.local",
                WinRmTransportMode.Auto,
                5986,
                5985,
                TimeSpan.FromSeconds(10)),
            session,
            cancellationToken,
            TimeProvider.System,
            NullLogger.Instance,
            Guid.NewGuid());

    private static WinRmCommandRecord ValidRecord(string deviceId) =>
        Record(
            ("DeviceID", deviceId),
            ("Name", "Processor"),
            ("Manufacturer", "Contoso"),
            ("NumberOfCores", 8U),
            ("NumberOfLogicalProcessors", 16U),
            ("MaxClockSpeed", 3200U));

    private static WinRmCommandRecord Record(
        params (string Name, object? Value)[] values) =>
        new(new Dictionary<string, object?>(
            values.ToDictionary(value => value.Name, value => value.Value),
            StringComparer.OrdinalIgnoreCase));

    private sealed class ProcessorSession(
        IReadOnlyList<WinRmCommandRecord> records) : IWinRmCommandSession
    {
        internal WinRmCommandDefinition? Command { get; private set; }

        public bool IsUsable => true;

        public Task OpenAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Command = command;
            return Task.FromResult(records);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingStore : IProcessorSnapshotStore
    {
        internal IReadOnlyList<ProcessorInventoryItem>? Items { get; private set; }

        public Task ReplaceAsync(
            Guid managedServerId,
            IReadOnlyList<ProcessorInventoryItem> items,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Items = items;
            return Task.CompletedTask;
        }
    }
}
