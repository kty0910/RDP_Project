namespace RemoteMonitor.Server.Models;

public sealed class RdpSessionInfo
{
    public string UserName { get; init; } = string.Empty;

    public string SessionName { get; init; } = string.Empty;

    public int SessionId { get; init; }

    public string State { get; init; } = string.Empty;

    public string IdleTime { get; init; } = string.Empty;

    public string LogonTime { get; init; } = string.Empty;

    public string ClientAddress { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public int ClientProtocolType { get; init; }

    public string Source { get; init; } = string.Empty;

    public bool IsActive => State.Equals("Active", StringComparison.OrdinalIgnoreCase)
        || State.Equals("\uD65C\uC131", StringComparison.OrdinalIgnoreCase);

    public bool IsRemoteDesktop => ClientProtocolType == 2
        || SessionName.StartsWith("rdp-tcp", StringComparison.OrdinalIgnoreCase);
}
