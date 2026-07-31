using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.ComponentModel;
using System.Diagnostics;

namespace RemoteMonitor.Shared;

internal sealed class WindowsShortcutService
{
    private readonly string shortcutName;
    private readonly string targetPath;
    private readonly string description;

    public WindowsShortcutService(string shortcutName, string targetPath, string description)
    {
        this.shortcutName = shortcutName;
        this.targetPath = targetPath;
        this.description = description;
    }

    public bool DesktopShortcutExists =>
        File.Exists(GetUserDesktopShortcutPath()) ||
        File.Exists(GetCommonDesktopShortcutPath());

    public bool StartMenuShortcutExists =>
        File.Exists(GetUserStartMenuShortcutPath()) ||
        File.Exists(GetCommonStartMenuShortcutPath());

    public bool IsDesktopShortcutUserCreated => File.Exists(GetUserDesktopShortcutPath());

    public bool IsStartMenuShortcutUserCreated => File.Exists(GetUserStartMenuShortcutPath());

    public bool CanRemoveDesktopShortcut => DesktopShortcutExists;

    public bool CanRemoveStartMenuShortcut => StartMenuShortcutExists;

    public void CreateDesktopShortcut()
    {
        CreateShortcut(GetUserDesktopShortcutPath());
    }

    public void CreateStartMenuShortcut()
    {
        CreateShortcut(GetUserStartMenuShortcutPath());
    }

    public void RemoveDesktopShortcut()
    {
        File.Delete(GetUserDesktopShortcutPath());
        RemoveCommonShortcut(ShortcutLocation.Desktop);
    }

    public void RemoveStartMenuShortcut()
    {
        var shortcutPath = GetUserStartMenuShortcutPath();
        File.Delete(shortcutPath);
        RemoveEmptyDirectory(Path.GetDirectoryName(shortcutPath));
        RemoveCommonShortcut(ShortcutLocation.StartMenu);
    }

    public static bool TryHandleRemoveCommonShortcutCommand(
        string[] args,
        string shortcutName,
        out int exitCode)
    {
        exitCode = 0;
        if (args.Length != 2 ||
            !string.Equals(args[0], "--remove-common-shortcut", StringComparison.OrdinalIgnoreCase) ||
            !Enum.TryParse(args[1], true, out ShortcutLocation location))
        {
            return false;
        }

        try
        {
            var shortcutPath = GetCommonShortcutPath(shortcutName, location);
            File.Delete(shortcutPath);
            RemoveEmptyDirectory(Path.GetDirectoryName(shortcutPath));
        }
        catch
        {
            exitCode = 1;
        }

        return true;
    }

    private void RemoveCommonShortcut(ShortcutLocation location)
    {
        var shortcutPath = GetCommonShortcutPath(shortcutName, location);
        if (!File.Exists(shortcutPath))
        {
            return;
        }

        try
        {
            File.Delete(shortcutPath);
            RemoveEmptyDirectory(Path.GetDirectoryName(shortcutPath));
            return;
        }
        catch (UnauthorizedAccessException)
        {
            // Continue by launching the same application with administrator rights.
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = targetPath,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("--remove-common-shortcut");
        startInfo.ArgumentList.Add(location.ToString());

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("관리자 권한 삭제 프로세스를 시작하지 못했습니다.");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("공용 바로가기를 삭제하지 못했습니다.");
            }
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("관리자 권한 요청이 취소되었습니다.", exception);
        }
    }

    private static void RemoveEmptyDirectory(string? directory)
    {
        if (directory is not null &&
            Directory.Exists(directory) &&
            !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }

    private void CreateShortcut(string shortcutPath)
    {
        var directory = Path.GetDirectoryName(shortcutPath)
            ?? throw new InvalidOperationException("바로가기 폴더를 확인할 수 없습니다.");
        Directory.CreateDirectory(directory);

        var shellLink = (IShellLinkW)(object)new ShellLink();
        try
        {
            shellLink.SetPath(targetPath);
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(targetPath) ?? AppContext.BaseDirectory);
            shellLink.SetDescription(description);
            shellLink.SetIconLocation(targetPath, 0);

            var persistFile = (IPersistFile)shellLink;
            persistFile.Save(shortcutPath, true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }

    private string GetUserDesktopShortcutPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"{shortcutName}.lnk");
    }

    private string GetCommonDesktopShortcutPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            $"{shortcutName}.lnk");
    }

    private string GetUserStartMenuShortcutPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            "Remote Monitor",
            $"{shortcutName}.lnk");
    }

    private string GetCommonStartMenuShortcutPath()
    {
        return GetCommonShortcutPath(shortcutName, ShortcutLocation.StartMenu);
    }

    private static string GetCommonShortcutPath(string name, ShortcutLocation location)
    {
        return location switch
        {
            ShortcutLocation.Desktop => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                $"{name}.lnk"),
            ShortcutLocation.StartMenu => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                "Remote Monitor",
                $"{name}.lnk"),
            _ => throw new ArgumentOutOfRangeException(nameof(location))
        };
    }

    private enum ShortcutLocation
    {
        Desktop,
        StartMenu
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink;

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder filePath,
            int maximumPathLength,
            IntPtr findData,
            uint flags);

        void GetIDList(out IntPtr itemIdList);
        void SetIDList(IntPtr itemIdList);

        void GetDescription(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description,
            int maximumDescriptionLength);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);

        void GetWorkingDirectory(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder workingDirectory,
            int maximumPathLength);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string workingDirectory);

        void GetArguments(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments,
            int maximumArgumentsLength);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);

        void GetIconLocation(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath,
            int iconPathLength,
            out int iconIndex);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr windowHandle, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
