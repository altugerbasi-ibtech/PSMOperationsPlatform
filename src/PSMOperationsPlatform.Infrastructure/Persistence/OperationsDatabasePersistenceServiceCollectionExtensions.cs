using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
