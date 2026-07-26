using PSMOperationsPlatform.Domain.Common;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class AuditLog : Entity
{
    public AuditLog(
        Guid id,
        DateTime occurredAt,
        string actor,
        string action,
        AuditOutcome outcome,
        string? entityType = null,
        Guid? entityId = null,
        Guid? correlationId = null,
        string? detailJson = null)
        : base(id)
    {
        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("Entity identifier cannot be empty.", nameof(entityId));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("Correlation identifier cannot be empty.", nameof(correlationId));
        }

        OccurredAt = occurredAt;
        Actor = Required(actor, nameof(actor));
        Action = Required(action, nameof(action));
        EntityType = Optional(entityType);
        EntityId = entityId;
        CorrelationId = correlationId;
        Outcome = EnumGuard.Defined(outcome, nameof(outcome));
        DetailJson = detailJson is null
            ? null
            : InventorySnapshot.ValidJson(detailJson, nameof(detailJson));
    }

    private AuditLog()
    {
        Actor = null!;
        Action = null!;
    }

    public DateTime OccurredAt { get; private set; }

    public string Actor { get; private set; }

    public string Action { get; private set; }

    public string? EntityType { get; private set; }

    public Guid? EntityId { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public AuditOutcome Outcome { get; private set; }

    public string? DetailJson { get; private set; }

    private static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
