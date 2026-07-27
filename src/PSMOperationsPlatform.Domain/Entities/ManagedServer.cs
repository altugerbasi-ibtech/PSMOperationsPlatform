using PSMOperationsPlatform.Domain.Common;
using PSMOperationsPlatform.Domain.Enums;

namespace PSMOperationsPlatform.Domain.Entities;

public sealed class ManagedServer : Entity
{
    public ManagedServer(
        Guid id,
        string fqdn,
        DateTime createdAt,
        string? displayName = null,
        string? environment = null,
        bool isEnabled = true,
        WinRmTransportMode winRmTransportMode = WinRmTransportMode.Auto,
        int winRmHttpsPort = 5986,
        int winRmHttpPort = 5985,
        int winRmProbeTimeoutSeconds = 10)
        : base(id)
    {
        Fqdn = NormalizeFqdn(fqdn);
        DisplayName = NormalizeOptional(displayName);
        Environment = NormalizeOptional(environment);
        IsEnabled = isEnabled;
        WinRmTransportMode = EnumGuard.Defined(
            winRmTransportMode,
            nameof(winRmTransportMode));
        WinRmHttpsPort = ValidPort(winRmHttpsPort, nameof(winRmHttpsPort));
        WinRmHttpPort = ValidPort(winRmHttpPort, nameof(winRmHttpPort));
        WinRmProbeTimeoutSeconds = Positive(
            winRmProbeTimeoutSeconds,
            nameof(winRmProbeTimeoutSeconds));
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        LastConnectivityState = ConnectivityState.Unknown;
        RowVersion = null!;
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

    public DateTime? NextConnectivityAttemptAt { get; private set; }

    public WinRmTransportMode WinRmTransportMode { get; private set; }

    public int WinRmHttpsPort { get; private set; }

    public int WinRmHttpPort { get; private set; }

    public int WinRmProbeTimeoutSeconds { get; private set; }

    public ConnectivityState LastConnectivityState { get; private set; }

    public DateTime? LastConnectivityAttemptAt { get; private set; }

    public DateTime? LastConnectivitySuccessAt { get; private set; }

    public ConnectivityTransport? LastSuccessfulTransport { get; private set; }

    public int ConsecutiveConnectivityFailures { get; private set; }

    public ConnectivityFailureCategory? LastConnectivityFailureCategory { get; private set; }

    public byte[] RowVersion { get; private set; } = null!;

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
        ResetConnectivityEligibility();
        UpdatedAt = updatedAt;
    }

    public void SetEnabled(bool isEnabled, DateTime updatedAt)
    {
        EnsureNotBeforeCreation(updatedAt);
        bool isBeingEnabled = !IsEnabled && isEnabled;
        IsEnabled = isEnabled;
        if (isBeingEnabled)
        {
            ResetConnectivityEligibility();
        }

        UpdatedAt = updatedAt;
    }

    public void ApplyConnectivitySuccess(
        DateTime completedAt,
        ConnectivityTransport successfulTransport,
        DateTime nextAttemptAt)
    {
        EnsureCompletedAttempt(completedAt, nextAttemptAt);
        LastConnectivityState = ConnectivityState.Reachable;
        LastConnectivityAttemptAt = completedAt;
        LastConnectivitySuccessAt = completedAt;
        LastSuccessfulTransport = EnumGuard.Defined(
            successfulTransport,
            nameof(successfulTransport));
        ConsecutiveConnectivityFailures = 0;
        LastConnectivityFailureCategory = null;
        NextConnectivityAttemptAt = nextAttemptAt;
    }

    public void ApplyConnectivityFailure(
        DateTime completedAt,
        ConnectivityFailureCategory failureCategory,
        DateTime nextAttemptAt)
    {
        EnsureCompletedAttempt(completedAt, nextAttemptAt);
        LastConnectivityState = ConnectivityState.Unreachable;
        LastConnectivityAttemptAt = completedAt;
        ConsecutiveConnectivityFailures =
            ConsecutiveConnectivityFailures == int.MaxValue
                ? int.MaxValue
                : ConsecutiveConnectivityFailures + 1;
        LastConnectivityFailureCategory = EnumGuard.Defined(
            failureCategory,
            nameof(failureCategory));
        NextConnectivityAttemptAt = nextAttemptAt;
    }

    private void EnsureNotBeforeCreation(DateTime value)
    {
        if (value < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Update time cannot precede creation time.");
        }
    }

    private void EnsureCompletedAttempt(DateTime completedAt, DateTime nextAttemptAt)
    {
        EnsureNotBeforeCreation(completedAt);
        if (nextAttemptAt < completedAt)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextAttemptAt),
                "Next attempt cannot precede the completed attempt.");
        }
    }

    private void ResetConnectivityEligibility()
    {
        LastConnectivityState = ConnectivityState.Unknown;
        ConsecutiveConnectivityFailures = 0;
        LastConnectivityFailureCategory = null;
        NextConnectivityAttemptAt = null;
    }

    private static string NormalizeFqdn(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int ValidPort(int value, string parameterName)
    {
        if (value is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Port must be between 1 and 65535.");
        }

        return value;
    }

    private static int Positive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be positive.");
        }

        return value;
    }
}
