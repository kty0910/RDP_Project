namespace RemoteMonitor.Client;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        if (Shared.WindowsShortcutService.TryHandleRemoveCommonShortcutCommand(
                args,
                "Remote Monitor Client",
                out var shortcutExitCode))
        {
            Environment.ExitCode = shortcutExitCode;
            return;
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        using var singleInstance = new Utilities.SingleInstanceGuard("RemoteMonitor.Client");

        if (!singleInstance.TryAcquire())
        {
            MessageBox.Show(
                "RemoteMonitor.Client is already running.",
                "RemoteMonitor.Client",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var startInTray = args.Any(argument =>
            string.Equals(argument, "--tray", StringComparison.OrdinalIgnoreCase));
        Application.Run(new Forms.MainForm(startInTray));
    }
}
