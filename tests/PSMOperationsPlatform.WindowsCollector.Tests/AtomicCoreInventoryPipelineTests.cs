using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class AtomicCoreInventoryPipelineTests
{
    [Fact]
    public async Task Collects_all_core_data_before_one_store_call_on_shared_session()
    {
        var session = new CoreSession();
        var store = new CapturingCoreStore();
        var schedule = new CapturingScheduleStore();
        var orchestrator = Create(store, schedule);

        Guid inventoryRunId = Guid.NewGuid();
        WindowsInventoryOrchestrationResult result = await orchestrator.ExecuteAsync(
            Target(), session, inventoryRunId, default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(15, session.InvocationCount);
        CoreWindowsInventorySnapshot snapshot = Assert.IsType<CoreWindowsInventorySnapshot>(
            store.Snapshot);
        Assert.Single(snapshot.Processors);
        Assert.Empty(snapshot.MemoryModules);
        Assert.Empty(snapshot.Disks);
        Assert.Single(snapshot.Volumes);
        Assert.Empty(snapshot.Network.Adapters);
        Assert.Empty(snapshot.Network.Ipv4Addresses);
        Assert.Empty(snapshot.WindowsRoles!);
        Assert.Empty(snapshot.WindowsFeatures!);
        Assert.Empty(snapshot.IisPlatforms!);
        Assert.Empty(snapshot.DotNetPlatforms!);
        Assert.Empty(snapshot.PowerShellPlatforms!);
        Assert.Equal(1, store.CallCount);
        Assert.Equal(inventoryRunId, store.InventoryRunId);
        Assert.Equal(0, schedule.CallCount);
        Assert.False(session.PersistenceObservedDuringCollection);
    }

    [Theory]
    [InlineData("Win32_ComputerSystem", "Computer")]
    [InlineData("Win32_OperatingSystem", "OperatingSystem")]
    [InlineData("Win32_BIOS", "Bios")]
    public async Task Invalid_empty_singular_module_prevents_all_core_persistence(
        string emptyClass,
        string expectedModule)
    {
        var store = new CapturingCoreStore();
        var schedule = new CapturingScheduleStore();

        WindowsInventoryOrchestrationResult result = await Create(store, schedule)
            .ExecuteAsync(Target(), new CoreSession(emptyClass: emptyClass), Guid.NewGuid(), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(expectedModule, Assert.Single(result.ModuleResults).ModuleKind.ToString());
        Assert.Equal("ValidationFailure", schedule.Category);
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task Timeout_preserves_previous_state_and_is_not_connectivity_failure()
    {
        var store = new CapturingCoreStore();
        var schedule = new CapturingScheduleStore();

        WindowsInventoryOrchestrationResult result = await Create(store, schedule)
            .ExecuteAsync(Target(), new CoreSession(timeoutClass: "Win32_BIOS"), Guid.NewGuid(), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal("Timeout", schedule.Category);
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task Structured_logs_identify_target_module_and_run_without_raw_bios_data()
    {
        var store = new CapturingCoreStore();
        var schedule = new CapturingScheduleStore();
        var logger = new ListLogger<AtomicWindowsInventoryOrchestrator>();
        Guid runId = Guid.NewGuid();
        var orchestrator = new AtomicWindowsInventoryOrchestrator(
            store, schedule, new FixedTimeProvider(), logger);

        await orchestrator.ExecuteAsync(Target(), new CoreSession(), runId, default);

        string log = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains(runId.ToString(), log, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("myapp01.ae.local", log, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bios", log, StringComparison.Ordinal);
        Assert.Contains("RawResultCount=1", log, StringComparison.Ordinal);
        Assert.Contains("EmptyResultStatus=ValidEmpty", log, StringComparison.Ordinal);
        Assert.DoesNotContain("BIOS-SERIAL", log, StringComparison.Ordinal);
        Assert.DoesNotContain("BFEBFBFF000906EA", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_empty_processor_prevents_all_persistence_and_records_failure()
    {
        var session = new CoreSession(emptyProcessor: true);
        var store = new CapturingCoreStore();
        var schedule = new CapturingScheduleStore();
        var orchestrator = Create(store, schedule);

        WindowsInventoryOrchestrationResult result = await orchestrator.ExecuteAsync(
            Target(), session, Guid.NewGuid(), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, store.CallCount);
        Assert.Equal(1, schedule.CallCount);
        Assert.Equal("ValidationFailure", schedule.Category);
    }

    [Fact]
    public async Task Cancellation_stops_collection_without_persistence_or_failure_schedule()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var store = new CapturingCoreStore();
        var schedule = new CapturingScheduleStore();
        var orchestrator = Create(store, schedule);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            orchestrator.ExecuteAsync(
                Target(), new CoreSession(), Guid.NewGuid(), cancellation.Token));

        Assert.Equal(0, store.CallCount);
        Assert.Equal(0, schedule.CallCount);
    }

    [Fact]
    public async Task Duplicate_physical_memory_module_key_is_ambiguous_and_preserves_state()
    {
        var store = new CapturingCoreStore();
        var schedule = new CapturingScheduleStore();
        var orchestrator = Create(store, schedule);

        WindowsInventoryOrchestrationResult result = await orchestrator.ExecuteAsync(
            Target(), new CoreSession(duplicateMemory: true), Guid.NewGuid(), default);

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, store.CallCount);
        Assert.Equal("ValidationFailure", schedule.Category);
    }

    private static AtomicWindowsInventoryOrchestrator Create(
        CapturingCoreStore store,
        CapturingScheduleStore schedule)
    {
        CoreSession.PersistenceProbe = () => store.CallCount > 0;
        return new(
            store,
            schedule,
            new FixedTimeProvider(),
            NullLogger<AtomicWindowsInventoryOrchestrator>.Instance);
    }

    private static WindowsTarget Target() => new(
        Guid.NewGuid(), "myapp01.ae.local", WinRmTransportMode.HttpOnly,
        5986, 5985, TimeSpan.FromSeconds(10));

    private sealed class CoreSession(
        bool emptyProcessor = false,
        bool duplicateMemory = false,
        string? emptyClass = null,
        string? timeoutClass = null)
        : IWinRmCommandSession
    {
        internal static Func<bool> PersistenceProbe { get; set; } = () => false;
        public bool IsUsable => true;
        public int InvocationCount { get; private set; }
        public bool PersistenceObservedDuringCollection { get; private set; }
        public Task OpenAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<WinRmCommandRecord>> InvokeAsync(
            WinRmCommandDefinition command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InvocationCount++;
            PersistenceObservedDuringCollection |= PersistenceProbe();
            string className = command.Parameters.TryGetValue(
                "ClassName", out object? value)
                ? (string)value!
                : command.CommandName;
            if (string.Equals(className, timeoutClass, StringComparison.Ordinal))
            {
                throw new TimeoutException("Deterministic collection timeout.");
            }
            if (string.Equals(className, emptyClass, StringComparison.Ordinal))
            {
                return Task.FromResult<IReadOnlyList<WinRmCommandRecord>>([]);
            }
            IReadOnlyList<WinRmCommandRecord> result = className switch
            {
                "Win32_ComputerSystem" => [Record(
                    ("Name", "MYAPP01"), ("Domain", "ae.local"),
                    ("DomainRole", 3), ("Manufacturer", "Contoso"),
                    ("Model", "Virtual Machine"), ("SystemType", "x64-based PC"))],
                "Win32_ComputerSystemProduct" => [Record(
                    ("UUID", "550e8400-e29b-41d4-a716-446655440000"),
                    ("IdentifyingNumber", "SYSTEM-1"))],
                "Win32_BIOS" => [Record(
                    ("Manufacturer", "Contoso"), ("SMBIOSBIOSVersion", "1.2.3"),
                    ("Version", "BIOS-1"), ("ReleaseDate", null),
                    ("SerialNumber", "BIOS-SERIAL"), ("SMBIOSMajorVersion", 3),
                    ("SMBIOSMinorVersion", 5))],
                "Win32_OperatingSystem" => [Record(
                    ("Caption", "Windows Server 2022"),
                    ("Version", "10.0"), ("BuildNumber", "20348"),
                    ("OSArchitecture", "64-bit"), ("ProductType", 3),
                    ("OperatingSystemSKU", 7), ("InstallationType", "Server Core"),
                    ("SystemDrive", "C:"), ("WindowsDirectory", @"C:\Windows"),
                    ("Locale", "0409"), ("CurrentTimeZone", 180),
                    ("InstallDate", null),
                    ("LastBootUpTime", null))],
                "Win32_PhysicalMemory" when duplicateMemory =>
                [
                    Memory("DIMM0", 1024L),
                    Memory(" dimm0 ", 2048L),
                ],
                "Win32_PhysicalMemory" => [],
                "Win32_Processor" when emptyProcessor => [],
                "Win32_Processor" => [Record(
                    ("DeviceID", "CPU0"), ("Name", "Processor"),
                    ("Manufacturer", "Contoso"), ("Description", "Processor"),
                    ("SocketDesignation", "CPU Socket 0"),
                    ("ProcessorId", "BFEBFBFF000906EA"),
                    ("NumberOfCores", 4),
                    ("NumberOfLogicalProcessors", 8), ("MaxClockSpeed", 3000),
                    ("CurrentClockSpeed", 2800), ("AddressWidth", 64),
                    ("DataWidth", 64), ("Architecture", 9),
                    ("VirtualizationFirmwareEnabled", true),
                    ("SecondLevelAddressTranslationExtensions", true),
                    ("VMMonitorModeExtensions", true))],
                "Win32_DiskDrive" => [],
                "Win32_Volume" => [Record(
                    ("DeviceID", @"\\?\Volume{abc}\"), ("DriveLetter", "C:"),
                    ("Label", "System"), ("FileSystem", "NTFS"),
                    ("Capacity", 1000L), ("FreeSpace", 500L),
                    ("BlockSize", 4096), ("DriveType", 3),
                    ("BootVolume", true), ("SystemVolume", true),
                    ("PageFilePresent", true), ("DirtyBitSet", false),
                    ("SerialNumber", "1234"))],
                "Win32_NetworkAdapter" => [],
                "Win32_NetworkAdapterConfiguration" => [],
                "Get-WindowsFeature" => [],
                "Get-ItemProperty" => [],
                "Get-Command" => [],
                _ => throw new InvalidOperationException(className),
            };
            return Task.FromResult(result);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static WinRmCommandRecord Record(
            params (string Name, object? Value)[] values) =>
            new(values.ToDictionary(x => x.Name, x => x.Value));

        private static WinRmCommandRecord Memory(
            string locator,
            long capacity) => Record(
                ("DeviceLocator", locator), ("BankLabel", null),
                ("Capacity", capacity), ("Speed", 3200),
                ("ConfiguredClockSpeed", 2933), ("Manufacturer", "Contoso"),
                ("PartNumber", "P1"), ("SerialNumber", "S1"),
                ("FormFactor", 8), ("MemoryType", 26));
    }

    private sealed class CapturingCoreStore : ICoreWindowsInventoryStore
    {
        public int CallCount { get; private set; }
        public CoreWindowsInventorySnapshot? Snapshot { get; private set; }
        public Guid InventoryRunId { get; private set; }

        public Task ReplaceAsync(
            Guid managedServerId,
            CoreWindowsInventorySnapshot snapshot,
            Guid inventoryRunId,
            DateTime capturedAt,
            DateTime nextInventoryAttemptAt,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Snapshot = snapshot;
            InventoryRunId = inventoryRunId;
            Assert.Equal(TimeSpan.FromHours(6), nextInventoryAttemptAt - capturedAt);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingScheduleStore : IInventoryScheduleStore
    {
        public int CallCount { get; private set; }
        public string? Category { get; private set; }

        public Task RecordFailureAsync(
            Guid managedServerId,
            DateTime attemptedAt,
            string failureCategory,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Category = failureCategory;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private static readonly DateTimeOffset Now =
            new(2026, 7, 28, 12, 0, 0, TimeSpan.FromHours(3));
        public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone =>
            TimeZoneInfo.CreateCustomTimeZone("Türkiye Test", TimeSpan.FromHours(3), "Türkiye Test", "Türkiye Test");
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
