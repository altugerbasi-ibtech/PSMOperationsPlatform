using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace PSMOperationsPlatform.Infrastructure.Configuration;

public static class OperationsDatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddOperationsDatabaseConfiguration(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<
            IOperationsDatabaseConfiguration,
            OperationsDatabaseConfiguration>();

        if (services.Any(descriptor =>
            descriptor.ServiceType ==
            typeof(OperationsDatabaseValidationRegistration)))
        {
            return services;
        }

        services.AddSingleton<OperationsDatabaseValidationRegistration>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<OperationsDatabaseValidationMarker>,
                OperationsDatabaseConfigurationValidator>());
        services
            .AddOptions<OperationsDatabaseValidationMarker>()
            .ValidateOnStart();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IHostedService,
                OperationsDatabaseStartupDiagnostics>());

        return services;
    }
}
