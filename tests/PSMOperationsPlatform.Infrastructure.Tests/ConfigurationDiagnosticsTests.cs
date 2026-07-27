using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PSMOperationsPlatform.Infrastructure.Configuration;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class ConfigurationDiagnosticsTests
{
    private const string ValidConnectionString =
        "Server=server-sensitive-value;" +
        "Database=database-sensitive-value;" +
        "Integrated Security=True";

    [Fact]
    public async Task StartupDiagnostics_LogsAllowlistedSummaryOnce()
    {
        var logs = new CapturingLoggerProvider();
        using IHost host = CreateHost(
            ValidConnectionString,
            registerCapability: true,
            registrationCount: 1,
            logs);

        await host.StartAsync();
        await host.StopAsync();

        CapturedLog entry = Assert.Single(SuccessEntries(logs));
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(
            OperationsDatabaseConfigurationLogEvents.ConfigurationValidatedId,
            entry.EventId.Id);
        Assert.Equal(
            OperationsDatabaseConfigurationLogEvents.ConfigurationValidatedName,
            entry.EventId.Name);
        Assert.Equal("DiagnosticsTest", entry.State["EnvironmentName"]);
        Assert.Equal(true, entry.State["IsConfigured"]);
        Assert.Equal("Integrated", entry.State["AuthenticationMode"]);
        Assert.Equal(
            true,
            entry.State["ConfigurationValidationSucceeded"]);
        Assert.Equal(
            [
                "AuthenticationMode",
                "ConfigurationValidationSucceeded",
                "EnvironmentName",
                "IsConfigured",
                "{OriginalFormat}",
            ],
            entry.State.Keys.Order(StringComparer.Ordinal));
        Assert.Null(entry.Exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Server=\"unterminated")]
    [InlineData(
        "Server=server-sensitive-value;Database=database-sensitive-value;User ID=sql-user-sensitive-value;Password=password-sensitive-value")]
    public async Task StartupDiagnostics_DoesNotLogSuccessForInvalidConfiguration(
        string? connectionString)
    {
        var logs = new CapturingLoggerProvider();
        using IHost host = CreateHost(
            connectionString,
            registerCapability: true,
            registrationCount: 1,
            logs);

        await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());

        Assert.Empty(SuccessEntries(logs));
    }

    [Fact]
    public async Task ConfigurationRedaction_ExcludesSensitiveSentinels()
    {
        var logs = new CapturingLoggerProvider();
        using IHost host = CreateHost(
            ValidConnectionString,
            registerCapability: true,
            registrationCount: 1,
            logs,
            "env-secret-sensitive-value");

        await host.StartAsync();
        await host.StopAsync();

        CapturedLog entry = Assert.Single(SuccessEntries(logs));
        string captured = entry.AllCapturedText();

        Assert.DoesNotContain("server-sensitive-value", captured);
        Assert.DoesNotContain("database-sensitive-value", captured);
        Assert.DoesNotContain("sql-user-sensitive-value", captured);
        Assert.DoesNotContain("password-sensitive-value", captured);
        Assert.DoesNotContain("env-secret-sensitive-value", captured);
        Assert.DoesNotContain(ValidConnectionString, captured);
    }

    [Fact]
    public async Task ConfigurationRedaction_InvalidStartupExcludesCredentialSentinels()
    {
        const string sqlConnectionString =
            "Server=server-sensitive-value;" +
            "Database=database-sensitive-value;" +
            "User ID=sql-user-sensitive-value;" +
            "Password=password-sensitive-value";
        var logs = new CapturingLoggerProvider();
        using IHost host = CreateHost(
            sqlConnectionString,
            registerCapability: true,
            registrationCount: 1,
            logs,
            "env-secret-sensitive-value");

        await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());

        string captured = string.Join(
            Environment.NewLine,
            logs.Entries.Select(entry => entry.AllCapturedText()));

        Assert.DoesNotContain(sqlConnectionString, captured);
        Assert.DoesNotContain("server-sensitive-value", captured);
        Assert.DoesNotContain("database-sensitive-value", captured);
        Assert.DoesNotContain("sql-user-sensitive-value", captured);
        Assert.DoesNotContain("password-sensitive-value", captured);
        Assert.DoesNotContain("env-secret-sensitive-value", captured);
        Assert.Empty(SuccessEntries(logs));
    }

    [Fact]
    public async Task DiagnosticsIdempotency_DuplicateRegistrationLogsOnce()
    {
        var logs = new CapturingLoggerProvider();
        using IHost host = CreateHost(
            ValidConnectionString,
            registerCapability: true,
            registrationCount: 2,
            logs);

        await host.StartAsync();
        await host.StopAsync();

        Assert.Single(SuccessEntries(logs));
    }

    [Fact]
    public async Task DiagnosticsIdempotency_LogsOncePerHostInstance()
    {
        var firstLogs = new CapturingLoggerProvider();
        var secondLogs = new CapturingLoggerProvider();
        using IHost firstHost = CreateHost(
            ValidConnectionString,
            registerCapability: true,
            registrationCount: 1,
            firstLogs);
        using IHost secondHost = CreateHost(
            ValidConnectionString,
            registerCapability: true,
            registrationCount: 1,
            secondLogs);

        await firstHost.StartAsync();
        await firstHost.StopAsync();
        await secondHost.StartAsync();
        await secondHost.StopAsync();

        Assert.Single(SuccessEntries(firstLogs));
        Assert.Single(SuccessEntries(secondLogs));
    }

    [Fact]
    public void DiagnosticsIdempotency_ServiceResolutionDoesNotLog()
    {
        var logs = new CapturingLoggerProvider();
        HostApplicationBuilder builder = CreateBuilder(
            ValidConnectionString,
            logs);
        builder.Services.AddOperationsDatabaseConfiguration();

        using IHost host = builder.Build();
        _ = host.Services.GetServices<IHostedService>().ToArray();

        Assert.Empty(SuccessEntries(logs));
    }

    [Fact]
    public async Task DiagnosticsScope_HostWithoutCapabilityIsUnaffected()
    {
        var logs = new CapturingLoggerProvider();
        using IHost host = CreateHost(
            connectionString: null,
            registerCapability: false,
            registrationCount: 0,
            logs);

        await host.StartAsync();
        await host.StopAsync();

        Assert.Empty(SuccessEntries(logs));
    }

    [Fact]
    public void ConfigurationEventId_IsStableAndDoesNotOverlapPersistenceRange()
    {
        Assert.Equal(
            2200,
            OperationsDatabaseConfigurationLogEvents.ConfigurationValidatedId);
        Assert.Equal(
            "OperationsDatabaseConfigurationValidated",
            OperationsDatabaseConfigurationLogEvents
                .ConfigurationValidatedName);
        Assert.DoesNotContain(
            OperationsDatabaseConfigurationLogEvents.ConfigurationValidatedId,
            Enumerable.Range(2100, 8));
    }

    private static IHost CreateHost(
        string? connectionString,
        bool registerCapability,
        int registrationCount,
        CapturingLoggerProvider logs,
        string? unrelatedSecret = null)
    {
        HostApplicationBuilder builder = CreateBuilder(
            connectionString,
            logs,
            unrelatedSecret);

        if (registerCapability)
        {
            for (int index = 0; index < registrationCount; index++)
            {
                builder.Services.AddOperationsDatabaseConfiguration();
            }
        }

        return builder.Build();
    }

    private static HostApplicationBuilder CreateBuilder(
        string? connectionString,
        CapturingLoggerProvider logs,
        string? unrelatedSecret = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = "DiagnosticsTest",
            });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:OperationsDatabase"] = connectionString,
                ["Unrelated:Secret"] = unrelatedSecret,
            });

        return builder;
    }

    private static CapturedLog[] SuccessEntries(
        CapturingLoggerProvider logs) =>
        logs.Entries
            .Where(entry =>
                entry.EventId.Id ==
                OperationsDatabaseConfigurationLogEvents
                    .ConfigurationValidatedId)
            .ToArray();

    private sealed class CapturingLoggerProvider :
        ILoggerProvider,
        ISupportExternalScope
    {
        private readonly ConcurrentQueue<CapturedLog> entries = new();
        private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();

        internal IReadOnlyCollection<CapturedLog> Entries =>
            entries.ToArray();

        public ILogger CreateLogger(string categoryName) =>
            new CapturingLogger(entries, () => scopes);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
            scopes = scopeProvider;

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(
        ConcurrentQueue<CapturedLog> entries,
        Func<IExternalScopeProvider> scopes)
        : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            scopes().Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var structuredState =
                state as IEnumerable<KeyValuePair<string, object?>>;
            var properties = structuredState?.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal) ??
                new Dictionary<string, object?>(StringComparer.Ordinal);
            var capturedScopes = new List<string>();
            scopes().ForEachScope(
                (scope, target) =>
                    target.Add(scope?.ToString() ?? string.Empty),
                capturedScopes);

            entries.Enqueue(
                new CapturedLog(
                    logLevel,
                    eventId,
                    properties,
                    formatter(state, exception),
                    exception,
                    capturedScopes));
        }
    }

    private sealed record CapturedLog(
        LogLevel Level,
        EventId EventId,
        IReadOnlyDictionary<string, object?> State,
        string Message,
        Exception? Exception,
        IReadOnlyList<string> Scopes)
    {
        internal string AllCapturedText() =>
            string.Join(
                Environment.NewLine,
                State
                    .Select(pair => $"{pair.Key}={pair.Value}")
                    .Append(Message)
                    .Append(Exception?.ToString() ?? string.Empty)
                    .Concat(Scopes));
    }
}
