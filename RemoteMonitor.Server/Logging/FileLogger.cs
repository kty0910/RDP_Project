namespace RemoteMonitor.Server.Logging;

public sealed class FileLogger
{
    private readonly string logDirectory;

    public FileLogger(string logDirectory)
    {
        this.logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? exception = null)
    {
        var detail = exception is null ? message : $"{message} {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", detail);
    }

    private void Write(string level, string message)
    {
        var filePath = Path.Combine(logDirectory, $"Server_{DateTime.Now:yyyyMMdd}.txt");
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
        File.AppendAllText(filePath, line);
    }
}
