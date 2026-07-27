using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PSMOperationsPlatform.Infrastructure.Configuration;

public static class PsmConfigurationExtensions
{
    public const string EnvironmentVariablePrefix = "PSM__";

    public static ConfigurationManager ConfigurePsmConfiguration(
        this ConfigurationManager configuration,
        string environmentName,
        string[] args,
        Assembly userSecretsAssembly)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(userSecretsAssembly);

        return configuration.ConfigurePsmConfiguration(
            environmentName,
            args,
            builder => builder.AddUserSecrets(
                userSecretsAssembly,
                optional: true,
                reloadOnChange: false));
    }

    internal static ConfigurationManager ConfigurePsmConfiguration(
        this ConfigurationManager configuration,
        string environmentName,
        string[] args,
        Action<IConfigurationBuilder> addDevelopmentUserSecrets)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(addDevelopmentUserSecrets);

        configuration.Sources.Clear();
        configuration
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile(
                $"appsettings.{environmentName}.json",
                optional: true,
                reloadOnChange: false);

        if (string.Equals(
            environmentName,
            Environments.Development,
            StringComparison.OrdinalIgnoreCase))
        {
            addDevelopmentUserSecrets(configuration);
        }

        configuration
            .AddEnvironmentVariables(EnvironmentVariablePrefix)
            .AddCommandLine(args);

        return configuration;
    }
}
