using PSMOperationsPlatform.WindowsCollector;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHealthChecks();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
