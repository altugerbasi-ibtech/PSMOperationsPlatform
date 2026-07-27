using System.Reflection;
using PSMOperationsPlatform.Infrastructure.Configuration;
using PSMOperationsPlatform.SqlCollector;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.ConfigurePsmConfiguration(
    builder.Environment.EnvironmentName,
    args,
    Assembly.GetExecutingAssembly());
builder.Services.AddHealthChecks();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
