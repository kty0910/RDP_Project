namespace RemoteMonitor.Client.Models;

public sealed class RemotePcInfo
{
    public string Name { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string DescriptionSummary { get; init; } = string.Empty;

    public string DescriptionDetails { get; init; } = string.Empty;

    public string DescriptionDetailsRtf { get; init; } = string.Empty;

    public int Port { get; init; } = 5000;

    public int RdpPort { get; init; } = 3389;

    public bool UseBridge { get; init; }

    public string BridgeHost { get; init; } = string.Empty;

    public int BridgeApiPort { get; init; } = 5000;

    public string BridgeToken { get; init; } = string.Empty;

    public string ApiBaseUrl => $"http://{Host}:{Port}";

    public string BridgeApiBaseUrl => $"http://{BridgeHost}:{BridgeApiPort}";

    public string RdpEndpoint => RdpPort == 3389 ? Host : $"{Host}:{RdpPort}";

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Host : $"{Name} ({Host})";
}
