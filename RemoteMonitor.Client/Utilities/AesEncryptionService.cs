using System.Security.Cryptography;
using System.Text;

namespace RemoteMonitor.Client.Utilities;

public sealed class AesEncryptionService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RemoteMonitor.Client.RemotePcList.v1");

    public string EncryptToText(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(Encrypt(plainBytes));
    }

    public string DecryptFromText(string cipherText)
    {
        var cipherBytes = Convert.FromBase64String(cipherText);
        return Encoding.UTF8.GetString(Decrypt(cipherBytes));
    }

    private byte[] Encrypt(byte[] plainBytes)
    {
        return ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
    }

    private byte[] Decrypt(byte[] cipherBytes)
    {
        return ProtectedData.Unprotect(cipherBytes, Entropy, DataProtectionScope.CurrentUser);
    }
}
