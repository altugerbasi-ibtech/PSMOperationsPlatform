using PSMOperationsPlatform.Domain.Common;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class ManagedServer : Entity
{
    public ManagedServer(
        Guid id,
        string fqdn,
        DateTime createdAt,
        string? displayName = null,
        string? environment = null,
        bool isEnabled = true)
        : base(id)
    {
        Fqdn = NormalizeFqdn(fqdn);
        DisplayName = NormalizeOptional(displayName);
        Environment = NormalizeOptional(environment);
        IsEnabled = isEnabled;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    private ManagedServer()
    {
        Fqdn = null!;
    }

    public string Fqdn { get; private set; }

    public string? DisplayName { get; private set; }

    public string? Environment { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public void UpdateDetails(string? displayName, string? environment, DateTime updatedAt)
    {
        EnsureNotBeforeCreation(updatedAt);
        DisplayName = NormalizeOptional(displayName);
        Environment = NormalizeOptional(environment);
        UpdatedAt = updatedAt;
    }

    public void ChangeFqdn(string fqdn, DateTime updatedAt)
    {
        EnsureNotBeforeCreation(updatedAt);
        Fqdn = NormalizeFqdn(fqdn);
        UpdatedAt = updatedAt;
    }

    public void SetEnabled(bool isEnabled, DateTime updatedAt)
    {
        EnsureNotBeforeCreation(updatedAt);
        IsEnabled = isEnabled;
        UpdatedAt = updatedAt;
    }

    private void EnsureNotBeforeCreation(DateTime value)
    {
        if (value < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Update time cannot precede creation time.");
        }
    }

    private static string NormalizeFqdn(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
