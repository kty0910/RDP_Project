using System.Collections.Concurrent;
using System.Net.Sockets;

namespace RemoteMonitor.Server.Bridge;

public sealed class RdpForwarderManager : IDisposable
{
    private readonly ConcurrentDictionary<string, RdpForwarder> forwarders = new(StringComparer.OrdinalIgnoreCase);
    private int nextPort = 13388;

    public int ActiveForwarderCount => forwarders.Count;

    public int EnsureForwarder(
        string host,
        int port,
        int rdpPortStart,
        int rdpPortEnd,
        CancellationToken cancellationToken)
    {
        var key = $"{host}:{port}";

        if (forwarders.TryGetValue(key, out var existing))
        {
            return existing.ListenPort;
        }

        for (var attempt = rdpPortStart; attempt <= rdpPortEnd; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var listenPort = GetNextPort(rdpPortStart, rdpPortEnd);

            try
            {
                var forwarder = new RdpForwarder(host, port, listenPort);
                forwarder.Start();

                if (forwarders.TryAdd(key, forwarder))
                {
                    return listenPort;
                }

                forwarder.Dispose();
                return forwarders[key].ListenPort;
            }
            catch (SocketException)
            {
                // Try next port.
            }
        }

        throw new InvalidOperationException("No available RDP forwarding port remains.");
    }

    public void Dispose()
    {
        StopAll();
    }

    public void StopAll()
    {
        foreach (var forwarder in forwarders.Values)
        {
            forwarder.Dispose();
        }

        forwarders.Clear();
    }

    private int GetNextPort(int rdpPortStart, int rdpPortEnd)
    {
        if (nextPort < rdpPortStart - 1 || nextPort >= rdpPortEnd)
        {
            Interlocked.Exchange(ref nextPort, rdpPortStart - 1);
        }

        var port = Interlocked.Increment(ref nextPort);

        if (port <= rdpPortEnd)
        {
            return port;
        }

        Interlocked.Exchange(ref nextPort, rdpPortStart);
        return rdpPortStart;
    }
}
