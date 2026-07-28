namespace RemoteMonitor.Server.Service;

using RemoteMonitor.Server.Bridge;
using RemoteMonitor.Server.Config;
using RemoteMonitor.Server.Logging;
using RemoteMonitor.Server.Networking;
using RemoteMonitor.Server.Services;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> logger;
    private readonly FileLogger fileLogger;
    private readonly RdpSessionService sessionService;
    private readonly BridgeService bridgeService;
    private readonly HttpApiServer apiServer;

    public Worker(ILogger<Worker> logger)
    {
        this.logger = logger;

        var options = ServerOptions.Default;
        fileLogger = new FileLogger(options.LogDirectory);
        sessionService = new RdpSessionService(fileLogger);
        bridgeService = new BridgeService(fileLogger);
        apiServer = new HttpApiServer(options, sessionService, bridgeService, fileLogger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await apiServer.StartAsync();
            fileLogger.Info("Server service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await sessionService.RefreshStatusAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Server service failed.");
            fileLogger.Error("Server service failed.", exception);
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        fileLogger.Info("Server service stopping.");
        apiServer.Dispose();
        bridgeService.Dispose();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        apiServer.Dispose();
        bridgeService.Dispose();
        base.Dispose();
    }
}
