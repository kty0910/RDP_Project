using System.Text.Json;
using RemoteMonitor.Client.Config;
using RemoteMonitor.Client.Models;
using RemoteMonitor.Client.Utilities;

namespace RemoteMonitor.Client.Services;

public sealed class RemotePcListService
{
    private const string EncryptedPrefix = "RDPENC1:";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ClientOptions options;
    private readonly AesEncryptionService encryptionService = new();
    private readonly PortableBackupEncryptionService backupEncryptionService = new();

    public RemotePcListService(ClientOptions options)
    {
        this.options = options;
        Directory.CreateDirectory(options.DataDirectory);
    }

    public IReadOnlyList<RemotePcInfo> Load()
    {
        if (!File.Exists(options.RemotePcListPath))
        {
            return CreateDefaultList();
        }

        try
        {
            var contents = File.ReadAllText(options.RemotePcListPath);
            var json = Decode(contents);
            var remotePcs = JsonSerializer.Deserialize<List<RemotePcInfo>>(json, JsonOptions) ?? new List<RemotePcInfo>();

            if (!contents.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
            {
                Save(remotePcs);
            }

            return remotePcs;
        }
        catch
        {
            BackupUnreadableListFile();
            return CreateDefaultList();
        }
    }

    public void Save(IReadOnlyList<RemotePcInfo> remotePcs)
    {
        var json = JsonSerializer.Serialize(remotePcs, JsonOptions);
        File.WriteAllText(options.RemotePcListPath, $"{EncryptedPrefix}{encryptionService.EncryptToText(json)}");
    }

    public void ExportBackup(string filePath, IReadOnlyList<RemotePcInfo> remotePcs, string password)
    {
        var json = JsonSerializer.Serialize(remotePcs, JsonOptions);
        var encryptedBackup = backupEncryptionService.Encrypt(json, password);
        File.WriteAllText(filePath, encryptedBackup);
    }

    public IReadOnlyList<RemotePcInfo> ImportBackup(string filePath, string password)
    {
        var encryptedBackup = File.ReadAllText(filePath);
        var json = backupEncryptionService.Decrypt(encryptedBackup, password);
        return JsonSerializer.Deserialize<List<RemotePcInfo>>(json, JsonOptions) ?? new List<RemotePcInfo>();
    }

    private IReadOnlyList<RemotePcInfo> CreateDefaultList()
    {
        var sample = new[]
        {
            new RemotePcInfo { Name = "Server PC", Host = "127.0.0.1", Port = 5000 }
        };
        Save(sample);
        return sample;
    }

    private void BackupUnreadableListFile()
    {
        try
        {
            if (!File.Exists(options.RemotePcListPath))
            {
                return;
            }

            var backupPath = Path.Combine(
                options.DataDirectory,
                $"{options.RemotePcListFileName}.invalid_{DateTime.Now:yyyyMMdd_HHmmss}");
            File.Move(options.RemotePcListPath, backupPath, overwrite: true);
        }
        catch
        {
            // If backup fails, overwrite the unreadable file with a clean default list.
        }
    }

    private string Decode(string contents)
    {
        if (!contents.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            return contents;
        }

        return encryptionService.DecryptFromText(contents[EncryptedPrefix.Length..]);
    }
}