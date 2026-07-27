using Microsoft.Extensions.Logging;

namespace PSMOperationsPlatform.Infrastructure.Configuration;

internal static class OperationsDatabaseConfigurationLogEvents
{
    internal const int ConfigurationValidatedId = 2200;
    internal const string ConfigurationValidatedName =
        "OperationsDatabaseConfigurationValidated";

    internal static readonly EventId ConfigurationValidated =
        new(ConfigurationValidatedId, ConfigurationValidatedName);
}
