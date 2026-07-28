namespace RemoteMonitor.Server.Models;

public sealed class HealthResponse
{
    public string Status { get; init; } = "OK";

    public DateTime ServerTime { get; init; } = DateTime.Now;
}
