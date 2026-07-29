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

    public DbSet<WindowsComputerInventory> WindowsComputerInventories =>
        Set<WindowsComputerInventory>();

    public DbSet<WindowsOperatingSystemInventory> WindowsOperatingSystemInventories =>
        Set<WindowsOperatingSystemInventory>();

    public DbSet<WindowsBiosInventory> WindowsBiosInventories =>
        Set<WindowsBiosInventory>();

    public DbSet<WindowsMemoryInventory> WindowsMemoryInventories =>
        Set<WindowsMemoryInventory>();

    public DbSet<WindowsProcessorInventory> WindowsProcessorInventories =>
        Set<WindowsProcessorInventory>();

    public DbSet<WindowsDiskInventory> WindowsDiskInventories =>
        Set<WindowsDiskInventory>();

    public DbSet<WindowsVolumeInventory> WindowsVolumeInventories =>
        Set<WindowsVolumeInventory>();

    public DbSet<WindowsNetworkAdapterInventory> WindowsNetworkAdapterInventories =>
        Set<WindowsNetworkAdapterInventory>();

    public DbSet<WindowsIpv4AddressInventory> WindowsIpv4AddressInventories =>
        Set<WindowsIpv4AddressInventory>();

    public DbSet<WindowsRoleInventory> WindowsRoleInventories =>
        Set<WindowsRoleInventory>();

    public DbSet<WindowsFeatureInventory> WindowsFeatureInventories =>
        Set<WindowsFeatureInventory>();

    public DbSet<WindowsIisPlatformInventory> WindowsIisPlatformInventories =>
        Set<WindowsIisPlatformInventory>();

    public DbSet<WindowsDotNetPlatformInventory> WindowsDotNetPlatformInventories =>
        Set<WindowsDotNetPlatformInventory>();

    public DbSet<WindowsPowerShellPlatformInventory> WindowsPowerShellPlatformInventories =>
        Set<WindowsPowerShellPlatformInventory>();

    public DbSet<WindowsCapabilitySnapshot> WindowsCapabilitySnapshots =>
        Set<WindowsCapabilitySnapshot>();

    public DbSet<WindowsCapabilityEntry> WindowsCapabilityEntries =>
        Set<WindowsCapabilityEntry>();
    public DbSet<WindowsCapabilityProvenance> WindowsCapabilityProvenance => Set<WindowsCapabilityProvenance>();
    public DbSet<CollectorDecisionPlan> CollectorDecisionPlans => Set<CollectorDecisionPlan>();
    public DbSet<CollectorStrategyDecision> CollectorStrategyDecisions => Set<CollectorStrategyDecision>();
    public DbSet<CollectorDecisionCapabilityReference> CollectorDecisionCapabilityReferences =>
        Set<CollectorDecisionCapabilityReference>();
    public DbSet<ExecutionPlan> ExecutionPlans => Set<ExecutionPlan>();
    public DbSet<ExecutionPlanStep> ExecutionPlanSteps => Set<ExecutionPlanStep>();
    public DbSet<ExecutionPlanExclusion> ExecutionPlanExclusions => Set<ExecutionPlanExclusion>();
    public DbSet<ExecutionPlanExclusionCapability> ExecutionPlanExclusionCapabilities =>
        Set<ExecutionPlanExclusionCapability>();
    public DbSet<ExecutionRunStateEntity> ExecutionRunStates => Set<ExecutionRunStateEntity>();
    public DbSet<ExecutionStepStateEntity> ExecutionStepStates => Set<ExecutionStepStateEntity>();
    public DbSet<ExecutionAttemptStateEntity> ExecutionAttemptStates => Set<ExecutionAttemptStateEntity>();
    public DbSet<ExecutionRunHistoryEntity> ExecutionRunHistory => Set<ExecutionRunHistoryEntity>();
    public DbSet<ExecutionStepHistoryEntity> ExecutionStepHistory => Set<ExecutionStepHistoryEntity>();
    public DbSet<ExecutionAttemptHistoryEntity> ExecutionAttemptHistory => Set<ExecutionAttemptHistoryEntity>();
    public DbSet<ExecutionStateTransitionHistoryEntity> ExecutionStateTransitionHistory =>
        Set<ExecutionStateTransitionHistoryEntity>();
    public DbSet<ExecutionArtifactHistoryEntity> ExecutionArtifactHistory =>
        Set<ExecutionArtifactHistoryEntity>();
    public DbSet<ExecutionPolicyHistoryEntity> ExecutionPolicyHistory =>
        Set<ExecutionPolicyHistoryEntity>();

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
