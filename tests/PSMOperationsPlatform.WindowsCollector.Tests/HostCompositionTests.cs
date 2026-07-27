using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PSMOperationsPlatform.Infrastructure.Configuration;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class HostCompositionTests
{
    private const string ValidConnectionString =
        "Server=collector-test;Database=collector-test;" +
        "Integrated Security=True";

    [Theory]
    [InlineData("Integrated Security=True")]
    [InlineData("Integrated Security=SSPI")]
    [InlineData("Trusted_Connection=True")]
    public async Task HostStartsWithIntegratedAuthentication(
        string authentication)
    {
        using IHost host = CreateHost(
            $"Server=collector-test;Database=collector-test;{authentication}");

        await host.StartAsync();
        await host.StopAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-key-value")]
    [InlineData(
        "Server=collector-test;Database=collector-test;User ID=user;Password=secret")]
    public async Task HostRejectsInvalidOperationsDatabaseConfiguration(
        string? connectionString)
    {
        using IHost host = CreateHost(connectionString);

        await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());
    }

    [Fact]
    public void HostSelectsOperationsDatabaseAndScopedPersistence()
    {
        HostApplicationBuilder builder = CreateBuilder(ValidConnectionString);

        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(IOperationsDatabaseConfiguration));
        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType == typeof(OperationsDbContext) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType == typeof(IWindowsTargetProvider) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType ==
                    typeof(IManagedServerConnectivityStore) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            builder.Services,
            descriptor =>
                descriptor.ServiceType ==
                    typeof(IConnectivityResultPersistence) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);

        using IHost host = builder.Build();
        using IServiceScope scope = host.Services.CreateScope();
        OperationsDbContext context =
            scope.ServiceProvider.GetRequiredService<OperationsDbContext>();

        Assert.Equal(
            "Microsoft.EntityFrameworkCore.SqlServer",
            context.Database.ProviderName);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:00:01")]
    public async Task HostRejectsNonPositivePollingInterval(string interval)
    {
        HostApplicationBuilder builder = CreateBuilder(ValidConnectionString);
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["WindowsCollector:PollingInterval"] = interval,
            });
        using IHost host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());
    }

    [Fact]
    public void PersistenceRegistrationIsIdempotent()
    {
        HostApplicationBuilder builder = CreateBuilder(ValidConnectionString);

        builder.Services.AddOperationsDatabasePersistence();
        builder.Services.AddOperationsDatabasePersistence();

        Assert.Single(
            builder.Services,
            descriptor => descriptor.ServiceType == typeof(OperationsDbContext));
    }

    [Fact]
    public void HostUsesApprovedProviderComposition()
    {
        HostApplicationBuilder builder =
            WindowsCollectorHost.CreateApplicationBuilder(
                ["--CompositionProof:Value=command-line"]);

        Assert.Equal(
            "command-line",
            builder.Configuration["CompositionProof:Value"]);
        Assert.Contains(
            builder.Configuration.Sources,
            source => source.GetType().Name.Contains(
                "EnvironmentVariables",
                StringComparison.Ordinal));
    }

    [Fact]
    public void PollingIntervalDefaultsToSixtySeconds()
    {
        HostApplicationBuilder builder = CreateBuilder(ValidConnectionString);
        using IHost host = builder.Build();

        var options = host.Services
            .GetRequiredService<IOptions<WindowsCollectorOptions>>();

        Assert.Equal(TimeSpan.FromSeconds(60), options.Value.PollingInterval);
    }

    private static IHost CreateHost(string? connectionString) =>
        CreateHostWithTargetProvider(CreateBuilder(connectionString));

    private static IHost CreateHostWithTargetProvider(
        HostApplicationBuilder builder)
    {
        builder.Services.RemoveAll<IWindowsTargetProvider>();
        builder.Services.AddScoped<IWindowsTargetProvider, EmptyTargetProvider>();
        return builder.Build();
    }

    private static HostApplicationBuilder CreateBuilder(
        string? connectionString)
    {
        HostApplicationBuilder builder =
            WindowsCollectorHost.CreateApplicationBuilder([]);
        builder.Logging.ClearProviders();
        builder.Environment.EnvironmentName = Environments.Production;
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:OperationsDatabase"] = connectionString,
            });

        return builder;
    }

    private sealed class EmptyTargetProvider : IWindowsTargetProvider
    {
        public Task<IReadOnlyList<WindowsTarget>> LoadEligibleAsync(
            DateTime currentTime,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WindowsTarget>>([]);
    }
}
