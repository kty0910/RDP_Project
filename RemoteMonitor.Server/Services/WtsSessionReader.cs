using System.Net;
using System.Runtime.InteropServices;
using RemoteMonitor.Server.Models;

namespace RemoteMonitor.Server.Services;

public sealed class WtsSessionReader
{
    public IReadOnlyList<RdpSessionInfo> ReadSessions()
    {
        var sessions = new List<RdpSessionInfo>();

        if (!NativeMethods.WTSEnumerateSessions(
                IntPtr.Zero,
                0,
                1,
                out var sessionInfoPointer,
                out var sessionCount))
        {
            return sessions;
        }

        try
        {
            var dataSize = Marshal.SizeOf<NativeMethods.WtsSessionInfo>();
            var current = sessionInfoPointer;

            for (var index = 0; index < sessionCount; index++)
            {
                var sessionInfo = Marshal.PtrToStructure<NativeMethods.WtsSessionInfo>(current);
                current += dataSize;

                sessions.Add(new RdpSessionInfo
                {
                    SessionId = sessionInfo.SessionId,
                    SessionName = sessionInfo.WinStationName ?? string.Empty,
                    State = sessionInfo.State.ToString(),
                    UserName = QueryString(sessionInfo.SessionId, NativeMethods.WtsInfoClass.WTSUserName),
                    ClientName = QueryString(sessionInfo.SessionId, NativeMethods.WtsInfoClass.WTSClientName),
                    ClientAddress = QueryClientAddress(sessionInfo.SessionId),
                    ClientProtocolType = QueryClientProtocolType(sessionInfo.SessionId),
                    Source = "wts"
                });
            }
        }
        finally
        {
            NativeMethods.WTSFreeMemory(sessionInfoPointer);
        }

        return sessions;
    }

    private static string QueryString(int sessionId, NativeMethods.WtsInfoClass infoClass)
    {
        if (!NativeMethods.WTSQuerySessionInformation(
                IntPtr.Zero,
                sessionId,
                infoClass,
                out var buffer,
                out _))
        {
            return string.Empty;
        }

        try
        {
            return Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    private static string QueryClientAddress(int sessionId)
    {
        if (!NativeMethods.WTSQuerySessionInformation(
                IntPtr.Zero,
                sessionId,
                NativeMethods.WtsInfoClass.WTSClientAddress,
                out var buffer,
                out _))
        {
            return string.Empty;
        }

        try
        {
            var address = Marshal.PtrToStructure<NativeMethods.WtsClientAddress>(buffer);

            if (address.AddressFamily != 2)
            {
                return string.Empty;
            }

            return new IPAddress(address.Address.Skip(2).Take(4).ToArray()).ToString();
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    private static int QueryClientProtocolType(int sessionId)
    {
        if (!NativeMethods.WTSQuerySessionInformation(
                IntPtr.Zero,
                sessionId,
                NativeMethods.WtsInfoClass.WTSClientProtocolType,
                out var buffer,
                out _))
        {
            return 0;
        }

        try
        {
            return Marshal.ReadInt16(buffer);
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    private static class NativeMethods
    {
        [DllImport("wtsapi32.dll", SetLastError = true)]
        internal static extern bool WTSEnumerateSessions(
            IntPtr serverHandle,
            int reserved,
            int version,
            out IntPtr sessionInfo,
            out int count);

        [DllImport("wtsapi32.dll")]
        internal static extern void WTSFreeMemory(IntPtr memory);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        internal static extern bool WTSQuerySessionInformation(
            IntPtr serverHandle,
            int sessionId,
            WtsInfoClass wtsInfoClass,
            out IntPtr buffer,
            out int bytesReturned);

        [StructLayout(LayoutKind.Sequential)]
        internal struct WtsSessionInfo
        {
            public int SessionId;

            [MarshalAs(UnmanagedType.LPStr)]
            public string? WinStationName;

            public WtsConnectState State;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct WtsClientAddress
        {
            public int AddressFamily;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] Address;
        }

        internal enum WtsInfoClass
        {
            WTSUserName = 5,
            WTSClientName = 10,
            WTSClientProtocolType = 16,
            WTSClientAddress = 14
        }

        internal enum WtsConnectState
        {
            Active,
            Connected,
            ConnectQuery,
            Shadow,
            Disconnected,
            Idle,
            Listen,
            Reset,
            Down,
            Init
        }
    }
}
