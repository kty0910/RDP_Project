using System.Net.Sockets;
using RemoteMonitor.Client.Models;

namespace RemoteMonitor.Client.Services;

public sealed class RdpAvailabilityService
{
    public async Task<bool> CanConnectAsync(RemotePcInfo remotePc, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(remotePc.Host, remotePc.RdpPort).WaitAsync(
                TimeSpan.FromSeconds(3),
                cancellationToken);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
