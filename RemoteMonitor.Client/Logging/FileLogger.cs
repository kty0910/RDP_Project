namespace RemoteMonitor.Client.Logging;

public sealed class FileLogger
{
    private readonly string logDirectory;

    public FileLogger(string logDirectory)
    {
        this.logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
    }

    public void Info(string message)
    {
        var filePath = Path.Combine(logDirectory, $"{DateTime.Now:yyyy-MM-dd}.txt");
        File.AppendAllText(filePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [INFO] {message}{Environment.NewLine}");
    }
}
