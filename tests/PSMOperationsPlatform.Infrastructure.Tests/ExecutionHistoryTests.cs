using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PSMOperationsPlatform.Application.Runtime;
using PSMOperationsPlatform.Infrastructure.Persistence;

namespace PSMOperationsPlatform.Infrastructure.Tests;

public sealed class ExecutionHistoryTests
{
    [Fact]
    public void Schema_page_and_retention_contracts_are_explicit_and_bounded()
    {
        Assert.Equal(1, ExecutionHistorySchemaVersion.Value);
        Assert.NotEqual(ExecutionEventSchemaVersion.Value + 1,
            ExecutionHistorySchemaVersion.Value);
        Assert.Equal(50, ExecutionHistoryPageRequest.DefaultPageSize);
        Assert.Equal(200, ExecutionHistoryPageRequest.MaximumPageSize);
        Assert.Throws<ArgumentException>(() =>
            new ExecutionHistoryPageRequest(1, 201).Validate());
        ExecutionHistoryRetentionPolicy policy =
            ExecutionHistoryRetentionPolicy.Version1.Validate();
        Assert.Equal((180, 90, 90, 500),
            (policy.RunDays, policy.TransitionDays,
                policy.FailedProjectionDays, policy.BatchSize));
    }

    [Fact]
    public async Task Complete_projection_is_atomic_queryable_and_idempotent()
    {
        await using Fixture fixture = await Fixture.Create();
        ExecutionHistoryProjection value = Projection();
        var writer = new ExecutionHistoryWriter(fixture.Context);

        ExecutionHistoryWriteResult first =
            await writer.WriteAsync(value, CancellationToken.None);
        ExecutionHistoryWriteResult duplicate =
            await writer.WriteAsync(value, CancellationToken.None);

        Assert.Equal(ExecutionHistoryWriteDisposition.Created, first.Disposition);
        Assert.Equal(ExecutionHistoryWriteDisposition.Duplicate, duplicate.Disposition);
        Assert.Single(await fixture.Context.ExecutionRunHistory.ToArrayAsync());
        Assert.Single(await fixture.Context.ExecutionStepHistory.ToArrayAsync());
        Assert.Single(await fixture.Context.ExecutionAttemptHistory.ToArrayAsync());
        Assert.Equal(2, await fixture.Context.ExecutionStateTransitionHistory.CountAsync());
        Assert.Single(await fixture.Context.ExecutionArtifactHistory.ToArrayAsync());
        Assert.Single(await fixture.Context.ExecutionPolicyHistory.ToArrayAsync());

        var query = new ExecutionHistoryQueryService(fixture.Context);
        ExecutionRunHistoryItem? run =
            await query.GetRunAsync(value.Run.ExecutionRunId, CancellationToken.None);
        Assert.NotNull(run);
        Assert.Equal("sample.strategy", run.StrategyCode);
        Assert.Equal("sample.plugin", run.PluginId);
        Assert.Equal("1.0", run.TargetSdkVersion);
        Assert.Equal(ExecutionHistoryProjectionStatus.Completed, run.ProjectionStatus);
        Assert.Single(await query.GetStepsAsync(run.ExecutionRunId, CancellationToken.None));
        Assert.Single(await query.GetAttemptsAsync(
            run.ExecutionRunId, value.Steps[0].ExecutionStepId, CancellationToken.None));
        Assert.Equal(2, (await query.GetTransitionsAsync(
            run.ExecutionRunId, CancellationToken.None)).Count);
        Assert.Single(await query.GetArtifactsAsync(
            run.ExecutionRunId, CancellationToken.None));
        Assert.Single(await query.GetPoliciesAsync(
            run.ExecutionRunId, CancellationToken.None));
    }

    [Fact]
    public async Task Queries_are_filtered_paged_no_tracking_and_deterministic()
    {
        await using Fixture fixture = await Fixture.Create();
        var writer = new ExecutionHistoryWriter(fixture.Context);
        ExecutionHistoryProjection older = Projection(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            new DateTime(2026, 1, 1, 1, 0, 0));
        ExecutionHistoryProjection newer = Projection(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            new DateTime(2026, 1, 2, 1, 0, 0));
        await writer.WriteAsync(older, CancellationToken.None);
        await writer.WriteAsync(newer, CancellationToken.None);
        fixture.Context.ChangeTracker.Clear();
        var service = new ExecutionHistoryQueryService(fixture.Context);
        var request = new ExecutionHistoryQuery(new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 3), newer.Run.ManagedServerId, "sample.strategy",
            "sample.plugin", "Completed", "None", "RunCompleted",
            new ExecutionHistoryPageRequest(1, 1));

