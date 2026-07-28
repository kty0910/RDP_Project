namespace RemoteMonitor.Server.Utilities;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex mutex;
    private bool hasHandle;

    public SingleInstanceGuard(string name)
    {
        mutex = new Mutex(false, $@"Global\{name}");
    }

    public bool TryAcquire()
    {
        hasHandle = mutex.WaitOne(TimeSpan.Zero);
        return hasHandle;
    }

    public void Dispose()
    {
        if (hasHandle)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
    }
}
