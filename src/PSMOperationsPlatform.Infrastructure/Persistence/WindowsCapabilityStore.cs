using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PSMOperationsPlatform.Application.Capabilities;
using PSMOperationsPlatform.Domain.Entities;

namespace PSMOperationsPlatform.Infrastructure.Persistence;

public interface IWindowsCapabilityCoordinator
{
    Task EvaluateAndReplaceAsync(Guid managedServerId, Guid inventoryRunId,
        long inventoryVersion, DateTime capturedAt, CoreWindowsInventorySnapshot snapshot,
        CancellationToken cancellationToken);
}

public sealed class WindowsCapabilityCoordinator(
    OperationsDbContext context,
    ICapabilityEngine engine,
    TimeProvider timeProvider,
    ILogger<WindowsCapabilityCoordinator> logger,
    ICollectorDecisionCoordinator? decisionCoordinator = null) : IWindowsCapabilityCoordinator
{
    public async Task EvaluateAndReplaceAsync(Guid managedServerId, Guid inventoryRunId,
        long inventoryVersion, DateTime capturedAt, CoreWindowsInventorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        long started = timeProvider.GetTimestamp();
        logger.LogInformation(
            "Capability evaluation started. ManagedServerId={ManagedServerId} InventoryRunId={InventoryRunId} SourceInventoryVersion={SourceInventoryVersion} CapabilitySchemaVersion={CapabilitySchemaVersion}",
            managedServerId, inventoryRunId, inventoryVersion, CapabilityEngine.SchemaVersion);
        var input = new PlatformCapabilityInput(
            managedServerId, inventoryRunId, inventoryVersion, capturedAt,
            snapshot.IisPlatforms?.Select(x => new IisCapabilityFact(x.IisKey, x.Installed, x.Version)).ToArray(),
            snapshot.DotNetPlatforms?.Select(x => new DotNetCapabilityFact(x.DotNetKey, x.Category, x.Version, x.Release)).ToArray(),
            snapshot.PowerShellPlatforms?.Select(x => new PowerShellCapabilityFact(x.PowerShellKey, x.Edition, x.Version)).ToArray(),
            snapshot.WindowsRoles?.Select(x => new WindowsFeatureCapabilityFact(x.RoleKey, x.Name)).ToArray(),
            snapshot.WindowsFeatures?.Select(x => new WindowsFeatureCapabilityFact(x.FeatureKey, x.Name)).ToArray());
        CapabilityEvaluationResult result = engine.Evaluate(input);
        if (result.Entries.Select(x => x.CapabilityCode).Distinct(StringComparer.Ordinal).Count()
            != result.Entries.Count)
            throw new InvalidOperationException("Capability evaluation produced duplicate capability codes.");

        Guid snapshotId = Guid.NewGuid();
        await InventoryStoreGuard.ExecuteTransactionAsync(context, async () =>
        {
            WindowsCapabilitySnapshot? previous = await context.WindowsCapabilitySnapshots
                .Include(x => x.Entries).ThenInclude(x => x.Provenance)
                .SingleOrDefaultAsync(x => x.ManagedServerId == managedServerId, cancellationToken);
            if (previous is not null)
            {
                context.WindowsCapabilityProvenance.RemoveRange(previous.Entries.SelectMany(x => x.Provenance));
                context.WindowsCapabilityEntries.RemoveRange(previous.Entries);
                context.WindowsCapabilitySnapshots.Remove(previous);
            }
            var entity = new WindowsCapabilitySnapshot(snapshotId, managedServerId,
                inventoryRunId, inventoryVersion, result.CapabilitySchemaVersion,
                result.EvaluatedAt, result.EvaluationStatus.ToString());
            foreach (CapabilityEntry entry in result.Entries)
            {
                var capabilityEntity = new WindowsCapabilityEntry(Guid.NewGuid(), entity.Id,
                    entry.CapabilityCode, entry.Subject.ToString(), entry.Category.ToString(),
                    entry.SupportStatus.ToString(), entry.ReadinessStatus.ToString(), entry.RuleVersion, entry.ReasonCode, entry.Reason);
                foreach (PlatformFactReference fact in entry.SourceFacts)
                    capabilityEntity.Provenance.Add(new WindowsCapabilityProvenance(Guid.NewGuid(),
                        capabilityEntity.Id, fact.ModuleName, fact.Category, fact.FactKey,
                        inventoryRunId, inventoryVersion));
                entity.Entries.Add(capabilityEntity);
            }
            context.WindowsCapabilitySnapshots.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        if (decisionCoordinator is not null)
            await decisionCoordinator.EvaluateAndReplaceAsync(snapshotId, result, cancellationToken);
        logger.LogInformation(
            "Capability evaluation completed. ManagedServerId={ManagedServerId} InventoryRunId={InventoryRunId} SourceInventoryVersion={SourceInventoryVersion} CapabilitySchemaVersion={CapabilitySchemaVersion} DurationMs={DurationMs} RuleCount={RuleCount} SupportedCount={SupportedCount} NotSupportedCount={NotSupportedCount} UnknownCount={UnknownCount} InvalidCount={InvalidCount} PersistenceStatus={PersistenceStatus}",
            managedServerId, inventoryRunId, inventoryVersion, result.CapabilitySchemaVersion,
            timeProvider.GetElapsedTime(started).TotalMilliseconds, result.Entries.Count,
            result.Entries.Count(x => x.SupportStatus == CapabilityStatus.Supported),
            result.Entries.Count(x => x.SupportStatus == CapabilityStatus.NotSupported),
            result.Entries.Count(x => x.SupportStatus == CapabilityStatus.Unknown),
            result.Entries.Count(x => x.SupportStatus == CapabilityStatus.Invalid), "Succeeded");
    }
}
