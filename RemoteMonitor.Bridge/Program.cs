using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

var options = BridgeOptions.Load(args);
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://{options.BindAddress}:{options.ApiPort}");
var app = builder.Build();
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
var forwarders = new RdpForwarderManager(options);
var localSessionReader = new LocalRdpSessionReader();

app.MapGet("/health", (HttpRequest request) =>
{
    if (!Authorize(request, options))
    {
        return Results.Unauthorized();
    }

    return Results.Json(new
    {
        status = "OK",
        serverTime = DateTime.Now,
        bindAddress = options.BindAddress,
        apiPort = options.ApiPort,
        rdpPortStart = options.RdpPortStart,
        rdpPortEnd = options.RdpPortEnd,
        allowedTargets = options.AllowedTargets.Select(target => target.Name).ToArray()
    });
});

app.MapGet("/status", async (HttpRequest request, string target) =>
{
    if (!Authorize(request, options))
    {
        return Results.Unauthorized();
    }

    if (!options.TryGetTarget(target, out var bridgeTarget))
    {
        return Results.BadRequest(new { error = "Target is not allowed by bridge_settings.json." });
    }

    if (bridgeTarget.IsSelf)
    {
        return Results.Json(localSessionReader.GetStatus());
    }

    var targetUrl = $"http://{bridgeTarget.Host}:{bridgeTarget.ApiPort}/status";

    try
    {
        using var response = await httpClient.GetAsync(targetUrl, request.HttpContext.RequestAborted);
        var body = await response.Content.ReadAsStringAsync(request.HttpContext.RequestAborted);
        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }
    catch (Exception exception)
    {
        return Results.Json(new { error = "Status proxy failed.", detail = exception.Message }, statusCode: 502);
    }
});

app.MapPost("/rdp/start", (HttpRequest request, RdpStartRequest startRequest) =>
{
    if (!Authorize(request, options))
    {
        return Results.Unauthorized();
    }

    if (!options.TryGetTarget(startRequest.Target, out var bridgeTarget))
    {
        return Results.BadRequest(new { error = "Target is not allowed by bridge_settings.json." });
    }

    try
    {
        var bridgeRdpPort = forwarders.EnsureForwarder(
            bridgeTarget.IsSelf ? "127.0.0.1" : bridgeTarget.Host,
            bridgeTarget.RdpPort,
            request.HttpContext.RequestAborted);

        return Results.Json(new RdpStartResponse(bridgeRdpPort));
    }
    catch (Exception exception)
    {
        return Results.Json(new { error = "RDP forwarder failed.", detail = exception.Message }, statusCode: 502);
    }
});

Console.WriteLine($"RemoteMonitor.Bridge listening on http://{options.BindAddress}:{options.ApiPort}");
Console.WriteLine($"RDP forward port range: {options.RdpPortStart}-{options.RdpPortEnd}");
Console.WriteLine("Only targets listed in bridge_settings.json are allowed.");
await app.RunAsync();

static bool Authorize(HttpRequest request, BridgeOptions options)
{
    if (string.IsNullOrWhiteSpace(options.Token))
    {
        return false;
    }

    if (request.Headers.TryGetValue("X-Bridge-Token", out var headerToken)
        && string.Equals(headerToken.ToString(), options.Token, StringComparison.Ordinal))
    {
        return true;
    }

    return request.Query.TryGetValue("token", out var queryToken)
        && string.Equals(queryToken.ToString(), options.Token, StringComparison.Ordinal);
}

public sealed record RdpStartRequest(string Target);

public sealed record RdpStartResponse(int BridgeRdpPort);

public sealed class BridgeOptions
{
    public string BindAddress { get; init; } = "127.0.0.1";

    public int ApiPort { get; init; } = 5000;

    public int RdpPortStart { get; init; } = 13389;

    public int RdpPortEnd { get; init; } = 13489;

    public string Token { get; init; } = string.Empty;

    public IReadOnlyList<BridgeTarget> AllowedTargets { get; init; } = Array.Empty<BridgeTarget>();

