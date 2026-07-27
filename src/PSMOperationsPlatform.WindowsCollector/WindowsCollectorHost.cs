using System.Reflection;
using Microsoft.Extensions.Options;
using PSMOperationsPlatform.Infrastructure.Configuration;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector;

public static class WindowsCollectorHost
{
    public const string ServiceName =
        "PSM Operations Platform Windows Collector";

    public static HostApplicationBuilder CreateApplicationBuilder(
        string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Configuration.ConfigurePsmConfiguration(
            builder.Environment.EnvironmentName,
            args,
            Assembly.GetExecutingAssembly());

        builder.Services
            .AddOptions<WindowsCollectorOptions>()
            .BindConfiguration(WindowsCollectorOptions.SectionName)
            .Validate(
                options => options.PollingInterval > TimeSpan.Zero,
                "WindowsCollector:PollingInterval must be positive.")
            .ValidateOnStart();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddOperationsDatabasePersistence();
        builder.Services.AddHealthChecks();
        builder.Services.AddWindowsService(options =>
            options.ServiceName = ServiceName);
        builder.Services.AddScoped<IWindowsTargetProvider, WindowsTargetProvider>();
        builder.Services.AddSingleton<IWinRmSessionFactory, PowerShellWinRmSessionFactory>();
        builder.Services.AddSingleton<IWinRmTransportClient, WinRmTransportClient>();
        builder.Services.AddSingleton<IWindowsConnectivityProbe, WindowsConnectivityProbe>();
        builder.Services.AddScoped<
            IWindowsInventoryOrchestrator,
            WindowsInventoryOrchestrator>();
        builder.Services.AddScoped<IWindowsInventoryModule, ComputerInventoryModule>();
        builder.Services.AddScoped<
            IWindowsInventoryModule,
            OperatingSystemInventoryModule>();
        builder.Services.AddScoped<IWindowsInventoryModule, MemoryInventoryModule>();
        builder.Services.AddScoped<IWindowsInventoryModule, ProcessorInventoryModule>();
        builder.Services.AddScoped<IWindowsInventoryModule, DiskInventoryModule>();
        builder.Services.AddScoped<IWindowsInventoryModule, VolumeInventoryModule>();
        builder.Services.AddScoped<IWindowsInventoryModule, NetworkInventoryModule>();
        builder.Services.AddScoped<
            IManagedServerConnectivityStore,
            ManagedServerConnectivityStore>();
        builder.Services.AddScoped<
            IConnectivityResultPersistence,
            ConnectivityResultPersistence>();
        builder.Services.AddScoped<IWindowsCollectorCycle, WindowsCollectorCycle>();
        builder.Services.AddHostedService<Worker>();

        return builder;
    }

    public static async Task RunAsync(
        string[] args,
        CancellationToken cancellationToken = default)
    {
        using IHost host = CreateApplicationBuilder(args).Build();
        await host.RunAsync(cancellationToken);
    }
}
