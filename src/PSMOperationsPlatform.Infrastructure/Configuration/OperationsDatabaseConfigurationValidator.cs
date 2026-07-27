using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace PSMOperationsPlatform.Infrastructure.Configuration;

internal sealed class OperationsDatabaseConfigurationValidator(
    IOperationsDatabaseConfiguration configuration)
    : IValidateOptions<OperationsDatabaseValidationMarker>
{
    private const string FirstUserSentinel =
        "PSM_VALIDATION_USER_SENTINEL_A";
    private const string SecondUserSentinel =
        "PSM_VALIDATION_USER_SENTINEL_B";
    private const string FirstPasswordSentinel =
        "PSM_VALIDATION_PASSWORD_SENTINEL_A";
    private const string SecondPasswordSentinel =
        "PSM_VALIDATION_PASSWORD_SENTINEL_B";

    public ValidateOptionsResult Validate(
        string? name,
        OperationsDatabaseValidationMarker options)
    {
        string? connectionString = configuration.GetConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ValidateOptionsResult.Fail(
                OperationsDatabaseConfigurationFailures.Missing);
        }

        SqlConnectionStringBuilder builder;
        bool hasSqlCredentialKeys;

        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
            hasSqlCredentialKeys = HasExplicitSqlCredentialKeys(
                connectionString);
        }
        catch (ArgumentException)
        {
            return ValidateOptionsResult.Fail(
                OperationsDatabaseConfigurationFailures.Malformed);
        }

        if (hasSqlCredentialKeys)
        {
            return ValidateOptionsResult.Fail(
                OperationsDatabaseConfigurationFailures
                    .SqlAuthenticationNotSupported);
        }

        if (!builder.IntegratedSecurity)
        {
            return ValidateOptionsResult.Fail(
                OperationsDatabaseConfigurationFailures
                    .IntegratedAuthenticationRequired);
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            return ValidateOptionsResult.Fail(
                OperationsDatabaseConfigurationFailures.Malformed);
        }

        return ValidateOptionsResult.Success;
    }

    private static bool HasExplicitSqlCredentialKeys(
        string connectionString)
    {
        var firstProbe = new SqlConnectionStringBuilder(
            $"User ID={FirstUserSentinel};" +
            $"Password={FirstPasswordSentinel};" +
            connectionString);
        var secondProbe = new SqlConnectionStringBuilder(
            $"User ID={SecondUserSentinel};" +
            $"Password={SecondPasswordSentinel};" +
            connectionString);

        bool userIdWasOverridden =
            !string.Equals(
                firstProbe.UserID,
                FirstUserSentinel,
                StringComparison.Ordinal) ||
            !string.Equals(
                secondProbe.UserID,
                SecondUserSentinel,
                StringComparison.Ordinal);
        bool passwordWasOverridden =
            !string.Equals(
                firstProbe.Password,
                FirstPasswordSentinel,
                StringComparison.Ordinal) ||
            !string.Equals(
                secondProbe.Password,
                SecondPasswordSentinel,
                StringComparison.Ordinal);

        return userIdWasOverridden || passwordWasOverridden;
    }
}
