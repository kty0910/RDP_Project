using System.Text.Json;

namespace RemoteMonitor.Server.Config;

public sealed class ServerOptions
{
    private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, "server_settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public int Port { get; init; } = 5000;

    public string LogDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "Logs");

    public string UrlPrefix => $"http://+:{Port}/";

    public static ServerOptions Default => Load();

    public static ServerOptions Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new ServerOptions();
            }

            var json = File.ReadAllText(SettingsPath);
            var options = JsonSerializer.Deserialize<ServerOptions>(json, JsonOptions) ?? new ServerOptions();
            return Normalize(options);
        }
        catch
        {
            return new ServerOptions();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppContext.BaseDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Normalize(this), JsonOptions));
    }

    public ServerOptions WithPort(int port) => Normalize(new ServerOptions
    {
        Port = port,
        LogDirectory = LogDirectory
    });

    private static ServerOptions Normalize(ServerOptions options)
    {
        return new ServerOptions
        {
            Port = options.Port is >= 1 and <= 65535 ? options.Port : 5000,
            LogDirectory = string.IsNullOrWhiteSpace(options.LogDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "Logs")
                : options.LogDirectory
        };
    }
}