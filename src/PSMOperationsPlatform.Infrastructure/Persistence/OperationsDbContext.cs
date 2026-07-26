using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public class OperationsDbContext : DbContext
{
    private readonly ILogger<OperationsDbContext>? logger;

    public OperationsDbContext(
        DbContextOptions<OperationsDbContext> options,
        ILogger<OperationsDbContext>? logger = null)
        : base(options)
    {
        this.logger = logger;
        PersistenceLogger.ContextCreated(logger);
    }

    public DbSet<ManagedServer> ManagedServers => Set<ManagedServer>();

    public DbSet<CollectorNode> CollectorNodes => Set<CollectorNode>();

    public DbSet<CollectorHeartbeat> CollectorHeartbeats => Set<CollectorHeartbeat>();

    public DbSet<CollectorRun> CollectorRuns => Set<CollectorRun>();

    public DbSet<InventorySnapshot> InventorySnapshots => Set<InventorySnapshot>();

    public DbSet<CommandQueueItem> CommandQueueItems => Set<CommandQueueItem>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        const string operationType = "Sync";
        PersistenceLogger.SaveStarted(logger, operationType, CountAffectedEntries());

        try
        {
            EnsureAppendOnlyEntitiesAreNotChanged();
            int affectedRows = base.SaveChanges(acceptAllChangesOnSuccess);
            PersistenceLogger.SaveSucceeded(logger, operationType, affectedRows);
            return affectedRows;
        }
        catch (Exception exception)
        {
            throw LogAndMapException(operationType, exception);
        }
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        const string operationType = "Async";
        PersistenceLogger.SaveStarted(logger, operationType, CountAffectedEntries());

        try
        {
            EnsureAppendOnlyEntitiesAreNotChanged();
            int affectedRows =
                await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            PersistenceLogger.SaveSucceeded(logger, operationType, affectedRows);
            return affectedRows;
        }
        catch (Exception exception)
        {
            throw LogAndMapException(operationType, exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OperationsDbContext).Assembly);
    }

    private void EnsureAppendOnlyEntitiesAreNotChanged()
    {
        var violation = ChangeTracker.Entries()
            .FirstOrDefault(entry =>
                entry.Entity is AuditLog or CollectorHeartbeat
                && entry.State is EntityState.Modified or EntityState.Deleted);

        if (violation is not null)
        {
            throw new PersistenceAppendOnlyViolationException(
                violation.Metadata.ClrType.Name,
                violation.State.ToString());
        }
    }

    private int CountAffectedEntries() =>
        ChangeTracker.Entries().Count(entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);

    private Exception LogAndMapException(string operationType, Exception exception)
    {
        Exception outwardException =
            PersistenceExceptionClassifier.Map(exception) ?? exception;
        PersistenceLogger.SaveFailed(logger, operationType, outwardException);
        return outwardException;
    }
}
