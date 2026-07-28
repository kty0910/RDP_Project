using Microsoft.Win32;

namespace RemoteMonitor.Server.Services;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "RemoteMonitor.Server";

    public bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppName) is string value && value.Equals(GetExecutablePath(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetRegistered(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(AppName, GetExecutablePath());
            return;
        }

        key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    private static string GetExecutablePath()
    {
        return Application.ExecutablePath;
    }
}
