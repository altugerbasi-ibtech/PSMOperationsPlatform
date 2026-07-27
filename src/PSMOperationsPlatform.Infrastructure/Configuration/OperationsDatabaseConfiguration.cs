using Microsoft.Extensions.Configuration;

namespace PSMOperationsPlatform.Infrastructure.Configuration;

internal sealed class OperationsDatabaseConfiguration(IConfiguration configuration)
    : IOperationsDatabaseConfiguration
{
    public string? GetConnectionString() =>
        configuration.GetConnectionString("OperationsDatabase");
}
