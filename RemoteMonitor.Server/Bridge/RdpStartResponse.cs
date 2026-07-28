namespace RemoteMonitor.Server.Bridge;

public sealed class RdpStartResponse
{
    public RdpStartResponse(int bridgeRdpPort)
    {
        BridgeRdpPort = bridgeRdpPort;
    }

    public int BridgeRdpPort { get; }
}
