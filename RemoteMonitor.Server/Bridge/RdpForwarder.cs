using System.Net;
using System.Net.Sockets;

namespace RemoteMonitor.Server.Bridge;

public sealed class RdpForwarder : IDisposable
{
    private readonly string targetHost;
    private readonly int targetPort;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellationSource = new();

    public RdpForwarder(string targetHost, int targetPort, int listenPort)
    {
        this.targetHost = targetHost;
        this.targetPort = targetPort;
        ListenPort = listenPort;
        listener = new TcpListener(IPAddress.Any, listenPort);
    }

    public int ListenPort { get; }

    public void Start()
    {
        listener.Start();
        _ = AcceptLoopAsync(cancellationSource.Token);
    }

    public void Dispose()
    {
        cancellationSource.Cancel();
        listener.Stop();
        cancellationSource.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = ForwardAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Keep accepting future connections.
            }
        }
    }

    private async Task ForwardAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var clientConnection = client;
        using var targetConnection = new TcpClient();
        await targetConnection.ConnectAsync(targetHost, targetPort, cancellationToken);

        await using var clientStream = clientConnection.GetStream();
        await using var targetStream = targetConnection.GetStream();

        var clientToTarget = clientStream.CopyToAsync(targetStream, cancellationToken);
        var targetToClient = targetStream.CopyToAsync(clientStream, cancellationToken);
        await Task.WhenAny(clientToTarget, targetToClient);
    }
}
