using RemoteMonitor.Client.Models;
using RemoteMonitor.Client.Networking;

namespace RemoteMonitor.Client.Services;

public sealed class RemoteStatusPollingService : IDisposable
{
    private readonly RemoteMonitorApiClient apiClient;
    private readonly TimeSpan interval = TimeSpan.FromSeconds(1);
    private CancellationTokenSource? cancellationTokenSource;
    private RemotePcInfo? currentRemotePc;
    private bool isRunning;

    public event EventHandler<RemoteStatusResult>? StatusChanged;

    public event EventHandler<PollingErrorEventArgs>? PollingFailed;

    public event EventHandler<PollingStateChangedEventArgs>? PollingStateChanged;

    public RemoteStatusPollingService(RemoteMonitorApiClient apiClient)
    {
        this.apiClient = apiClient;
    }

    public async Task StartAsync(RemotePcInfo remotePc)
    {
        Stop();
        cancellationTokenSource = new CancellationTokenSource();
        currentRemotePc = remotePc;
        SetRunning(true);

        try
        {
            await apiClient.GetHealthAsync(remotePc, cancellationTokenSource.Token);
            await PollOnceAsync(remotePc, cancellationTokenSource.Token);
            _ = PollLoopAsync(remotePc, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            SetRunning(false);
        }
        catch (Exception exception)
        {
            PollingFailed?.Invoke(this, new PollingErrorEventArgs(remotePc, exception));
            _ = PollLoopAsync(remotePc, cancellationTokenSource.Token);
        }
    }

    public void Stop()
    {
        cancellationTokenSource?.Cancel();
        SetRunning(false);
    }

    public void Dispose()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        SetRunning(false);
    }

    private async Task PollLoopAsync(RemotePcInfo remotePc, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await PollOnceAsync(remotePc, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Polling was restarted or the form was closed.
        }
        finally
        {
            if (ReferenceEquals(currentRemotePc, remotePc))
            {
                SetRunning(false);
            }
        }
    }

    private async Task PollOnceAsync(RemotePcInfo remotePc, CancellationToken cancellationToken)
    {
        try
        {
            var status = await apiClient.GetStatusAsync(remotePc, cancellationToken);
            StatusChanged?.Invoke(this, new RemoteStatusResult
            {
                RemotePc = remotePc,
                Status = status
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            PollingFailed?.Invoke(this, new PollingErrorEventArgs(remotePc, exception));
        }
    }

    private void SetRunning(bool running)
    {
        if (isRunning == running)
        {
            return;
        }

        isRunning = running;
        PollingStateChanged?.Invoke(this, new PollingStateChangedEventArgs(currentRemotePc, isRunning));
    }
}
