using System.Runtime.InteropServices;
using System.Text;

namespace RemoteMonitor.Client.Services;

internal static class WindowsRdpCredentialService
{
    private const uint CredTypeGeneric = 1;
    private const uint CredTypeDomainPassword = 2;
    private const uint CredPersistLocalMachine = 2;

    public static void SetPersistentCredential(
        string endpoint,
        string userName,
        string password)
    {
        if (string.IsNullOrWhiteSpace(endpoint)
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrEmpty(password))
        {
            return;
        }

        try
        {
            var targetNames = GetCredentialTargetNames(endpoint).ToArray();
            foreach (var targetName in targetNames)
            {
                CredDelete(targetName, CredTypeDomainPassword, 0);
                CredDelete(targetName, CredTypeGeneric, 0);
            }

            WriteCredential(
                $"TERMSRV/{ExtractHost(endpoint.Trim())}",
                userName.Trim(),
                password);
        }
        catch
        {
            // Credential Manager access must not block an RDP connection attempt.
        }
    }

    private static void WriteCredential(string targetName, string userName, string password)
    {
        var targetNamePointer = IntPtr.Zero;
        var userNamePointer = IntPtr.Zero;
        var passwordPointer = IntPtr.Zero;

        try
        {
            targetNamePointer = Marshal.StringToCoTaskMemUni(targetName);
            userNamePointer = Marshal.StringToCoTaskMemUni(userName);
            passwordPointer = Marshal.StringToCoTaskMemUni(password);

            var credential = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = targetNamePointer,
                CredentialBlobSize = (uint)Encoding.Unicode.GetByteCount(password),
                CredentialBlob = passwordPointer,
                Persist = CredPersistLocalMachine,
                UserName = userNamePointer
            };

            CredWrite(ref credential, 0);
        }
        finally
        {
            if (passwordPointer != IntPtr.Zero)
            {
                Marshal.ZeroFreeCoTaskMemUnicode(passwordPointer);
            }

            if (userNamePointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(userNamePointer);
            }

            if (targetNamePointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(targetNamePointer);
            }
        }
    }

    private static IEnumerable<string> GetCredentialTargetNames(string endpoint)
    {
        var trimmedEndpoint = endpoint.Trim();
        var host = ExtractHost(trimmedEndpoint);
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"TERMSRV/{trimmedEndpoint}",
            $"TERMSRV/{host}"
        };
    }

    private static string ExtractHost(string endpoint)
    {
        if (endpoint.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracket = endpoint.IndexOf(']');
            if (closingBracket > 1)
            {
                return endpoint[1..closingBracket];
            }
        }

        var lastColon = endpoint.LastIndexOf(':');
        if (lastColon > 0
            && endpoint.IndexOf(':') == lastColon
            && int.TryParse(endpoint[(lastColon + 1)..], out _))
        {
            return endpoint[..lastColon];
        }

        return endpoint;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, int flags);
}
