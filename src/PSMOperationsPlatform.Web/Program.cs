using System.Reflection;
using PSMOperationsPlatform.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.ConfigurePsmConfiguration(
    builder.Environment.EnvironmentName,
    args,
    Assembly.GetExecutingAssembly());
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
