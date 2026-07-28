namespace RemoteMonitor.Server.Bridge;

public sealed class RdpStartRequest
{
    public string Host { get; init; } = string.Empty;

    public int ApiPort { get; init; } = 5000;

    public int RdpPort { get; init; } = 3389;
}
