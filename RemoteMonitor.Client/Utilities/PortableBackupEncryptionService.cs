using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RemoteMonitor.Client.Utilities;

public sealed class PortableBackupEncryptionService
{
    private const int SaltSize = 16;
    private const int IvSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 210000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string Encrypt(string plainText, string password)
    {
        ValidatePassword(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var key = DeriveKey(password, salt);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        var hmac = ComputeHmac(key, salt, iv, cipherBytes);

        var package = new BackupPackage
        {
            Version = 1,
            Iterations = Iterations,
            Salt = Convert.ToBase64String(salt),
            Iv = Convert.ToBase64String(iv),
            Data = Convert.ToBase64String(cipherBytes),
            Hmac = Convert.ToBase64String(hmac)
        };

        return JsonSerializer.Serialize(package, JsonOptions);
    }

    public string Decrypt(string cipherText, string password)
    {
        ValidatePassword(password);

        BackupPackage package;

        try
        {
            package = JsonSerializer.Deserialize<BackupPackage>(cipherText, JsonOptions)
                ?? throw new InvalidOperationException("백업 파일 형식이 올바르지 않습니다.");

            var salt = Convert.FromBase64String(package.Salt);
            var iv = Convert.FromBase64String(package.Iv);
            var cipherBytes = Convert.FromBase64String(package.Data);
            var expectedHmac = Convert.FromBase64String(package.Hmac);
            var key = DeriveKey(password, salt, package.Iterations);
            var actualHmac = ComputeHmac(key, salt, iv, cipherBytes);

            if (!CryptographicOperations.FixedTimeEquals(expectedHmac, actualHmac))
            {
                throw new InvalidOperationException("비밀번호를 다시 확인해 주세요.");
            }

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("비밀번호를 다시 확인해 주세요.");
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations = Iterations)
    {
        using var deriveBytes = new Rfc2898DeriveBytes(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256);
        return deriveBytes.GetBytes(KeySize);
    }

    private static byte[] ComputeHmac(byte[] key, byte[] salt, byte[] iv, byte[] cipherBytes)
    {
        using var hmac = new HMACSHA256(key);
        hmac.TransformBlock(salt, 0, salt.Length, null, 0);
        hmac.TransformBlock(iv, 0, iv.Length, null, 0);
        hmac.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return hmac.Hash ?? throw new InvalidOperationException("HMAC 생성에 실패했습니다.");
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("백업 비밀번호를 입력해 주세요.");
        }
    }

    private sealed class BackupPackage
    {
        public int Version { get; init; }

        public int Iterations { get; init; }

        public string Salt { get; init; } = string.Empty;

        public string Iv { get; init; } = string.Empty;

        public string Data { get; init; } = string.Empty;

        public string Hmac { get; init; } = string.Empty;
    }
}
