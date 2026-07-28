namespace RemoteMonitor.Client.Models;

public sealed class PollingStateChangedEventArgs : EventArgs
{
    public PollingStateChangedEventArgs(RemotePcInfo? remotePc, bool isRunning)
    {
        RemotePc = remotePc;
        IsRunning = isRunning;
    }

    public RemotePcInfo? RemotePc { get; }

    public bool IsRunning { get; }
}
