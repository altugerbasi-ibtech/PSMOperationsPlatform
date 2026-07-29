using System.Globalization;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

internal static class InventoryPlaceholderNormalizer
{
    private static readonly HashSet<string> GeneralValues =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "To Be Filled By O.E.M.",
            "Default string",
            "Not Specified",
            "Unknown",
        };

    private static readonly HashSet<string> SerialValues =
        new(GeneralValues, StringComparer.OrdinalIgnoreCase)
        {
            "System Serial Number",
            "00000000",
        };

    private static readonly string[] VirtualMachineMarkers =
    [
        "Virtual Machine", "VMware", "VirtualBox", "KVM", "Xen",
        "QEMU", "Amazon EC2", "Google Compute Engine", "Nutanix",
        "OpenStack",
    ];

    internal static string? General(string? value) =>
        value is null || GeneralValues.Contains(value) ? null : value;

    internal static string? Serial(string? value) =>
        value is null || SerialValues.Contains(value) ? null : value;

    internal static Guid? Uuid(string? value)
    {
        if (value is null)
        {
            return null;
        }
        if (!Guid.TryParse(value, out Guid result))
        {
            throw new WindowsInventoryValidationException(
                "Computer system product UUID is not a valid GUID.");
        }
        if (result == Guid.Empty
            || result == Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"))
        {
            return null;
        }
        return result;
    }

    internal static bool? VirtualMachine(string? manufacturer, string? model)
    {
        if (manufacturer is null && model is null)
        {
            return null;
        }
        string candidate = $"{manufacturer} {model}";
        return VirtualMachineMarkers.Any(
            marker => candidate.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class AtomicWindowsInventoryOrchestrator(
    ICoreWindowsInventoryStore store,
    IInventoryScheduleStore scheduleStore,
    TimeProvider timeProvider,
    ILogger<AtomicWindowsInventoryOrchestrator> logger)
    : IWindowsInventoryOrchestrator
{
    private static readonly TimeSpan SuccessfulInterval = TimeSpan.FromHours(6);

    public async Task<WindowsInventoryOrchestrationResult> ExecuteAsync(
        WindowsTarget target,
        IWinRmCommandSession session,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        long startedAt = timeProvider.GetTimestamp();
        WindowsCollectorLog.InventoryStarted(logger, target.TargetId, correlationId, 8);

        try
        {
            CoreWindowsInventorySnapshot snapshot =
                await CollectAsync(target, session, correlationId, cancellationToken);
            DateTime capturedAt = timeProvider.GetLocalNow().DateTime;
            await store.ReplaceAsync(
                target.TargetId,
                snapshot,
                correlationId,
                capturedAt,
                capturedAt.Add(SuccessfulInterval),
                cancellationToken);
            var results = Enum.GetValues<WindowsInventoryModuleKind>()
                .Select(kind => new WindowsInventoryModuleResult(
                    kind,
                    WindowsInventoryModuleOutcome.Succeeded))
                .ToArray();
            WindowsCollectorLog.InventoryCompleted(
                logger, target.TargetId, correlationId, true,
                results.Length, 0,
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
            return new(target.TargetId, results);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (CoreInventoryModuleException exception)
        {
            await RecordFailureSafelyAsync(
                target.TargetId,
                exception.FailureCategory,
                cancellationToken);
            WindowsCollectorLog.InventoryModuleFailed(
                logger, target.TargetId, correlationId,
                exception.ModuleKind.ToString(), exception.FailureCategory);
            WindowsCollectorLog.InventoryCompleted(
                logger, target.TargetId, correlationId, false, 1, 1,
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
            return new(
                target.TargetId,
                [new(exception.ModuleKind, WindowsInventoryModuleOutcome.Failed,
                    exception.FailureCategory)]);
        }
        catch (Exception exception)
        {
            string category = exception is TimeoutException
                ? "Timeout"
                : exception is WindowsInventoryValidationException or ArgumentException
                    ? "ValidationFailure"
                    : "PersistenceFailure";
            await RecordFailureSafelyAsync(
                target.TargetId,
                category,
                cancellationToken);
            WindowsCollectorLog.InventoryCompleted(
                logger, target.TargetId, correlationId, false, 0, 1,
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
            return new(
                target.TargetId,
                [new(WindowsInventoryModuleKind.Computer,
                    WindowsInventoryModuleOutcome.Failed, category)]);
        }
    }

    private async Task<CoreWindowsInventorySnapshot> CollectAsync(
        WindowsTarget target,
        IWinRmCommandSession session,
        Guid runId,
        CancellationToken cancellationToken)
    {
        ComputerInventoryState computer = await CollectModuleAsync(
            WindowsInventoryModuleKind.Computer,
            async () =>
            {
                WinRmCommandRecord record = WindowsInventoryRecordNormalizer.Single(
                    await session.InvokeAsync(
                        CoreWindowsInventoryCommands.ComputerSystem,
                        cancellationToken),
                    "Computer");
                WinRmCommandRecord product = WindowsInventoryRecordNormalizer.Single(
                    await session.InvokeAsync(
                        CoreWindowsInventoryCommands.ComputerSystemProduct,
                        cancellationToken),
                    "Computer System Product");
                string? manufacturer = InventoryPlaceholderNormalizer.General(
                    WindowsInventoryRecordNormalizer.OptionalNormalizedString(record, "Manufacturer", 255));
                string? model = InventoryPlaceholderNormalizer.General(
                    WindowsInventoryRecordNormalizer.OptionalNormalizedString(record, "Model", 255));
                return new ComputerInventoryState(
                    WindowsInventoryRecordNormalizer.RequiredNormalizedString(record, "Name", 255),
                    target.HostName,
                    WindowsInventoryRecordNormalizer.OptionalNormalizedString(record, "Domain", 255),
                    manufacturer,
                    model,
                    InventoryPlaceholderNormalizer.Serial(
                        WindowsInventoryRecordNormalizer.OptionalNormalizedString(product, "IdentifyingNumber", 255)),
                    WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(record, "DomainRole"),
                    WindowsInventoryRecordNormalizer.OptionalNormalizedString(record, "SystemType", 100),
                    InventoryPlaceholderNormalizer.VirtualMachine(manufacturer, model),
                    InventoryPlaceholderNormalizer.Uuid(
                        WindowsInventoryRecordNormalizer.OptionalNormalizedString(product, "UUID", 50)));
            },
            target,
            runId);

        OperatingSystemInventoryState operatingSystem = await CollectModuleAsync(
            WindowsInventoryModuleKind.OperatingSystem,
            async () =>
            {
                WinRmCommandRecord record = WindowsInventoryRecordNormalizer.Single(
                    await session.InvokeAsync(
                        CoreWindowsInventoryCommands.OperatingSystem,
                        cancellationToken),
                    "Operating System");
                return new OperatingSystemInventoryState(
                    WindowsInventoryRecordNormalizer.RequiredNormalizedString(record, "Caption", 255),
                    WindowsInventoryRecordNormalizer.RequiredNormalizedString(record, "Version", 100),
                    WindowsInventoryRecordNormalizer.RequiredNormalizedString(record, "BuildNumber", 50),
                    WindowsInventoryRecordNormalizer.RequiredNormalizedString(record, "OSArchitecture", 50),
                    Edition: WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                        record, "OperatingSystemSKU")?.ToString(CultureInfo.InvariantCulture),
                    InstallDate: WindowsInventoryRecordNormalizer.OptionalDateTime(
                        record, "InstallDate", timeProvider),
                    LastBootTime: WindowsInventoryRecordNormalizer.OptionalDateTime(
                        record, "LastBootUpTime", timeProvider),
                    ProductType: WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(
                        record, "ProductType"),
                    InstallationType: WindowsInventoryRecordNormalizer.OptionalNormalizedString(
                        record, "InstallationType", 100),
                    SystemDrive: WindowsInventoryRecordNormalizer.OptionalNormalizedString(
                        record, "SystemDrive", 10),
                    WindowsDirectory: WindowsInventoryRecordNormalizer.OptionalNormalizedString(
                        record, "WindowsDirectory", 260),
                    Locale: WindowsInventoryRecordNormalizer.OptionalNormalizedString(
                        record, "Locale", 20),
                    CurrentTimeZoneMinutes: WindowsInventoryRecordNormalizer.OptionalInt32(
                        record, "CurrentTimeZone"));
            },
            target,
            runId);

        BiosInventoryState bios = await CollectModuleAsync(
            WindowsInventoryModuleKind.Bios,
            async () =>
            {
                WinRmCommandRecord record = WindowsInventoryRecordNormalizer.Single(
                    await session.InvokeAsync(
                        CoreWindowsInventoryCommands.Bios,
                        cancellationToken),
                    "BIOS");
                return new BiosInventoryState(
                    InventoryPlaceholderNormalizer.General(
                        WindowsInventoryRecordNormalizer.OptionalNormalizedString(record, "Manufacturer", 255)),
                    WindowsInventoryRecordNormalizer.OptionalNormalizedString(record, "SMBIOSBIOSVersion", 255),
                    WindowsInventoryRecordNormalizer.OptionalNormalizedString(record, "Version", 255),
                    WindowsInventoryRecordNormalizer.OptionalDateTime(record, "ReleaseDate", timeProvider),
                    InventoryPlaceholderNormalizer.Serial(
                        WindowsInventoryRecordNormalizer.OptionalNormalizedString(record, "SerialNumber", 255)),
                    WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(record, "SMBIOSMajorVersion"),
                    WindowsInventoryRecordNormalizer.OptionalNonNegativeInt32(record, "SMBIOSMinorVersion"));
            },
            target,
            runId);

        PhysicalMemoryInventoryItem[] memory = await CollectModuleAsync(
            WindowsInventoryModuleKind.Memory,
            async () =>
            {
                IReadOnlyList<WinRmCommandRecord> records = await session.InvokeAsync(
                    PhysicalMemoryInventoryCommand.Definition,
                    cancellationToken);
                PhysicalMemoryInventoryItem[] items =
                    PhysicalMemoryInventoryNormalizer.Normalize(records);
                WindowsCollectorLog.InventoryModuleContractValidated(
                    logger, target.TargetId, target.HostName, runId, "Memory",
                    records.Count, items.Length, "Valid",
                    items.Length == 0 ? "ValidEmpty" : "NotEmpty",
                    "PendingAtomicCommit");
                return items;
            },
            target,
            runId);

        ProcessorInventoryItem[] processors = await CollectModuleAsync(
            WindowsInventoryModuleKind.Processor,
            async () =>
            {
                IReadOnlyList<WinRmCommandRecord> records = await session.InvokeAsync(
                    ProcessorInventoryCommand.Definition,
                    cancellationToken);
                ProcessorInventoryItem[] items =
                    ProcessorInventoryNormalizer.Normalize(records);
                WindowsCollectorLog.InventoryModuleContractValidated(
                    logger, target.TargetId, target.HostName, runId, "Processor",
                    records.Count, items.Length, "Valid", "NotEmpty",
                    "PendingAtomicCommit");
                return items;
            },
            target,
            runId);

        DiskInventoryItem[] disks = await CollectModuleAsync(
            WindowsInventoryModuleKind.Disk,
            async () => RequireModuleResult(
                await new PhysicalDiskInventoryModule().CollectAsync(
                    new InventoryModuleContext(
                        target.TargetId, target.HostName, runId, session,
                        timeProvider, logger),
                    cancellationToken)),
            target,
            runId);

        VolumeInventoryItem[] volumes = await CollectModuleAsync(
            WindowsInventoryModuleKind.Volume,
            async () => RequireModuleResult(
                await new VolumeInventoryModule().CollectAsync(
                    new InventoryModuleContext(
                        target.TargetId, target.HostName, runId, session,
                        timeProvider, logger),
                    cancellationToken)),
            target,
            runId);

        NetworkInventorySnapshot network = await CollectModuleAsync(
            WindowsInventoryModuleKind.NetworkAdapter,
            async () =>
            {
                var context = new InventoryModuleContext(
                    target.TargetId, target.HostName, runId, session,
                    timeProvider, logger);
                NetworkAdapterInventoryItem[] adapters = RequireModuleResult(
                    await new NetworkAdapterInventoryModule().CollectAsync(
                        context, cancellationToken));
                Ipv4AddressInventoryItem[] addresses = RequireModuleResult(
                    await new Ipv4InventoryModule().CollectAsync(
                        context, cancellationToken));
                return new NetworkInventorySnapshot(adapters, addresses);
            },
            target,
            runId);

        var discoveryContext = new InventoryModuleContext(
            target.TargetId, target.HostName, runId, session,
            timeProvider, logger);
        WindowsRoleInventoryItem[] roles = await CollectModuleAsync(
            WindowsInventoryModuleKind.WindowsRole,
            async () => RequireModuleResult(
                await new WindowsRoleDiscoveryModule().CollectAsync(
                    discoveryContext, cancellationToken)),
            target, runId);
        WindowsFeatureInventoryItem[] features = await CollectModuleAsync(
            WindowsInventoryModuleKind.WindowsFeature,
            async () => RequireModuleResult(
                await new WindowsFeatureDiscoveryModule().CollectAsync(
                    discoveryContext, cancellationToken)),
            target, runId);
        IisPlatformInventoryItem[] iis = await CollectModuleAsync(
            WindowsInventoryModuleKind.IisPlatform,
            async () => RequireModuleResult(
                await new IisPlatformDiscoveryModule().CollectAsync(
                    discoveryContext, cancellationToken)),
            target, runId);
        DotNetPlatformInventoryItem[] dotNet = await CollectModuleAsync(
            WindowsInventoryModuleKind.DotNetPlatform,
            async () => RequireModuleResult(
                await new DotNetPlatformDiscoveryModule().CollectAsync(
                    discoveryContext, cancellationToken)),
            target, runId);
        PowerShellPlatformInventoryItem[] powerShell = await CollectModuleAsync(
            WindowsInventoryModuleKind.PowerShellPlatform,
            async () => RequireModuleResult(
                await new PowerShellPlatformDiscoveryModule().CollectAsync(
                    discoveryContext, cancellationToken)),
            target, runId);

        return new(
            computer, operatingSystem, bios, processors, memory,
            disks, volumes, network, roles, features, iis, dotNet, powerShell);
    }

    private async Task<T> CollectModuleAsync<T>(
        WindowsInventoryModuleKind kind,
        Func<Task<T>> collect,
        WindowsTarget target,
        Guid runId)
    {
        long startedAt = timeProvider.GetTimestamp();
        WindowsCollectorLog.InventoryModuleStarted(
            logger, target.TargetId, runId, kind.ToString());
        try
        {
            T result = await collect();
            int count = result switch
            {
                Array array => array.Length,
                NetworkInventorySnapshot network => network.Adapters.Count + network.Ipv4Addresses.Count,
                _ => 1,
            };
            WindowsCollectorLog.InventoryModuleCompleted(
                logger, target.TargetId, runId, kind.ToString(),
                timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
            WindowsCollectorLog.InventoryModuleNormalized(
                logger, target.TargetId, target.HostName, runId,
                kind.ToString(), count);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            string category = exception switch
            {
                WinRmCommandExecutionException => "CollectionFailure",
                WindowsInventoryValidationException => "ValidationFailure",
                FormatException or OverflowException => "ParsingFailure",
                TimeoutException => "Timeout",
                InventoryModuleResultException module => module.Category,
                _ => "Unexpected",
            };
            throw new CoreInventoryModuleException(kind, category, exception);
        }
    }

    private static T RequireModuleResult<T>(InventoryModuleResult<T> result) =>
        result.IsSuccessful && result.IsValid && result.NormalizedResult is not null
            ? result.NormalizedResult
            : throw new InventoryModuleResultException(
                result.FailureCategory ?? "Unexpected");

    private sealed class InventoryModuleResultException(string category)
        : InvalidOperationException("Inventory module collection did not produce a valid result.")
    {
        internal string Category { get; } = category;
    }

    private async Task RecordFailureSafelyAsync(
        Guid targetId,
        string category,
        CancellationToken cancellationToken)
    {
        try
        {
            await scheduleStore.RecordFailureAsync(
                targetId,
                timeProvider.GetLocalNow().DateTime,
                category,
                cancellationToken);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            // Inventory scheduling failure must not terminate the target loop.
        }
    }

    private static DiskInventoryItem[] NormalizeDisks(
        IReadOnlyList<WinRmCommandRecord> records)
        => PhysicalDiskInventoryNormalizer.Normalize(records);

    private static VolumeInventoryItem[] NormalizeVolumes(
        IReadOnlyList<WinRmCommandRecord> records)
        => VolumeInventoryNormalizer.Normalize(records);

}

internal sealed class CoreInventoryModuleException(
    WindowsInventoryModuleKind moduleKind,
    string failureCategory,
    Exception innerException)
    : Exception("Core inventory module failed.", innerException)
{
    internal WindowsInventoryModuleKind ModuleKind { get; } = moduleKind;
    internal string FailureCategory { get; } = failureCategory;
}
