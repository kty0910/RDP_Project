namespace RemoteMonitor.Server.Bridge;

public sealed class BridgeTarget
{
    public string Name { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public string DescriptionSummary { get; init; } = string.Empty;

    public string DescriptionDetails { get; init; } = string.Empty;

    public string DescriptionDetailsRtf { get; init; } = string.Empty;

    public int ApiPort { get; init; } = 5000;

    public int RdpPort { get; init; } = 3389;

    public static string CreateName(string host, int apiPort, int rdpPort)
    {
        return $"{host}:{apiPort}:{rdpPort}";
    }
}
