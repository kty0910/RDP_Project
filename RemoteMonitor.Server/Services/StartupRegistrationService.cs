using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace RemoteMonitor.Server.Services;

public sealed class StartupRegistrationService
{
    private const string ConfigureArgument = "--configure-startup";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "RemoteMonitorServerUI";
    private const string LegacyCurrentUserValueName = "RemoteMonitor.Server";
    private const string ServiceName = "RemoteMonitor.Server.Service";
    private const string ServiceDisplayName = "Remote Monitor Server Service";
    private const string ServiceRegistryPath = @"SYSTEM\CurrentControlSet\Services\RemoteMonitor.Server.Service";

    public bool IsRegistered()
    {
        return HasUiRegistration() || IsServiceAutomatic();
    }

    public async Task SetRegisteredAsync(bool enabled)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Application.ExecutablePath,
            Arguments = $"{ConfigureArgument} {(enabled ? "enable" : "disable")}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("관리자 권한 요청을 시작할 수 없습니다.");
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    enabled
                        ? "Server 자동 실행을 설정하지 못했습니다."
                        : "Server 자동 실행을 해제하지 못했습니다.");
            }

            DeleteLegacyCurrentUserRegistration();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("관리자 권한 요청이 취소되었습니다.", exception);
        }
    }

    public static bool TryHandleConfigurationCommand(string[] args, out int exitCode)
    {
        exitCode = 0;
        var argumentIndex = Array.FindIndex(
            args,
            argument => string.Equals(argument, ConfigureArgument, StringComparison.OrdinalIgnoreCase));

        if (argumentIndex < 0)
        {
            return false;
        }

        try
        {
            if (argumentIndex + 1 >= args.Length)
            {
                throw new ArgumentException("자동 실행 설정 값이 없습니다.");
            }

            var enabled = args[argumentIndex + 1] switch
            {
                "enable" => true,
                "disable" => false,
                _ => throw new ArgumentException("자동 실행 설정 값이 올바르지 않습니다.")
            };

            ConfigureMachineStartup(enabled);
        }
        catch
        {
            exitCode = 1;
        }

        return true;
    }

    private static bool HasUiRegistration()
    {
        using var machineRunKey = Registry.LocalMachine.OpenSubKey(RunKeyPath, writable: false);
        if (machineRunKey?.GetValue(RunValueName) is string machineValue &&
            !string.IsNullOrWhiteSpace(machineValue))
        {
            return true;
        }

        using var userRunKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return userRunKey?.GetValue(LegacyCurrentUserValueName) is string userValue &&
               !string.IsNullOrWhiteSpace(userValue);
    }

    private static bool IsServiceAutomatic()
    {
        using var serviceKey = Registry.LocalMachine.OpenSubKey(ServiceRegistryPath, writable: false);
        return serviceKey?.GetValue("Start") is int startValue && startValue == 2;
    }

    private static void ConfigureMachineStartup(bool enabled)
    {
        if (enabled)
        {
            EnableService();

            using var runKey = Registry.LocalMachine.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("Server UI 자동 실행 설정을 열 수 없습니다.");
            runKey.SetValue(
                RunValueName,
                $"\"{GetServerExecutablePath()}\" --tray",
                RegistryValueKind.String);
            return;
        }

        using (var runKey = Registry.LocalMachine.OpenSubKey(RunKeyPath, writable: true))
        {
            runKey?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }

        RunServiceControl(["stop", ServiceName], requireSuccess: false);
        RunServiceControl(["delete", ServiceName], requireSuccess: false);
    }

    private static void EnableService()
    {
        var serviceExecutablePath = Path.Combine(AppContext.BaseDirectory, "RemoteMonitor.Server.Service.exe");
        if (!File.Exists(serviceExecutablePath))
        {
            throw new FileNotFoundException(
                "Server Service 실행 파일을 찾을 수 없습니다.",
                serviceExecutablePath);
        }

        var serviceExists = RunServiceControl(["query", ServiceName], requireSuccess: false) == 0;
        var command = serviceExists ? "config" : "create";
        RunServiceControl(
            [
                command,
                ServiceName,
                "binPath=",
                serviceExecutablePath,
                "start=",
                "auto",
                "DisplayName=",
                ServiceDisplayName
            ],
            requireSuccess: true);

        RunServiceControl(["start", ServiceName], requireSuccess: false);
    }

    private static int RunServiceControl(IEnumerable<string> arguments, bool requireSuccess)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "sc.exe"),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows Service 설정을 시작할 수 없습니다.");
        process.WaitForExit();

        if (requireSuccess && process.ExitCode != 0)
        {
            throw new InvalidOperationException("Windows Service 설정에 실패했습니다.");
        }

        return process.ExitCode;
    }

    private static void DeleteLegacyCurrentUserRegistration()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        runKey?.DeleteValue(LegacyCurrentUserValueName, throwOnMissingValue: false);
    }

    private static string GetServerExecutablePath()
    {
        return Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "RemoteMonitor.Server.exe");
    }
}
