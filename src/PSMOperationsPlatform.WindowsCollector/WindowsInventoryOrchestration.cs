namespace PSMOperationsPlatform.WindowsCollector;

internal enum WindowsInventoryModuleKind
{
    Computer = 100,
    OperatingSystem = 200,
    Bios = 250,
    Memory = 300,
    Processor = 400,
    Disk = 500,
    Volume = 600,
    NetworkAdapter = 700,
    Ipv4Address = 800,
    WindowsRole = 900,
    WindowsFeature = 1000,
    IisPlatform = 1100,
    DotNetPlatform = 1200,
    PowerShellPlatform = 1300
}

internal enum WindowsInventoryModuleOutcome
{
    Succeeded,
    Failed
}

internal sealed record WindowsInventoryModuleResult(
    WindowsInventoryModuleKind ModuleKind,
    WindowsInventoryModuleOutcome Outcome,
    string? FailureCategory = null);

internal sealed record WindowsInventoryOrchestrationResult(
    Guid TargetId,
    IReadOnlyList<WindowsInventoryModuleResult> ModuleResults)
{
    public bool IsSuccessful =>
        ModuleResults.All(result =>
            result.Outcome == WindowsInventoryModuleOutcome.Succeeded);
}

internal sealed class WindowsInventoryExecutionContext
{
    public WindowsInventoryExecutionContext(
        WindowsTarget managedServer,
        IWinRmCommandSession session,
        CancellationToken cancellationToken,
        TimeProvider timeProvider,
        ILogger logger,
        Guid correlationId)
    {
        ArgumentNullException.ThrowIfNull(managedServer);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation identifier cannot be empty.",
                nameof(correlationId));
        }

        ManagedServer = managedServer;
        Session = session;
        CancellationToken = cancellationToken;
        TimeProvider = timeProvider;
        Logger = logger;
        CorrelationId = correlationId;
    }

    public WindowsTarget ManagedServer { get; }

    public IWinRmCommandSession Session { get; }

    public CancellationToken CancellationToken { get; }

    public TimeProvider TimeProvider { get; }

    public ILogger Logger { get; }

    public Guid CorrelationId { get; }
}

internal interface IWindowsInventoryModule
{
    WindowsInventoryModuleKind Kind { get; }

    Task ExecuteAsync(WindowsInventoryExecutionContext context);
}

internal interface IWindowsInventoryOrchestrator
{
    Task<WindowsInventoryOrchestrationResult> ExecuteAsync(
        WindowsTarget target,
        IWinRmCommandSession session,
        Guid correlationId,
        CancellationToken cancellationToken);
}

internal sealed class WindowsInventoryOrchestrator
    : IWindowsInventoryOrchestrator
{
    private readonly IReadOnlyList<IWindowsInventoryModule> modules;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<WindowsInventoryOrchestrator> logger;

    public WindowsInventoryOrchestrator(
        IEnumerable<IWindowsInventoryModule> modules,
        TimeProvider timeProvider,
        ILogger<WindowsInventoryOrchestrator> logger)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        IWindowsInventoryModule[] ordered = modules
            .OrderBy(module => module.Kind)
            .ToArray();
        WindowsInventoryModuleKind? duplicateKind = ordered
            .GroupBy(module => module.Kind)
            .Where(group => group.Count() > 1)
            .Select(group => (WindowsInventoryModuleKind?)group.Key)
            .SingleOrDefault();
        if (duplicateKind.HasValue)
        {
            throw new InvalidOperationException(
                $"Inventory module kind '{duplicateKind.Value}' is registered more than once.");
        }

        this.modules = ordered;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

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
        WindowsCollectorLog.InventoryStarted(
            logger,
            target.TargetId,
            correlationId,
            modules.Count);

        var context = new WindowsInventoryExecutionContext(
            target,
            session,
            cancellationToken,
            timeProvider,
            logger,
            correlationId);
        var results = new List<WindowsInventoryModuleResult>(modules.Count);

        foreach (IWindowsInventoryModule module in modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long moduleStartedAt = timeProvider.GetTimestamp();
            WindowsCollectorLog.InventoryModuleStarted(
                logger,
                target.TargetId,
                correlationId,
                module.Kind.ToString());

            try
            {
                await module.ExecuteAsync(context);
                results.Add(
                    new WindowsInventoryModuleResult(
                        module.Kind,
                        WindowsInventoryModuleOutcome.Succeeded));
                WindowsCollectorLog.InventoryModuleCompleted(
                    logger,
                    target.TargetId,
                    correlationId,
                    module.Kind.ToString(),
                    timeProvider.GetElapsedTime(moduleStartedAt)
                        .TotalMilliseconds);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                string failureCategory = exception.GetType().Name;
                results.Add(
                    new WindowsInventoryModuleResult(
                        module.Kind,
                        WindowsInventoryModuleOutcome.Failed,
                        failureCategory));
                WindowsCollectorLog.InventoryModuleFailed(
                    logger,
                    target.TargetId,
                    correlationId,
                    module.Kind.ToString(),
                    failureCategory);
                if (!session.IsUsable)
                {
                    break;
                }
            }
        }

        var result = new WindowsInventoryOrchestrationResult(
            target.TargetId,
            results);
        WindowsCollectorLog.InventoryCompleted(
            logger,
            target.TargetId,
            correlationId,
            result.IsSuccessful,
            results.Count,
            results.Count(module =>
                module.Outcome == WindowsInventoryModuleOutcome.Failed),
            timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
        return result;
    }
}
