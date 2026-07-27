using Microsoft.Extensions.Logging.Abstractions;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class CoreWindowsInventoryModuleTests
{
    [Fact]
    public async Task Computer_module_projects_normalizes_and_persists_once()
    {
        var session = new RecordingSession(
            [
                Record(
                    ("Name", "APP01"),
                    ("Domain", "ae.local"),
                    ("Manufacturer", "Contoso"),
                    ("Model", "Model 1")),
            ],
            [Record(("SerialNumber", "SERIAL-1"))]);
        var store = new ComputerStore();
        var module = new ComputerInventoryModule(store);

        await module.ExecuteAsync(Context(session));

        Assert.Equal(
            [CoreWindowsInventoryCommands.ComputerSystem, CoreWindowsInventoryCommands.Bios],
            session.Commands);
        Assert.NotNull(store.State);
        Assert.Equal("APP01", store.State.ComputerName);
        Assert.Equal("ae.local", store.State.DomainName);
        Assert.Equal("SERIAL-1", store.State.SerialNumber);
        Assert.Null(store.State.Fqdn);
    }

    [Fact]
    public async Task Operating_system_module_maps_only_approved_properties()
    {
        DateTime installed = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);
        DateTime booted = new(2026, 7, 27, 8, 0, 0, DateTimeKind.Unspecified);
        var session = new RecordingSession(
            [
                Record(
                    ("Caption", "Microsoft Windows Server"),
                    ("Version", "10.0.20348"),
                    ("BuildNumber", "20348"),
                    ("OSArchitecture", "64-bit"),
                    ("InstallDate", installed),
                    ("LastBootUpTime", booted)),
            ]);
        var store = new OperatingSystemStore();
        var module = new OperatingSystemInventoryModule(store);

        await module.ExecuteAsync(Context(session));

        Assert.Same(CoreWindowsInventoryCommands.OperatingSystem, Assert.Single(session.Commands));
        Assert.Equal("20348", store.State!.BuildNumber);
        Assert.Equal("64-bit", store.State.Architecture);
        Assert.Equal(installed, store.State.InstallDate);
        Assert.Equal(booted, store.State.LastBootTime);
        Assert.Null(store.State.Edition);
        Assert.Null(store.State.TimeZoneId);
    }

    [Fact]
    public async Task Operating_system_module_converts_offset_timestamp_to_repository_local_time()
    {
        var session = new RecordingSession(
            [
                Record(
                    ("Caption", "Microsoft Windows Server"),
                    ("Version", "10.0.20348"),
                    ("BuildNumber", "20348"),
                    ("OSArchitecture", "64-bit"),
                    ("InstallDate", new DateTimeOffset(2026, 7, 27, 8, 0, 0, TimeSpan.Zero)),
                    ("LastBootUpTime", new DateTime(2026, 7, 27, 7, 0, 0, DateTimeKind.Utc))),
            ]);
        var store = new OperatingSystemStore();

        await new OperatingSystemInventoryModule(store).ExecuteAsync(
            Context(session, new TurkiyeTimeProvider()));

        Assert.Equal(
            new DateTime(2026, 7, 27, 11, 0, 0, DateTimeKind.Unspecified),
            store.State!.InstallDate);
        Assert.Equal(
            new DateTime(2026, 7, 27, 10, 0, 0, DateTimeKind.Unspecified),
            store.State.LastBootTime);
    }

    [Fact]
    public async Task Memory_module_accepts_unsigned_cim_value_and_persists()
    {
        var session = new RecordingSession(
            [Record(("TotalPhysicalMemory", 34_359_738_368UL))]);
        var store = new MemoryStore();
        var module = new MemoryInventoryModule(store);

        await module.ExecuteAsync(Context(session));

        Assert.Same(CoreWindowsInventoryCommands.Memory, Assert.Single(session.Commands));
        Assert.Equal(34_359_738_368L, store.State!.TotalPhysicalMemoryBytes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Required_computer_name_failure_does_not_call_store(string? name)
    {
        var session = new RecordingSession(
            [
                Record(
                    ("Name", name),
                    ("Domain", "ae.local"),
                    ("Manufacturer", null),
                    ("Model", null)),
            ],
            [Record(("SerialNumber", null))]);
        var store = new ComputerStore();
        var module = new ComputerInventoryModule(store);

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => module.ExecuteAsync(Context(session)));

        Assert.Null(store.State);
    }

    [Fact]
    public async Task Invalid_timestamp_does_not_call_operating_system_store()
    {
        var session = new RecordingSession(
            [
                Record(
                    ("Caption", "Windows"),
                    ("Version", "10"),
                    ("BuildNumber", "1"),
                    ("OSArchitecture", "64-bit"),
                    ("InstallDate", "not-a-timestamp"),
                    ("LastBootUpTime", null)),
            ]);
        var store = new OperatingSystemStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new OperatingSystemInventoryModule(store)
                .ExecuteAsync(Context(session)));

        Assert.Null(store.State);
    }

    [Fact]
    public async Task Negative_memory_does_not_call_store()
    {
        var store = new MemoryStore();

        await Assert.ThrowsAsync<WindowsInventoryValidationException>(
            () => new MemoryInventoryModule(store).ExecuteAsync(
                Context(new RecordingSession(
                    [Record(("TotalPhysicalMemory", -1L))]))));

        Assert.Null(store.State);
    }

    [Fact]
    public void Commands_are_allowlisted_object_projections()
    {
        WinRmCommandDefinition[] commands =
        [
            CoreWindowsInventoryCommands.ComputerSystem,
            CoreWindowsInventoryCommands.Bios,
            CoreWindowsInventoryCommands.OperatingSystem,
            CoreWindowsInventoryCommands.Memory,
        ];

        Assert.All(commands, command =>
        {
            Assert.Equal("Get-CimInstance", command.CommandName);
            Assert.NotEmpty(command.PropertyNames);
            Assert.DoesNotContain("*", command.PropertyNames);
            Assert.Equal(command.PropertyNames, Assert.IsType<string[]>(command.Parameters["Property"]));
            Assert.DoesNotContain(
                command.CommandName,
                new[] { "Format-Table", "Format-List", "Out-String" });
        });
        Assert.Equal(
            [
                "Caption",
                "Version",
                "BuildNumber",
                "OSArchitecture",
                "InstallDate",
                "LastBootUpTime",
            ],
            CoreWindowsInventoryCommands.OperatingSystem.PropertyNames);
    }

    private static WindowsInventoryExecutionContext Context(
        IWinRmCommandSession session,
        TimeProvider? timeProvider = null) =>
        new(
            new WindowsTarget(
                Guid.NewGuid(),
                "target.ae.local",
                WinRmTransportMode.Auto,
                5986,
                5985,
                TimeSpan.FromSeconds(10)),
            session,
            CancellationToken.None,
            timeProvider ?? TimeProvider.System,
            NullLogger.Instance,
            Guid.NewGuid());

    private static WinRmCommandRecord Record(
        params (string Name, object? Value)[] values) =>
        new(new Dictionary<string, object?>(
            values.ToDictionary(value => value.Name, value => value.Value),
            StringComparer.OrdinalIgnoreCase));

    private sealed class RecordingSession(
        params IReadOnlyList<WinRmCommandRecord>[] results)
        : IWinRmCommandSession
    {
        private int invocation;

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
            return Task.FromResult(results[invocation++]);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ComputerStore : IComputerInventoryStore
    {
        internal ComputerInventoryState? State { get; private set; }

        public Task UpsertAsync(
            Guid managedServerId,
            ComputerInventoryState state,
            CancellationToken cancellationToken)
        {
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class OperatingSystemStore : IOperatingSystemInventoryStore
    {
        internal OperatingSystemInventoryState? State { get; private set; }

        public Task UpsertAsync(
            Guid managedServerId,
            OperatingSystemInventoryState state,
            CancellationToken cancellationToken)
        {
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryStore : IMemoryInventoryStore
    {
        internal MemoryInventoryState? State { get; private set; }

        public Task UpsertAsync(
            Guid managedServerId,
            MemoryInventoryState state,
            CancellationToken cancellationToken)
        {
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class TurkiyeTimeProvider : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone(
                "Test-Turkiye",
                TimeSpan.FromHours(3),
                "Test Turkiye",
                "Test Turkiye");
    }
}
