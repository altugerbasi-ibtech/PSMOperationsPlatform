using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PSMOperationsPlatform.Application.Capabilities;
using PSMOperationsPlatform.Application.Decisions;
using PSMOperationsPlatform.Application.ExecutionPlanning;
using PSMOperationsPlatform.Application.Runtime;
using PSMOperationsPlatform.CollectorSdk;
using PSMOperationsPlatform.Infrastructure.Configuration;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public static class OperationsDatabasePersistenceServiceCollectionExtensions
{
    private const int MaximumRetryCount = 3;
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(10);

    public static IServiceCollection AddOperationsDatabasePersistence(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOperationsDatabaseConfiguration();

        if (services.Any(descriptor =>
            descriptor.ServiceType ==
            typeof(OperationsDatabasePersistenceRegistration)))
        {
            return services;
        }

        services.AddSingleton<OperationsDatabasePersistenceRegistration>();
        services.AddScoped<IComputerInventoryStore, ComputerInventoryStore>();
        services.AddScoped<IOperatingSystemInventoryStore, OperatingSystemInventoryStore>();
        services.AddScoped<IMemoryInventoryStore, MemoryInventoryStore>();
        services.AddScoped<IProcessorSnapshotStore, ProcessorSnapshotStore>();
        services.AddScoped<IDiskSnapshotStore, DiskSnapshotStore>();
        services.AddScoped<IVolumeSnapshotStore, VolumeSnapshotStore>();
        services.AddScoped<INetworkSnapshotStore, NetworkSnapshotStore>();
        services.AddScoped<ICoreWindowsInventoryStore, CoreWindowsInventoryStore>();
        services.AddSingleton<ICapabilityEngine, CapabilityEngine>();
        services.AddScoped<IWindowsCapabilityCoordinator, WindowsCapabilityCoordinator>();
        services.AddSingleton<ICollectorDecisionEngine, CollectorDecisionEngine>();
        services.AddScoped<ICollectorDecisionCoordinator, CollectorDecisionCoordinator>();
        services.AddSingleton<IExecutionPlanEngine, ExecutionPlanEngine>();
        services.AddScoped<IExecutionPlanCoordinator, ExecutionPlanCoordinator>();
        services.AddSingleton<IExecutionPolicyCatalog, ExecutionPolicyCatalog>();
        services.AddSingleton<IRuntimePluginCompatibilityMatrix,
            RuntimePluginCompatibilityMatrix>();
        services.AddSingleton<ICollectorPluginRegistry>(provider =>
            new CollectorPluginRegistry(Array.Empty<ICollectorPlugin>(),
                provider.GetRequiredService<IRuntimePluginCompatibilityMatrix>(),
                CollectorRuntimeVersions.RuntimeVersion));
        services.AddSingleton<IPluginPolicyCompatibilityValidator,
            PluginPolicyCompatibilityValidator>();
        services.AddSingleton<ExecutionMonitoringSubscriber>();
        services.AddSingleton<IExecutionMonitoring>(provider =>
            provider.GetRequiredService<ExecutionMonitoringSubscriber>());
        services.AddSingleton<IExecutionEventSubscriber, LoggingExecutionEventSubscriber>();
        services.AddSingleton<IExecutionEventSubscriber, ExecutionMonitoringEventSubscriber>();
        services.AddSingleton<IExecutionEventSink, CompositeExecutionEventSink>();
        services.AddScoped<IExecutionStateStore, ExecutionStateStore>();
        services.AddScoped<ICommittedExecutionPlanLoader, CommittedExecutionPlanLoader>();
        services.AddScoped<ICollectorRuntime, CollectorRuntime>();
        services.AddScoped<IExecutionDispatcher, ExecutionDispatcher>();
        services.AddScoped<ICollectorRuntimeOrchestrator, CollectorRuntimeOrchestrator>();
        services.AddScoped<IInventoryScheduleStore, InventoryScheduleStore>();
        services.AddDbContext<OperationsDbContext>((serviceProvider, options) =>
        {
            string connectionString = serviceProvider
                .GetRequiredService<IOperationsDatabaseConfiguration>()
                .GetConnectionString()!;

            options.UseSqlServer(
                connectionString,
                sqlServer => sqlServer.EnableRetryOnFailure(
                    MaximumRetryCount,
                    MaximumRetryDelay,
                    errorNumbersToAdd: null));
        });

        return services;
    }

    private sealed class OperationsDatabasePersistenceRegistration;
}
