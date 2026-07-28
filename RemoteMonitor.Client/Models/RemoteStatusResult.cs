namespace RemoteMonitor.Client.Models;

public sealed class RemoteStatusResult
{
    public RemotePcInfo RemotePc { get; init; } = new();

    public RemoteStatusResponse Status { get; init; } = new();
}
