using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PSMOperationsPlatform.Infrastructure.Configuration;

namespace PSMOperationsPlatform.Infrastructure.Tests;

[Collection(nameof(ConfigurationEnvironmentCollection))]
public sealed class ConfigurationCompositionTests
{
    private const string EnvironmentVariableName =
        "PSM__CompositionTests__Value";

    [Fact]
    public void ConfigurePsmConfiguration_AppliesProvidersInApprovedOrder()
    {
        using var files = ConfigurationFiles.Create(
            baseValue: "base-json",
            environmentValue: "environment-json");

        string? originalValue = Environment.GetEnvironmentVariable(
            EnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(
                EnvironmentVariableName,
                "environment-variable");

            var configuration = new ConfigurationManager();
            configuration.SetBasePath(files.DirectoryPath);

            configuration.ConfigurePsmConfiguration(
                Environments.Development,
                ["--CompositionTests:Value=command-line"],
                builder => builder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["CompositionTests:Value"] = "user-secrets",
                    }));

            Assert.Equal(
                "command-line",
                configuration["CompositionTests:Value"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                EnvironmentVariableName,
                originalValue);
        }
    }

    [Fact]
    public void ConfigurePsmConfiguration_AddsEachApprovedProviderOnce()
    {
        var configuration = new ConfigurationManager();

        configuration.ConfigurePsmConfiguration(
            Environments.Development,
            ["--CompositionTests:Value=command-line"],
            builder => builder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["CompositionTests:Value"] = "user-secrets",
                }));

        Assert.Equal(5, configuration.Sources.Count);
    }

    [Fact]
    public void ConfigurePsmConfiguration_UsesEachProviderAtItsPrecedenceLevel()
    {
        using var files = ConfigurationFiles.Create(
            baseValue: "base-json",
            environmentValue: "environment-json");

        var configuration = new ConfigurationManager();
        configuration.SetBasePath(files.DirectoryPath);

        configuration.ConfigurePsmConfiguration(
            Environments.Development,
            [],
            builder => builder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["CompositionTests:Value"] = "user-secrets",
                }));

        Assert.Equal("user-secrets", configuration["CompositionTests:Value"]);
    }

    [Fact]
    public void ConfigurePsmConfiguration_EnvironmentJsonOverridesBaseJson()
    {
        using var files = ConfigurationFiles.Create(
            baseValue: "base-json",
            environmentValue: "environment-json");

        var configuration = new ConfigurationManager();
        configuration.SetBasePath(files.DirectoryPath);

        configuration.ConfigurePsmConfiguration(
            Environments.Development,
            [],
            _ => { });

        Assert.Equal(
            "environment-json",
            configuration["CompositionTests:Value"]);
    }

    [Fact]
    public void ConfigurePsmConfiguration_ReadsBaseJsonWhenNoOverrideExists()
    {
        using var files = ConfigurationFiles.Create(
            baseValue: "base-json",
            environmentValue: "environment-json");

        var configuration = new ConfigurationManager();
        configuration.SetBasePath(files.DirectoryPath);

        configuration.ConfigurePsmConfiguration(
            Environments.Staging,
            [],
            _ => throw new InvalidOperationException(
                "Staging must not add User Secrets."));

        Assert.Equal("base-json", configuration["CompositionTests:Value"]);
    }

    [Fact]
    public void ConfigurePsmConfiguration_EnvironmentVariableOverridesUserSecrets()
    {
        using var files = ConfigurationFiles.Create(
            baseValue: "base-json",
            environmentValue: "environment-json");

        string? originalValue = Environment.GetEnvironmentVariable(
            EnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(
                EnvironmentVariableName,
                "environment-variable");

            var configuration = new ConfigurationManager();
            configuration.SetBasePath(files.DirectoryPath);
            configuration.ConfigurePsmConfiguration(
                Environments.Development,
                [],
                builder => builder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["CompositionTests:Value"] = "user-secrets",
                    }));

            Assert.Equal(
                "environment-variable",
                configuration["CompositionTests:Value"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                EnvironmentVariableName,
                originalValue);
        }
    }

    [Fact]
    public void ConfigurePsmConfiguration_PrefixedEnvironmentVariableMapsToSection()
    {
        const string variableName = "PSM__SomeSection__SomeValue";
        string? originalValue = Environment.GetEnvironmentVariable(variableName);

        try
        {
            Environment.SetEnvironmentVariable(variableName, "mapped-value");

            var configuration = new ConfigurationManager();
            configuration.ConfigurePsmConfiguration(
                Environments.Production,
                [],
                _ => throw new InvalidOperationException(
                    "Production must not add User Secrets."));

            Assert.Equal("mapped-value", configuration["SomeSection:SomeValue"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }

    [Fact]
    public void ConfigurePsmConfiguration_PrefixedOperationsDatabaseMapsToNamedConnection()
    {
        const string variableName =
            "PSM__ConnectionStrings__OperationsDatabase";
        string? originalValue = Environment.GetEnvironmentVariable(variableName);

        try
        {
            Environment.SetEnvironmentVariable(variableName, "mapped-value");

            var configuration = new ConfigurationManager();
            configuration.ConfigurePsmConfiguration(
                Environments.Production,
                [],
                _ => throw new InvalidOperationException(
                    "Production must not add User Secrets."));

            Assert.Equal(
                "mapped-value",
                configuration.GetConnectionString("OperationsDatabase"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }

    [Fact]
    public void ConfigurePsmConfiguration_DoesNotAddUserSecretsOutsideDevelopment()
    {
        bool userSecretsRequested = false;
        var configuration = new ConfigurationManager();

        configuration.ConfigurePsmConfiguration(
            Environments.Production,
            [],
            _ => userSecretsRequested = true);

        Assert.False(userSecretsRequested);
    }

    [Fact]
    public void GetConnectionString_ReturnsOperationsDatabaseValue()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:OperationsDatabase"] = "test-value",
            });

        Assert.Equal(
            "test-value",
            configuration.GetConnectionString("OperationsDatabase"));
    }

    [Fact]
    public void AddOperationsDatabaseConfiguration_RegistersNamedConnectionAccessor()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:OperationsDatabase"] = "test-value",
            });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOperationsDatabaseConfiguration();

        using ServiceProvider provider = services.BuildServiceProvider();
        IOperationsDatabaseConfiguration accessor =
            provider.GetRequiredService<IOperationsDatabaseConfiguration>();

        Assert.Equal("test-value", accessor.GetConnectionString());
    }

    private sealed class ConfigurationFiles : IDisposable
    {
        private ConfigurationFiles(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public string DirectoryPath { get; }

        public static ConfigurationFiles Create(
            string baseValue,
            string environmentValue)
        {
            string directoryPath = Path.Combine(
                Path.GetTempPath(),
                $"PSMOperationsPlatform-{Guid.NewGuid():N}");

            Directory.CreateDirectory(directoryPath);
            File.WriteAllText(
                Path.Combine(directoryPath, "appsettings.json"),
                $$"""
                {
                  "CompositionTests": {
                    "Value": "{{baseValue}}"
                  }
                }
                """);
            File.WriteAllText(
                Path.Combine(
                    directoryPath,
                    $"appsettings.{Environments.Development}.json"),
                $$"""
                {
                  "CompositionTests": {
                    "Value": "{{environmentValue}}"
                  }
                }
                """);

            return new ConfigurationFiles(directoryPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }
}

[CollectionDefinition(
    nameof(ConfigurationEnvironmentCollection),
    DisableParallelization = true)]
public sealed class ConfigurationEnvironmentCollection;
