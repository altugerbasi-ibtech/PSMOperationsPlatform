using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using PSMOperationsPlatform.Domain.Entities;
using PSMOperationsPlatform.Domain.Enums;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.WindowsCollector.Tests;

public sealed class WindowsTargetProviderTests
{
    private static readonly DateTime CurrentTime =
        new(2026, 7, 27, 14, 30, 0);

    [Fact]
    public async Task LoadsOnlyEnabledTargetsThatAreDueOrHaveNoEligibilityTime()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        ManagedServer nullDue = CreateServer("null.ae.local");
        ManagedServer pastDue = CreateServer("past.ae.local");
        ManagedServer equalDue = CreateServer("equal.ae.local");
        ManagedServer customPolicy = new(
            Guid.NewGuid(),
            "custom.ae.local",
            CurrentTime.AddDays(-1),
            winRmTransportMode: WinRmTransportMode.HttpOnly,
            winRmHttpsPort: 15986,
            winRmHttpPort: 15985,
            winRmProbeTimeoutSeconds: 25);
        ManagedServer futureDue = CreateServer("future.ae.local");
        ManagedServer disabledNull = CreateServer("disabled-null.ae.local", false);
        ManagedServer disabledPast = CreateServer("disabled-past.ae.local", false);
        database.Context.AddRange(
            nullDue,
            pastDue,
            equalDue,
            customPolicy,
            futureDue,
            disabledNull,
            disabledPast);
        SetNextAttempt(database.Context, pastDue, CurrentTime.AddSeconds(-1));
        SetNextAttempt(database.Context, equalDue, CurrentTime);
        SetNextAttempt(database.Context, futureDue, CurrentTime.AddSeconds(1));
        futureDue.ApplyInventorySuccess(
            CurrentTime.AddHours(-1),
            CurrentTime.AddSeconds(1));
        SetNextAttempt(database.Context, disabledPast, CurrentTime.AddSeconds(-1));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var provider = new WindowsTargetProvider(database.Context);

        IReadOnlyList<WindowsTarget> targets =
            await provider.LoadEligibleAsync(CurrentTime, CancellationToken.None);

        Assert.Equal(
            new[] { nullDue.Id, pastDue.Id, equalDue.Id, customPolicy.Id }.Order(),
            targets.Select(target => target.TargetId).Order());
        WindowsTarget projectedPolicy = Assert.Single(
            targets,
            target => target.TargetId == customPolicy.Id);
        Assert.Equal(WinRmTransportMode.HttpOnly, projectedPolicy.TransportMode);
        Assert.Equal(15986, projectedPolicy.HttpsPort);
        Assert.Equal(15985, projectedPolicy.HttpPort);
        Assert.Equal(TimeSpan.FromSeconds(25), projectedPolicy.ProbeTimeout);
        Assert.Empty(database.Context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task EmptyResultIsSuccessful()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        database.Context.Add(CreateServer("disabled.ae.local", false));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();
        var provider = new WindowsTargetProvider(database.Context);

        IReadOnlyList<WindowsTarget> targets =
            await provider.LoadEligibleAsync(CurrentTime, CancellationToken.None);

        Assert.Empty(targets);
    }

    [Fact]
    public async Task MaterializationHonorsCancellation()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        var provider = new WindowsTargetProvider(database.Context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.LoadEligibleAsync(CurrentTime, cancellation.Token));
    }

    [Fact]
    public void ProjectionIsImmutableAndContainsOnlyProbeIdentity()
    {
        string[] properties = typeof(WindowsTarget)
            .GetProperties()
            .Select(property => property.Name)
            .Order()
            .ToArray();

        Assert.Equal(
            new[]
            {
                "HostName",
                "HttpPort",
                "HttpsPort",
                "IsInventoryDue",
                "ProbeTimeout",
                "RowVersion",
                "TargetId",
                "TransportMode",
            },
            properties);
        Assert.All(
            typeof(WindowsTarget).GetProperties(),
            property => Assert.Contains(
                typeof(System.Runtime.CompilerServices.IsExternalInit),
                property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()));
    }

    private static ManagedServer CreateServer(
        string fqdn,
        bool isEnabled = true) =>
        new(Guid.NewGuid(), fqdn, CurrentTime.AddDays(-1), isEnabled: isEnabled);

    private static void SetNextAttempt(
        OperationsDbContext context,
        ManagedServer server,
        DateTime value) =>
        context.Entry(server)
            .Property(entity => entity.NextConnectivityAttemptAt)
            .CurrentValue = value;

    private sealed class TestDatabase(
        SqliteConnection connection,
        OperationsDbContext context) : IAsyncDisposable
    {
        public OperationsDbContext Context { get; } = context;

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateFunction(
                "ISJSON",
                (string? value) =>
                {
                    if (value is null)
                    {
                        return 0;
                    }

                    try
                    {
                        using JsonDocument _ = JsonDocument.Parse(value);
                        return 1;
                    }
                    catch (JsonException)
                    {
                        return 0;
                    }
                });
            var options = new DbContextOptionsBuilder<OperationsDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new SqliteOperationsDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class SqliteOperationsDbContext(
        DbContextOptions<OperationsDbContext> options)
        : OperationsDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(entityType => entityType.GetProperties()))
            {
                if (property.GetColumnType() == "nvarchar(max)")
                {
                    property.SetColumnType("TEXT");
                }
            }

            modelBuilder.Entity<ManagedServer>()
                .Property(entity => entity.RowVersion)
                .HasDefaultValueSql("randomblob(8)");
        }
    }
}
