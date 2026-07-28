namespace RemoteMonitor.Client.Models;

public sealed class HealthResponse
{
    public string Status { get; init; } = string.Empty;

    public DateTime ServerTime { get; init; }
}
