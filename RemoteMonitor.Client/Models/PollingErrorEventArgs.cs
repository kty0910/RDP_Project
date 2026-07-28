namespace RemoteMonitor.Client.Models;

public sealed class PollingErrorEventArgs : EventArgs
{
    public PollingErrorEventArgs(RemotePcInfo remotePc, Exception exception)
    {
        RemotePc = remotePc;
        Exception = exception;
    }

    public RemotePcInfo RemotePc { get; }

    public Exception Exception { get; }
}
