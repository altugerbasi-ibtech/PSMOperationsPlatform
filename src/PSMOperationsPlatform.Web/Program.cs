using System.Reflection;
using PSMOperationsPlatform.Infrastructure.Configuration;
using PSMOperationsPlatform.Web.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.ConfigurePsmConfiguration(
    builder.Environment.EnvironmentName,
    args,
    Assembly.GetExecutingAssembly());
builder.Services.AddHealthChecks();
builder.Services.AddPortalWindowsAuthentication();

var app = builder.Build();

app.UsePortalAuthentication();

app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