    public static BridgeOptions Load(string[] args)
    {
        var options = LoadFromFile();
        return new BridgeOptions
        {
            BindAddress = ReadStringArg(args, "--bind", options.BindAddress),
            ApiPort = ReadIntArg(args, "--api-port", options.ApiPort),
            RdpPortStart = ReadIntArg(args, "--rdp-port-start", options.RdpPortStart),
            RdpPortEnd = ReadIntArg(args, "--rdp-port-end", options.RdpPortEnd),
            Token = ReadStringArg(args, "--token", options.Token),
            AllowedTargets = options.AllowedTargets
        };
    }

    public bool TryGetTarget(string name, out BridgeTarget target)
    {
        target = AllowedTargets.FirstOrDefault(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(item.Host)) ?? new BridgeTarget();

        return !string.IsNullOrWhiteSpace(target.Name);
    }

    private static BridgeOptions LoadFromFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "bridge_settings.json");

        if (!File.Exists(path))
        {
            var defaultOptions = new BridgeOptions
            {
                Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),
                AllowedTargets =
                [
                    new BridgeTarget
                    {
                        Name = "bridge-self",
                        Host = "self",
                        ApiPort = 5000,
                        RdpPort = 3389
                    },
                    new BridgeTarget
                    {
                        Name = "sample-target",
                        Host = "192.168.250.3",
                        ApiPort = 5000,
                        RdpPort = 3389
                    }
                ]
            };
            File.WriteAllText(path, JsonSerializer.Serialize(defaultOptions, new JsonSerializerOptions { WriteIndented = true }));
            return defaultOptions;
        }

        var json = File.ReadAllText(path);
        var options = JsonSerializer.Deserialize<BridgeOptions>(json) ?? new BridgeOptions();
        var migratedOptions = NormalizeOptions(options);

        if (migratedOptions.ApiPort != options.ApiPort
            || migratedOptions.AllowedTargets.Count != options.AllowedTargets.Count)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(migratedOptions, new JsonSerializerOptions { WriteIndented = true }));
        }

        return migratedOptions;
    }

    private static BridgeOptions NormalizeOptions(BridgeOptions options)
    {
        var normalizedApiPort = options.ApiPort == 15000 ? 5000 : options.ApiPort;

        if (options.AllowedTargets.Any(target => target.IsSelf))
        {
            return new BridgeOptions
            {
                BindAddress = options.BindAddress,
                ApiPort = normalizedApiPort,
                RdpPortStart = options.RdpPortStart,
                RdpPortEnd = options.RdpPortEnd,
                Token = options.Token,
                AllowedTargets = options.AllowedTargets
            };
        }

        return new BridgeOptions
        {
            BindAddress = options.BindAddress,
            ApiPort = normalizedApiPort,
            RdpPortStart = options.RdpPortStart,
            RdpPortEnd = options.RdpPortEnd,
            Token = options.Token,
            AllowedTargets =
            [
                new BridgeTarget
                {
                    Name = "bridge-self",
                    Host = "self",
                    ApiPort = 5000,
                    RdpPort = 3389
                },
                .. options.AllowedTargets
            ]
        };
    }

    private static int ReadIntArg(string[] args, string name, int defaultValue)
    {
        var value = ReadStringArg(args, name, string.Empty);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static string ReadStringArg(string[] args, string name, string defaultValue)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return defaultValue;
    }
}

public sealed class BridgeTarget
{
    public string Name { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public int ApiPort { get; init; } = 5000;

    public int RdpPort { get; init; } = 3389;

    public bool IsSelf => Host.Equals("self", StringComparison.OrdinalIgnoreCase)
        || Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
}

public sealed class LocalRdpSessionReader
{
    private readonly object statusLock = new();
    private ServerStatusResponse latestStatus = new() { CheckedAt = DateTime.MinValue };

    public ServerStatusResponse GetStatus()
    {
        lock (statusLock)
        {
            if (DateTime.Now - latestStatus.CheckedAt < TimeSpan.FromSeconds(1))
            {
                return latestStatus;
            }

            latestStatus = RefreshStatus();
            return latestStatus;
        }
    }

    private static ServerStatusResponse RefreshStatus()
    {
        var sessions = ReadSessions();
        var activeRdpSessions = sessions
            .Where(session => session.IsActive && session.IsRemoteDesktop)
            .ToArray();

        return new ServerStatusResponse
        {
            CheckedAt = DateTime.Now,
            HasActiveRdpSession = activeRdpSessions.Length > 0,
            ActiveRdpSessionCount = activeRdpSessions.Length,
            Sessions = sessions
        };
    }

