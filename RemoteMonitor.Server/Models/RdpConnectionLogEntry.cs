using System.ComponentModel;

namespace RemoteMonitor.Server.Models;

public sealed class RdpConnectionLogEntry : INotifyPropertyChanged
{
    private DateTime? endedAt;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SessionId { get; init; }

    public string IpAddress { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public DateTime StartedAt { get; init; }

    public DateTime? EndedAt
    {
        get => endedAt;
        set
        {
            if (endedAt == value)
            {
                return;
            }

            endedAt = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndedAt)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndedAtText)));
        }
    }

    public string StartedAtText => StartedAt.ToString("yyyy-MM-dd HH:mm:ss");

    public string EndedAtText => EndedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
}