        ExecutionHistoryPageResult<ExecutionRunHistoryItem> page =
            await service.ListRunsAsync(request, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal(newer.Run.ExecutionRunId, page.Items[0].ExecutionRunId);
        Assert.True(page.HasNextPage);
        Assert.Equal(2, page.TotalCount);
        Assert.Empty(fixture.Context.ChangeTracker.Entries());
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListRunsAsync(
            request with { CompletedFrom = new DateTime(2027, 1, 1) },
            CancellationToken.None));
    }

    [Fact]
    public async Task Retention_cutoffs_use_TimeProvider_and_cleanup_is_bounded_idempotent()
    {
        await using Fixture fixture = await Fixture.Create();
        var writer = new ExecutionHistoryWriter(fixture.Context);
        await writer.WriteAsync(Projection(completedAt:
            new DateTime(2025, 1, 1)), CancellationToken.None);
        var provider = new FixedTimeProvider(new DateTimeOffset(
            new DateTime(2026, 7, 29), TimeSpan.FromHours(3)));
        var service = new ExecutionHistoryRetentionService(fixture.Context, provider);

        ExecutionHistoryRetentionCutoffs cutoffs =
            service.GetCutoffs(ExecutionHistoryRetentionPolicy.Version1);
        ExecutionHistoryRetentionResult first = await service.DeleteExpiredAsync(
            ExecutionHistoryRetentionPolicy.Version1, CancellationToken.None);
        ExecutionHistoryRetentionResult second = await service.DeleteExpiredAsync(
            ExecutionHistoryRetentionPolicy.Version1, CancellationToken.None);

        Assert.Equal(provider.GetLocalNow().DateTime.AddDays(-180), cutoffs.RunCutoff);
        Assert.Equal(1, first.RunsDeleted);
        Assert.Equal(1, first.StepsDeleted);
        Assert.Equal(1, first.AttemptsDeleted);
        Assert.Equal(0, second.RunsDeleted);
        Assert.Empty(await fixture.Context.ExecutionRunHistory.ToArrayAsync());
    }

    [Fact]
    public async Task Cancelled_write_does_not_persist_partial_rows()
    {
        await using Fixture fixture = await Fixture.Create();
        var writer = new ExecutionHistoryWriter(fixture.Context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            writer.WriteAsync(Projection(), cancellation.Token));
        Assert.Empty(await fixture.Context.ExecutionRunHistory.ToArrayAsync());
    }

    [Fact]
    public async Task Ef_model_has_history_keys_constraints_indexes_and_no_generic_event_table()
    {
        await using Fixture fixture = await Fixture.Create();
        var model = fixture.Context.Model;
        string[] tables = model.GetEntityTypes()
            .Select(x => x.GetTableName()).Where(x => x is not null)
            .Cast<string>().ToArray();
        Assert.Contains("ExecutionRunHistory", tables);
        Assert.Contains("ExecutionStepHistory", tables);
        Assert.Contains("ExecutionAttemptHistory", tables);
        Assert.Contains("ExecutionStateTransitionHistory", tables);
        Assert.Contains("ExecutionArtifactHistory", tables);
        Assert.Contains("ExecutionPolicyHistory", tables);
        Assert.DoesNotContain("AuditHistory", tables);
        Assert.DoesNotContain("MonitoringHistory", tables);
        Assert.DoesNotContain("ExecutionEventHistory", tables);
        foreach (var type in model.GetEntityTypes()
                     .Where(x => x.GetSchema() == "history"))
        {
            Assert.NotNull(type.FindPrimaryKey());
            Assert.Contains(type.GetIndexes(), x => x.IsUnique);
        }
    }

    [Fact]
    public void Projection_contract_is_immutable_safe_partial_and_deterministically_ordered()
    {
        ExecutionHistoryProjection value = Projection();
        Assert.IsAssignableFrom<IReadOnlyList<ExecutionStepHistoryItem>>(value.Steps);
        Assert.Equal(value.Steps.OrderBy(x => x.StepOrdinal), value.Steps);
        Assert.Equal(value.Transitions.OrderBy(x => x.TransitionSequence), value.Transitions);
        Assert.DoesNotContain("exception", value.Run.ProjectionReasonCode,
            StringComparison.OrdinalIgnoreCase);
        ExecutionHistoryProjection partial = value with
        {
            Run = value.Run with
            {
                ProjectionStatus = ExecutionHistoryProjectionStatus.Partial,
                ProjectionFailureCategory =
                    ExecutionHistoryFailureCategory.HistorySequenceInvalid,
                ProjectionReasonCode = "HistoryFactsIncomplete"
            },
            Transitions = Array.AsReadOnly(Array.Empty<ExecutionStateTransitionHistoryItem>())
        };
        Assert.Equal("Completed", partial.Run.TerminalState);
        Assert.Equal(ExecutionHistoryProjectionStatus.Partial,
            partial.Run.ProjectionStatus);
    }

    private static ExecutionHistoryProjection Projection(
        Guid? runId = null, DateTime? completedAt = null)
    {
        Guid run = runId ?? Guid.Parse("10000000-0000-0000-0000-000000000001");
        Guid step = Guid.Parse("20000000-0000-0000-0000-000000000001");
        DateTime completed = completedAt ?? new DateTime(2026, 7, 29, 12, 0, 0);
        var runValue = new ExecutionRunHistoryItem(run,
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Guid.Parse("50000000-0000-0000-0000-000000000001"),
            Guid.Parse("60000000-0000-0000-0000-000000000001"),
            Guid.Parse("70000000-0000-0000-0000-000000000001"),
            1, completed.AddMinutes(-1),
            completed.AddSeconds(-50), completed, TimeSpan.FromMinutes(1).Ticks,
            completed, "Completed", "Completed", "None", "RunCompleted", 0, 1, 0,
            1, 1, 0, 0, 0, 0, "sample.strategy", 1, "sample.plugin", 1, "1.0",
            "1.0", 1, 1, 1, 1, "ManagedTargetServer", true, 0, 1, 0, 0,
            ExecutionHistoryProjectionStatus.Completed,
            ExecutionHistoryFailureCategory.None, "HistoryProjectionCompleted");
        return new(ExecutionHistorySchemaVersion.Value, runValue,
            Array.AsReadOnly(new[]
            {
                new ExecutionStepHistoryItem(step, 1, 0, "sample.strategy", 1,
                    "sample.plugin", 1, "ManagedTargetServer",
                    completed.AddMinutes(-1), completed.AddSeconds(-50), completed,
                    1, 2, 3, "Completed", "None", "StepCompleted", 1, 0,
                    false, false, false, false, 0, 1, 0, 0, 0)
            }),
            Array.AsReadOnly(new[]
            {
                new ExecutionAttemptHistoryItem(step, 1, completed.AddSeconds(-50),
                    completed, TimeSpan.FromSeconds(50).Ticks, "Completed", "None",
                    "Success", false, null, false, false, 0)
            }),
            Array.AsReadOnly(new[]
            {
                new ExecutionStateTransitionHistoryItem(null, 1, "Run", null,
                    "Running", completed.AddSeconds(-50), "ExecutionRunStarted",
                    "RunStarted", "None", 1),
                new ExecutionStateTransitionHistoryItem(null, 2, "Run", "Running",
                    "Completed", completed, "ExecutionRunCompleted", "RunCompleted",
                    "None", 1)
            }),
            Array.AsReadOnly(new[]
            {
                new ExecutionArtifactHistoryItem(step, "artifact-1", 1, "Object",
                    "sample.object", null, 1, null, 0, completed)
            }),
            Array.AsReadOnly(new[]
            {
                new ExecutionHistoryPolicyProvenance(step, "ShortReadOnly", 1,
                    TimeSpan.FromSeconds(30).Ticks, "NoRetry", 1, 1, "None",
                    "SerialCore", 1, 1, "Lightweight", 1, 4, "Disabled", 1, false)
            }));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private Fixture(SqliteConnection connection, OperationsDbContext context)
        {
            this.connection = connection; Context = context;
        }
        public OperationsDbContext Context { get; }
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateFunction("ISJSON", (string? value) => value is null ? 0 : 1);
            var options = new DbContextOptionsBuilder<OperationsDbContext>()
                .UseSqlite(connection).Options;
            var context = new SqliteHistoryDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new(connection, context);
        }
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class SqliteHistoryDbContext(
        DbContextOptions<OperationsDbContext> options) : OperationsDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                         .SelectMany(x => x.GetProperties())
                         .Where(x => x.GetColumnType() == "nvarchar(max)"))
                property.SetColumnType("TEXT");
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
        public override TimeZoneInfo LocalTimeZone =>
            TimeZoneInfo.CreateCustomTimeZone("Fixed", TimeSpan.FromHours(3), "Fixed", "Fixed");
    }
}
