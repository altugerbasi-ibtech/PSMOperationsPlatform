namespace PSMOperationsPlatform.Infrastructure.Configuration;

internal static class OperationsDatabaseConfigurationFailures
{
    internal const string Missing = "OperationsDatabase.Missing";
    internal const string Malformed = "OperationsDatabase.Malformed";
    internal const string IntegratedAuthenticationRequired =
        "OperationsDatabase.IntegratedAuthenticationRequired";
    internal const string SqlAuthenticationNotSupported =
        "OperationsDatabase.SqlAuthenticationNotSupported";
}