    private static IReadOnlyList<RdpSessionInfo> ReadSessions()
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
                    ClientProtocolType = QueryClientProtocolType(sessionInfo.SessionId),
                    Source = "bridge-wts"
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

        internal enum WtsInfoClass
        {
            WTSUserName = 5,
            WTSClientName = 10,
            WTSClientProtocolType = 16
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

public sealed class ServerStatusResponse
{
    public DateTime CheckedAt { get; init; } = DateTime.Now;

    public bool HasActiveRdpSession { get; init; }

    public int ActiveRdpSessionCount { get; init; }

    public IReadOnlyList<RdpSessionInfo> Sessions { get; init; } = Array.Empty<RdpSessionInfo>();
}

public sealed class RdpSessionInfo
{
    public string UserName { get; init; } = string.Empty;

    public string SessionName { get; init; } = string.Empty;

    public int SessionId { get; init; }

    public string State { get; init; } = string.Empty;

    public string IdleTime { get; init; } = string.Empty;

    public string LogonTime { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public int ClientProtocolType { get; init; }

    public string Source { get; init; } = string.Empty;

    public bool IsActive => State.Equals("Active", StringComparison.OrdinalIgnoreCase)
        || State.Equals("\uD65C\uC131", StringComparison.OrdinalIgnoreCase);

    public bool IsRemoteDesktop => ClientProtocolType == 2
        || SessionName.StartsWith("rdp-tcp", StringComparison.OrdinalIgnoreCase);
}

public sealed class RdpForwarderManager
{
    private readonly BridgeOptions options;
    private readonly ConcurrentDictionary<string, RdpForwarder> forwarders = new(StringComparer.OrdinalIgnoreCase);
    private int nextPort;

    public RdpForwarderManager(BridgeOptions options)
    {
        this.options = options;
        nextPort = options.RdpPortStart - 1;
    }

    public int EnsureForwarder(string host, int port, CancellationToken cancellationToken)
    {
        var key = $"{host}:{port}";

        if (forwarders.TryGetValue(key, out var existing))
        {
            return existing.ListenPort;
        }

        for (var attempt = options.RdpPortStart; attempt <= options.RdpPortEnd; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var listenPort = GetNextPort();

            try
            {
                var forwarder = new RdpForwarder(host, port, listenPort);
                forwarder.Start();

                if (forwarders.TryAdd(key, forwarder))
                {
                    return listenPort;
                }

                forwarder.Dispose();
                return forwarders[key].ListenPort;
            }
            catch (SocketException)
            {
                // Try next port.
            }
        }

        throw new InvalidOperationException("No available RDP forwarding port remains.");
    }

    private int GetNextPort()
    {
        var port = Interlocked.Increment(ref nextPort);

        if (port <= options.RdpPortEnd)
        {
            return port;
        }

        Interlocked.Exchange(ref nextPort, options.RdpPortStart);
        return options.RdpPortStart;
    }
}

public sealed class RdpForwarder : IDisposable
{
    private readonly string targetHost;
    private readonly int targetPort;
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellationSource = new();

    public int ListenPort { get; }

    public RdpForwarder(string targetHost, int targetPort, int listenPort)
    {
        this.targetHost = targetHost;
        this.targetPort = targetPort;
        ListenPort = listenPort;
        listener = new TcpListener(IPAddress.Any, listenPort);
    }

    public void Start()
    {
        listener.Start();
        _ = AcceptLoopAsync(cancellationSource.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = ForwardAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Keep accepting future clients.
            }
        }
    }

    private async Task ForwardAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var clientConnection = client;
        using var targetConnection = new TcpClient();
        await targetConnection.ConnectAsync(targetHost, targetPort, cancellationToken);

        await using var clientStream = clientConnection.GetStream();
        await using var targetStream = targetConnection.GetStream();

        var clientToTarget = clientStream.CopyToAsync(targetStream, cancellationToken);
        var targetToClient = targetStream.CopyToAsync(clientStream, cancellationToken);
        await Task.WhenAny(clientToTarget, targetToClient);
    }

    public void Dispose()
    {
        cancellationSource.Cancel();
        listener.Stop();
        cancellationSource.Dispose();
    }
}
