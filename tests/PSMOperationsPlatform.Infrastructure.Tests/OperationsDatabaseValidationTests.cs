using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PSMOperationsPlatform.Infrastructure.Configuration;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class OperationsDatabaseValidationTests
{
    private const string ValidPrefix =
        "Server=validation-server;Database=validation-database;";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OperationsDatabaseValidation_RejectsMissingValues(
        string? connectionString)
    {
        ValidateOptionsResult result = Validate(connectionString);

        AssertFailedWith(
            result,
            OperationsDatabaseConfigurationFailures.Missing);
    }

    [Theory]
    [InlineData("Integrated Security=True")]
    [InlineData("Integrated Security=SSPI")]
    [InlineData("Trusted_Connection=True")]
    [InlineData("Trusted_Connection=Yes")]
    [InlineData("integrated security=true;Application Name=PSM")]
    public void IntegratedAuthentication_AcceptsSupportedSemanticForms(
        string authentication)
    {
        ValidateOptionsResult result = Validate(
            $"{ValidPrefix}{authentication}");

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("User ID=sa;Password=secret")]
    [InlineData("UID=sa;PWD=secret")]
    [InlineData("User ID=;Integrated Security=True")]
    [InlineData("Password=;Integrated Security=True")]
    [InlineData("Integrated Security=False;User ID=sa;Password=secret")]
    [InlineData("Trusted_Connection=False;UID=sa;PWD=secret")]
    [InlineData("User ID=sa")]
    [InlineData("Password=secret")]
    [InlineData("uId=sa;pWd=secret")]
    public void SqlAuthentication_RejectsCredentialKeys(
        string authentication)
    {
        ValidateOptionsResult result = Validate(
            $"{ValidPrefix}{authentication}");

        AssertFailedWith(
            result,
            OperationsDatabaseConfigurationFailures
                .SqlAuthenticationNotSupported);
    }

    [Theory]
    [InlineData("Integrated Security=False")]
    [InlineData("Trusted_Connection=False")]
    public void IntegratedAuthentication_RejectsDisabledIntegratedSecurity(
        string authentication)
    {
        ValidateOptionsResult result = Validate(
            $"{ValidPrefix}{authentication}");

        AssertFailedWith(
            result,
            OperationsDatabaseConfigurationFailures
                .IntegratedAuthenticationRequired);
    }

    [Theory]
    [InlineData("not-a-key-value")]
    [InlineData("Server=\"unterminated")]
    [InlineData("Server=validation-server;Database")]
    public void ConnectionStringValidation_RejectsMalformedValuesSafely(
        string connectionString)
    {
        ValidateOptionsResult result = Validate(connectionString);

        AssertFailedWith(
            result,
            OperationsDatabaseConfigurationFailures.Malformed);
        Assert.DoesNotContain(connectionString, SingleFailure(result));
    }

    [Theory]
    [InlineData("Encrypt=False")]
    [InlineData("Encrypt=True")]
    [InlineData("TrustServerCertificate=True")]
    [InlineData("TrustServerCertificate=False")]
    [InlineData("Encrypt=False;TrustServerCertificate=True")]
    public void ConnectionStringValidation_DoesNotImposeEncryptionPolicy(
        string transportSettings)
    {
        ValidateOptionsResult result = Validate(
            $"{ValidPrefix}Integrated Security=True;{transportSettings}");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ValidateOnStart_AllowsValidConfiguration()
    {
        using IHost host = CreateHost(
            $"{ValidPrefix}Integrated Security=True",
            registerCapability: true);

        await host.StartAsync();
        await host.StopAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Server=\"unterminated")]
    [InlineData(
        "Server=validation-server;Database=validation-database;User ID=sa;Password=secret")]
    public async Task ValidateOnStart_RejectsInvalidConfiguration(
        string? connectionString)
    {
        using IHost host = CreateHost(
            connectionString,
            registerCapability: true);

        await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());
    }

    [Fact]
    public async Task ValidateOnStart_DoesNotAffectHostWithoutCapability()
    {
        using IHost host = CreateHost(
            connectionString: null,
            registerCapability: false);

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task AddOperationsDatabaseConfiguration_IsIdempotent()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(
            ConnectionValues($"{ValidPrefix}Integrated Security=True"));

        builder.Services.AddOperationsDatabaseConfiguration();
        builder.Services.AddOperationsDatabaseConfiguration();

        Assert.Single(
            builder.Services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(IValidateOptions<OperationsDatabaseValidationMarker>));

        using IHost host = builder.Build();
        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task ConfigurationRedaction_FailuresContainOnlyStableCode()
    {
        const string connectionString =
            "Server=SENTINEL-SERVER;Database=SENTINEL-DATABASE;" +
            "User ID=SENTINEL-USER;Password=SENTINEL-PASSWORD";

        using IHost host = CreateHost(
            connectionString,
            registerCapability: true);

        OptionsValidationException exception =
            await Assert.ThrowsAsync<OptionsValidationException>(
                () => host.StartAsync());

        string failure = Assert.Single(exception.Failures);

        Assert.Equal(
            OperationsDatabaseConfigurationFailures
                .SqlAuthenticationNotSupported,
            failure);
        Assert.DoesNotContain(connectionString, exception.ToString());
        Assert.DoesNotContain("SENTINEL-SERVER", exception.ToString());
        Assert.DoesNotContain("SENTINEL-DATABASE", exception.ToString());
        Assert.DoesNotContain("SENTINEL-USER", exception.ToString());
        Assert.DoesNotContain("SENTINEL-PASSWORD", exception.ToString());
        Assert.Null(exception.InnerException);
    }

    private static ValidateOptionsResult Validate(string? connectionString)
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(
            ConnectionValues(connectionString));

        var accessor = new OperationsDatabaseConfiguration(configuration);
        var validator = new OperationsDatabaseConfigurationValidator(accessor);

        return validator.Validate(
            Options.DefaultName,
            new OperationsDatabaseValidationMarker());
    }

    private static IHost CreateHost(
        string? connectionString,
        bool registerCapability)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(
            ConnectionValues(connectionString));

        if (registerCapability)
        {
            builder.Services.AddOperationsDatabaseConfiguration();
        }

        return builder.Build();
    }

    private static Dictionary<string, string?> ConnectionValues(
        string? connectionString) =>
        new()
        {
            ["ConnectionStrings:OperationsDatabase"] = connectionString,
        };

    private static void AssertFailedWith(
        ValidateOptionsResult result,
        string expectedFailure)
    {
        Assert.True(result.Failed);
        Assert.Equal(expectedFailure, SingleFailure(result));
    }

    private static string SingleFailure(ValidateOptionsResult result) =>
        Assert.Single(result.Failures!);
}
