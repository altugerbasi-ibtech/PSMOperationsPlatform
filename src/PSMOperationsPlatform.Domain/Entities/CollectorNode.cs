using PSMOperationsPlatform.Domain.Common;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class CollectorNode : Entity
{
    public CollectorNode(
        Guid id,
        string name,
        CollectorType collectorType,
        string hostFqdn,
        string instanceKey,
        DateTime registeredAt,
        string? version = null,
        bool isEnabled = true)
        : base(id)
    {
        Name = Required(name, nameof(name));
        CollectorType = EnumGuard.Defined(collectorType, nameof(collectorType));
        HostFqdn = NormalizeFqdn(hostFqdn);
        InstanceKey = Required(instanceKey, nameof(instanceKey));
        Version = Optional(version);
        IsEnabled = isEnabled;
        RegisteredAt = registeredAt;
        UpdatedAt = registeredAt;
        RowVersion = null!;
    }

    private CollectorNode()
    {
        Name = null!;
        HostFqdn = null!;
        InstanceKey = null!;
        RowVersion = null!;
    }

    public string Name { get; private set; }

    public CollectorType CollectorType { get; private set; }

    public string HostFqdn { get; private set; }

    public string InstanceKey { get; private set; }

    public string? Version { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTime RegisteredAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; }

    public void UpdateMetadata(string name, string? version, DateTime updatedAt)
    {
        EnsureValidUpdateTime(updatedAt);
        Name = Required(name, nameof(name));
        Version = Optional(version);
        UpdatedAt = updatedAt;
    }

    public void SetEnabled(bool isEnabled, DateTime updatedAt)
    {
        EnsureValidUpdateTime(updatedAt);
        IsEnabled = isEnabled;
        UpdatedAt = updatedAt;
    }

    private void EnsureValidUpdateTime(DateTime value)
    {
        if (value < RegisteredAt)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Update time cannot precede registration time.");
        }
    }

    private static string NormalizeFqdn(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private static string Required(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
