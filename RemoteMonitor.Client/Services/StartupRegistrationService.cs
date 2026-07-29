using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace RemoteMonitor.Client.Services;

internal static class StartupRegistrationService
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "RemoteMonitorClient";

    public static bool IsEnabled()
    {
        return HasRegistration(RegistryHive.CurrentUser) ||
               HasRegistration(RegistryHive.LocalMachine);
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            if (!HasRegistration(RegistryHive.LocalMachine))
            {
                using var runKey = Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true)
                    ?? throw new InvalidOperationException("자동 실행 설정을 열 수 없습니다.");

                runKey.SetValue(
                    ValueName,
                    $"\"{Application.ExecutablePath}\" --tray",
                    RegistryValueKind.String);
            }

            return;
        }

        using (var runKey = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true))
        {
            runKey?.DeleteValue(ValueName, throwOnMissingValue: false);
        }

        if (HasRegistration(RegistryHive.LocalMachine))
        {
            DeleteMachineRegistration();
        }
    }

    private static bool HasRegistration(RegistryHive hive)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var runKey = baseKey.OpenSubKey(RunSubKey, writable: false);
        return runKey?.GetValue(ValueName) is string value &&
               !string.IsNullOrWhiteSpace(value);
    }

    private static void DeleteMachineRegistration()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var runKey = baseKey.OpenSubKey(RunSubKey, writable: true);
            runKey?.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }
        catch (UnauthorizedAccessException)
        {
            // 설치 프로그램이 등록한 시스템 자동 실행 항목은 관리자 권한으로 제거한다.
        }

        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(systemDirectory, "reg.exe"),
            Arguments = $"delete \"HKLM\\{RunSubKey}\" /v \"{ValueName}\" /f",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("관리자 권한 요청을 시작할 수 없습니다.");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException("시스템 자동 실행 설정을 해제하지 못했습니다.");
            }
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException("관리자 권한 요청이 취소되었습니다.", exception);
        }
    }
}
