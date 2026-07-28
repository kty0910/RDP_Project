using System.Net;
using System.Net.Sockets;
using RemoteMonitor.Client.Models;
using RemoteMonitor.Client.Networking;
using RemoteMonitor.Server.Bridge;
using RemoteMonitor.Server.Config;
using RemoteMonitor.Server.Logging;
using RemoteMonitor.Server.Networking;
using RemoteMonitor.Server.Services;

var portProbe = new TcpListener(IPAddress.Loopback, 0);
portProbe.Start();
var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
portProbe.Stop();

var logger = new FileLogger(Path.Combine(AppContext.BaseDirectory, "Logs"));
var sessionService = new RdpSessionService(logger);
using var bridgeService = new BridgeService(logger);
using var apiServer = new HttpApiServer(
    new ServerOptions
    {
        Port = port,
        LogDirectory = Path.Combine(AppContext.BaseDirectory, "Logs")
    },
    sessionService,
    bridgeService,
    logger);

await apiServer.StartAsync();

var apiClient = new RemoteMonitorApiClient();
var remotePc = new RemotePcInfo
{
    Name = "status-push-smoke-test",
    Host = "127.0.0.1",
    Port = port,
    RdpPort = 3389
};
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await using var stream = apiClient
    .StreamStatusAsync(remotePc, timeout.Token)
    .GetAsyncEnumerator(timeout.Token);

if (!await stream.MoveNextAsync().AsTask().WaitAsync(timeout.Token))
{
    throw new InvalidOperationException("The status stream did not return its initial snapshot.");
}

await sessionService.RefreshStatusAsync(timeout.Token);

if (!await stream.MoveNextAsync().AsTask().WaitAsync(timeout.Token))
{
    throw new InvalidOperationException("The status stream did not publish a changed status.");
}

if (stream.Current.CheckedAt == DateTime.MinValue)
{
    throw new InvalidOperationException("The changed status did not contain a valid observation time.");
}

Console.WriteLine("PASS: snapshot and statusChanged events were received.");
