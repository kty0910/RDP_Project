using RemoteMonitor.Server.Logging;
using RemoteMonitor.Server.Models;

namespace RemoteMonitor.Server.Services;

public sealed class RdpSessionService
{
    private readonly FileLogger logger;
    private readonly WtsSessionReader wtsSessionReader = new();
    private readonly object statusLock = new();
    private ServerStatusResponse latestStatus = new() { CheckedAt = DateTime.MinValue };

    public RdpSessionService(FileLogger logger)
    {
        this.logger = logger;
    }

    public Task<ServerStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        lock (statusLock)
        {
            return Task.FromResult(latestStatus);
        }
    }

    public Task<ServerStatusResponse> RefreshStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var wtsSessions = wtsSessionReader.ReadSessions();
            var activeRdpSessions = wtsSessions
                .Where(session => session.IsActive && session.IsRemoteDesktop)
                .ToArray();

            var refreshedStatus = new ServerStatusResponse
            {
                CheckedAt = DateTime.Now,
                HasActiveRdpSession = activeRdpSessions.Length > 0,
                ActiveRdpSessionCount = activeRdpSessions.Length,
                Sessions = wtsSessions
            };

            lock (statusLock)
            {
                latestStatus = refreshedStatus;
            }

            return Task.FromResult(refreshedStatus);
        }
        catch (Exception exception)
        {
            logger.Error("Failed to read RDP sessions.", exception);

            var emptyStatus = new ServerStatusResponse
            {
                CheckedAt = DateTime.Now,
                Sessions = Array.Empty<RdpSessionInfo>()
            };

            lock (statusLock)
            {
                latestStatus = emptyStatus;
            }

            return Task.FromResult(emptyStatus);
        }
    }
}
