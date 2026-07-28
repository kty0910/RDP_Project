using RemoteMonitor.Server.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Remote Monitor Server";
});
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
