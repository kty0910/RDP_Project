namespace RemoteMonitor.Server.Bridge;

public sealed class BridgeStatus
{
    public bool Enabled { get; init; }

    public int AllowedTargetCount { get; init; }

    public int ActiveForwarderCount { get; init; }

    public int RdpPortStart { get; init; }

    public int RdpPortEnd { get; init; }

    public string[] TargetNames { get; init; } = [];
}
