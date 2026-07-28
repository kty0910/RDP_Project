namespace RemoteMonitor.Server.Models;

public sealed class ServerStatusResponse
{
    public DateTime CheckedAt { get; init; } = DateTime.Now;

    public bool HasActiveRdpSession { get; init; }

    public int ActiveRdpSessionCount { get; init; }

    public IReadOnlyList<RdpSessionInfo> Sessions { get; init; } = Array.Empty<RdpSessionInfo>();
}
