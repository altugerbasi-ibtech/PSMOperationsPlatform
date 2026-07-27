namespace PSMOperationsPlatform.Infrastructure.Configuration;

public interface IOperationsDatabaseConfiguration
{
    string? GetConnectionString();
}
