using System.Diagnostics;
using System.Text;
using RemoteMonitor.Client.Models;

namespace RemoteMonitor.Client.Services;

public sealed class RdpConnectionService
{
    public void Connect(RemotePcInfo remotePc, string? endpointOverride = null)
    {
        if (string.IsNullOrWhiteSpace(remotePc.Host))
        {
            throw new InvalidOperationException("Remote PC host is empty.");
        }

        var endpoint = string.IsNullOrWhiteSpace(endpointOverride) ? remotePc.RdpEndpoint : endpointOverride;

        WindowsRdpCredentialService.SetPersistentCredential(
            endpoint,
            remotePc.UserId,
            remotePc.Password);

        if (!string.IsNullOrWhiteSpace(remotePc.Password))
        {
            Clipboard.SetText(remotePc.Password);
        }

        var rdpFilePath = CreateTemporaryRdpFile(remotePc, endpoint);
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "mstsc.exe",
            Arguments = $"\"{rdpFilePath}\"",
            UseShellExecute = true
        });

        if (process is null)
        {
            throw new InvalidOperationException("Failed to start mstsc.exe.");
        }
    }

    private static string CreateTemporaryRdpFile(RemotePcInfo remotePc, string endpoint)
    {
        var fileName = $"{SanitizeFileName(endpoint)}.rdp";
        var filePath = Path.Combine(Path.GetTempPath(), fileName);
        var lines = new List<string>
        {
            "screen mode id:i:2",
            "use multimon:i:0",
            "desktopwidth:i:1920",
            "desktopheight:i:1080",
            "session bpp:i:32",
            "prompt for credentials:i:0",
            "disablepasswordsaving:i:0",
            "authentication level:i:2",
            "enablecredsspsupport:i:1",
            "redirectclipboard:i:1",
            "redirectsmartcards:i:0",
            "redirectwebauthn:i:0",
            "redirectprinters:i:0",
            "redirectcomports:i:0",
            $"full address:s:{endpoint}"
        };

        if (!string.IsNullOrWhiteSpace(remotePc.UserId))
        {
            lines.Add($"username:s:{remotePc.UserId.Trim()}");
        }

        File.WriteAllLines(filePath, lines, Encoding.Unicode);
        return filePath;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(invalidChars.Contains(character) ? '_' : character);
        }

        return builder.Length == 0 ? "RemoteDesktop" : builder.ToString();
    }
}
