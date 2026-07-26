using PSMOperationsPlatform.Domain.Common;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class CommandQueueItem : Entity
{
    public CommandQueueItem(
        Guid id,
        string commandType,
        CollectorType targetCollectorType,
        string payloadJson,
        int priority,
        DateTime createdAt,
        string createdBy,
        Guid? managedServerId = null,
        DateTime? notBefore = null)
        : base(id)
    {
        if (managedServerId == Guid.Empty)
        {
            throw new ArgumentException("Managed server identifier cannot be empty.", nameof(managedServerId));
        }

        if (priority < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        CommandType = Required(commandType, nameof(commandType));
        TargetCollectorType = EnumGuard.Defined(
            targetCollectorType,
            nameof(targetCollectorType));
        ManagedServerId = managedServerId;
        PayloadJson = InventorySnapshot.ValidJson(payloadJson, nameof(payloadJson));
        Status = CommandStatus.Pending;
        Priority = priority;
        NotBefore = notBefore;
        CreatedAt = createdAt;
        CreatedBy = Required(createdBy, nameof(createdBy));
        RowVersion = null!;
    }

    private CommandQueueItem()
    {
        CommandType = null!;
        PayloadJson = null!;
        CreatedBy = null!;
        RowVersion = null!;
    }

    public string CommandType { get; private set; }

    public CollectorType TargetCollectorType { get; private set; }

    public Guid? ManagedServerId { get; private set; }

    public string PayloadJson { get; private set; }

    public CommandStatus Status { get; private set; }

    public int Priority { get; private set; }

    public DateTime? NotBefore { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public string CreatedBy { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public string? FailureCode { get; private set; }

    public string? FailureMessage { get; private set; }

    public byte[] RowVersion { get; private set; }

    public void Complete(DateTime completedAt) =>
        SetTerminalState(CommandStatus.Completed, completedAt, null, null);

    public void Fail(DateTime completedAt, string failureCode, string? failureMessage) =>
        SetTerminalState(
            CommandStatus.Failed,
            completedAt,
            Required(failureCode, nameof(failureCode)),
            Optional(failureMessage));

    public void Cancel(DateTime completedAt) =>
        SetTerminalState(CommandStatus.Cancelled, completedAt, null, null);

    private void SetTerminalState(
        CommandStatus status,
        DateTime completedAt,
        string? failureCode,
        string? failureMessage)
    {
        if (Status != CommandStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending command can be completed.");
        }

        if (completedAt < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(completedAt));
        }

        Status = status;
        CompletedAt = completedAt;
        FailureCode = failureCode;
        FailureMessage = failureMessage;
    }

    private static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
