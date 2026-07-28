namespace RemoteMonitor.Client.Config;

public sealed class ClientOptions
{
    public string DataDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "Data");

    public string RemotePcListFileName { get; init; } = "remote_pc_list.dat";

    public string RemotePcListPath => Path.Combine(DataDirectory, RemotePcListFileName);

    public static ClientOptions Default { get; } = new();
}
