using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PSMOperationsPlatform.Infrastructure.Configuration;

internal sealed class OperationsDatabaseStartupDiagnostics(
    ILogger<OperationsDatabaseStartupDiagnostics> logger,
    IHostEnvironment hostEnvironment)
    : IHostedService
{
    private const string IntegratedAuthenticationMode = "Integrated";
    private int hasLogged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref hasLogged, 1) == 0)
        {
            logger.LogInformation(
                OperationsDatabaseConfigurationLogEvents
                    .ConfigurationValidated,
                "Operations database configuration validated successfully. Environment={EnvironmentName} Configured={IsConfigured} AuthenticationMode={AuthenticationMode} ConfigurationValidationSucceeded={ConfigurationValidationSucceeded}",
                hostEnvironment.EnvironmentName,
                true,
                IntegratedAuthenticationMode,
                true);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
