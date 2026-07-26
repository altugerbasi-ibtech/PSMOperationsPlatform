using PSMOperationsPlatform.Domain.Common;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class CollectorRun : Entity
{
    public CollectorRun(
        Guid id,
        Guid collectorNodeId,
        Guid managedServerId,
        CollectionType collectionType,
        DateTime createdAt)
        : base(id)
    {
        CollectorNodeId = RequiredId(collectorNodeId, nameof(collectorNodeId));
        ManagedServerId = RequiredId(managedServerId, nameof(managedServerId));
        CollectionType = EnumGuard.Defined(collectionType, nameof(collectionType));
        Status = CollectorRunStatus.Pending;
        CreatedAt = createdAt;
    }

    private CollectorRun()
    {
    }

    public Guid CollectorNodeId { get; private set; }

    public Guid ManagedServerId { get; private set; }

    public CollectionType CollectionType { get; private set; }

    public CollectorRunStatus Status { get; private set; }

    public DateTime? StartedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorMessage { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public void Start(DateTime startedAt)
    {
        EnsureStatus(CollectorRunStatus.Pending);
        EnsureNotBeforeCreation(startedAt);
        Status = CollectorRunStatus.Running;
        StartedAt = startedAt;
    }

    public void Succeed(DateTime completedAt) =>
        Complete(CollectorRunStatus.Succeeded, completedAt, null, null);

    public void Fail(DateTime completedAt, string errorCode, string? errorMessage) =>
        Complete(
            CollectorRunStatus.Failed,
            completedAt,
            Required(errorCode, nameof(errorCode)),
            Optional(errorMessage));

    public void Cancel(DateTime completedAt) =>
        Complete(CollectorRunStatus.Cancelled, completedAt, null, null);

    private void Complete(
        CollectorRunStatus terminalStatus,
        DateTime completedAt,
        string? errorCode,
        string? errorMessage)
    {
        EnsureStatus(CollectorRunStatus.Running);
        if (completedAt < StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(completedAt));
        }

        Status = terminalStatus;
        CompletedAt = completedAt;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    private void EnsureStatus(CollectorRunStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Collector run must be {expected}.");
        }
    }

    private void EnsureNotBeforeCreation(DateTime value)
    {
        if (value < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    private static Guid RequiredId(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException("Identifier cannot be empty.", parameterName)
            : value;

    private static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
