using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using RemoteMonitor.Server.Bridge;
using RemoteMonitor.Server.Config;
using RemoteMonitor.Server.Logging;
using RemoteMonitor.Server.Models;
using RemoteMonitor.Server.Services;

namespace RemoteMonitor.Server.Networking;

public sealed class HttpApiServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

#if STATUS_PUSH_PROTOTYPE
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);
#endif

    private readonly TcpListener listener;
    private readonly RdpSessionService sessionService;
    private readonly BridgeService bridgeService;
    private readonly FileLogger logger;
    private readonly int port;
    private CancellationTokenSource? cancellationTokenSource;
    private bool isStarted;

    public HttpApiServer(
        ServerOptions options,
        RdpSessionService sessionService,
        BridgeService bridgeService,
        FileLogger logger)
    {
        this.sessionService = sessionService;
        this.bridgeService = bridgeService;
        this.logger = logger;
        port = options.Port;
        listener = new TcpListener(IPAddress.Any, port);
    }

    public Task StartAsync()
    {
        if (isStarted)
        {
            return Task.CompletedTask;
        }

        cancellationTokenSource = new CancellationTokenSource();
        listener.Start();
        isStarted = true;
        _ = Task.Run(() => ListenAsync(cancellationTokenSource.Token));
        logger.Info($"HTTP API server started on port {port}.");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        cancellationTokenSource?.Cancel();
        listener.Stop();
        cancellationTokenSource?.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleAsync(client, cancellationToken), cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.Error("HTTP listener error.", exception);
            }
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var clientScope = client;
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, leaveOpen: true);
        await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };

        var requestLine = await reader.ReadLineAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(requestLine))
        {
            return;
        }

        var request = HttpRequestLine.Parse(requestLine);
        var headers = await ReadHeadersAsync(reader, cancellationToken);
        var contentLength = GetContentLength(headers);

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
            && request.Path.Equals("/rdp/start", StringComparison.OrdinalIgnoreCase))
        {
            if (!bridgeService.IsAuthorized(headers, request.Query))
            {
                await WriteJsonAsync(writer, HttpStatusCode.Unauthorized, new { error = "Unauthorized" });
                return;
            }

            var body = await ReadBodyAsync(reader, contentLength, cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                await WriteJsonAsync(writer, HttpStatusCode.BadRequest, new { error = "Request body is empty." });
                return;
            }

            RdpStartRequest startRequest;

            try
            {
                startRequest = JsonSerializer.Deserialize<RdpStartRequest>(body, JsonOptions) ?? new RdpStartRequest();
            }
            catch (JsonException exception)
            {
                logger.Error("Invalid RDP start request body.", exception);
                await WriteJsonAsync(writer, HttpStatusCode.BadRequest, new { error = "Invalid request body." });
                return;
            }

            var result = bridgeService.StartRdpForwarder(startRequest, cancellationToken);
            await WriteContentAsync(writer, result.StatusCode, result.Body, result.ContentType);
            return;
        }

        if (!request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(writer, HttpStatusCode.MethodNotAllowed, new { error = "Method not allowed" });
            return;
        }

        if (request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(writer, HttpStatusCode.OK, new HealthResponse());
            return;
        }

#if STATUS_PUSH_PROTOTYPE
        if (request.Path.Equals("/status/stream", StringComparison.OrdinalIgnoreCase))
        {
            await StreamStatusAsync(writer, headers, cancellationToken);
            return;
        }
#endif

        if (request.Path.Equals("/status", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Query.ContainsKey("host"))
            {
                if (!bridgeService.IsAuthorized(headers, request.Query))
                {
                    await WriteJsonAsync(writer, HttpStatusCode.Unauthorized, new { error = "Unauthorized" });
                    return;
                }

                if (!TryGetBridgeEndpoint(request.Query, out var host, out var apiPort, out var rdpPort))
                {
                    await WriteJsonAsync(writer, HttpStatusCode.BadRequest, new { error = "Invalid bridge endpoint." });
                    return;
                }

                var result = await bridgeService.GetTargetStatusAsync(host, apiPort, rdpPort, cancellationToken);
                await WriteContentAsync(writer, result.StatusCode, result.Body, result.ContentType);
                return;
            }

            var status = await sessionService.GetStatusAsync(cancellationToken);
            await WriteJsonAsync(writer, HttpStatusCode.OK, status);
            return;
        }

        if (request.Path.Equals("/bridge/status", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(writer, HttpStatusCode.OK, bridgeService.GetStatus());
            return;
        }

        if (request.Path.Equals("/sessions", StringComparison.OrdinalIgnoreCase))
        {
            var status = await sessionService.GetStatusAsync(cancellationToken);
            await WriteJsonAsync(writer, HttpStatusCode.OK, status.Sessions);
            return;
        }

        await WriteJsonAsync(writer, HttpStatusCode.NotFound, new { error = "Not found" });
    }

#if STATUS_PUSH_PROTOTYPE
    private async Task StreamStatusAsync(
        StreamWriter writer,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        var clientId = headers.TryGetValue("X-Client-Id", out var value) && !string.IsNullOrWhiteSpace(value)
            ? new string(value.Trim().Take(64).ToArray())
            : "anonymous";
        var updates = Channel.CreateBounded<ServerStatusResponse>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        void OnStatusChanged(ServerStatusResponse status) => updates.Writer.TryWrite(status);

        sessionService.StatusChanged += OnStatusChanged;

        try
        {
            await writer.WriteLineAsync("HTTP/1.1 200 OK");
            await writer.WriteLineAsync("Content-Type: text/event-stream; charset=utf-8");
            await writer.WriteLineAsync("Cache-Control: no-cache");
            await writer.WriteLineAsync("Connection: close");
            await writer.WriteLineAsync("X-Accel-Buffering: no");
            await writer.WriteLineAsync();

            await WriteSseEventAsync(writer, "snapshot", await sessionService.GetStatusAsync(cancellationToken));
            logger.Info($"Status stream subscribed: clientId={clientId}.");

            var updateAvailable = updates.Reader.WaitToReadAsync(cancellationToken).AsTask();
            var heartbeatDue = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var completed = await Task.WhenAny(updateAvailable, heartbeatDue);

                if (completed == updateAvailable && await updateAvailable)
                {
                    ServerStatusResponse? latest = null;
                    while (updates.Reader.TryRead(out var status))
                    {
                        latest = status;
                    }

                    if (latest is not null)
                    {
                        await WriteSseEventAsync(writer, "statusChanged", latest);
                    }

                    updateAvailable = updates.Reader.WaitToReadAsync(cancellationToken).AsTask();
                }
                else
                {
                    await writer.WriteLineAsync($": heartbeat {DateTimeOffset.UtcNow:O}");
                    await writer.WriteLineAsync();
                    await writer.FlushAsync(cancellationToken);
                    heartbeatDue = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        finally
        {
            sessionService.StatusChanged -= OnStatusChanged;
            updates.Writer.TryComplete();
            logger.Info($"Status stream unsubscribed: clientId={clientId}.");
        }
    }

    private static async Task WriteSseEventAsync(
        StreamWriter writer,
        string eventName,
        ServerStatusResponse status)
    {
        var json = JsonSerializer.Serialize(status, SseJsonOptions);
        await writer.WriteLineAsync($"event: {eventName}");
        await writer.WriteLineAsync($"data: {json}");
        await writer.WriteLineAsync();
        await writer.FlushAsync();
    }
#endif

    private static bool TryGetBridgeEndpoint(
        IReadOnlyDictionary<string, string> query,
        out string host,
        out int apiPort,
        out int rdpPort)
    {
        host = query.TryGetValue("host", out var hostValue) ? hostValue.Trim() : string.Empty;
        apiPort = query.TryGetValue("apiPort", out var apiPortValue) && int.TryParse(apiPortValue, out var parsedApiPort)
            ? parsedApiPort
            : 0;
        rdpPort = query.TryGetValue("rdpPort", out var rdpPortValue) && int.TryParse(rdpPortValue, out var parsedRdpPort)
            ? parsedRdpPort
            : 0;

        return !string.IsNullOrWhiteSpace(host)
            && apiPort is >= 1 and <= 65535
            && rdpPort is >= 1 and <= 65535;
    }

    private static async Task<Dictionary<string, string>> ReadHeadersAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrEmpty(line))
            {
                return headers;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var name = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            headers[name] = value;
        }

        return headers;
    }

    private static async Task WriteJsonAsync(StreamWriter writer, HttpStatusCode statusCode, object value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await WriteContentAsync(writer, statusCode, json, "application/json; charset=utf-8");
    }

    private static async Task WriteContentAsync(
        StreamWriter writer,
        HttpStatusCode statusCode,
        string body,
        string contentType)
    {
        var bodyBytes = System.Text.Encoding.UTF8.GetByteCount(body);
        await writer.WriteLineAsync($"HTTP/1.1 {(int)statusCode} {GetReasonPhrase(statusCode)}");
        await writer.WriteLineAsync($"Content-Type: {contentType}");
        await writer.WriteLineAsync($"Content-Length: {bodyBytes}");
        await writer.WriteLineAsync("Connection: close");
        await writer.WriteLineAsync();
        await writer.WriteAsync(body);
    }

    private static int GetContentLength(IReadOnlyDictionary<string, string> headers)
    {
        return headers.TryGetValue("Content-Length", out var value) && int.TryParse(value, out var contentLength)
            ? contentLength
            : 0;
    }

    private static async Task<string> ReadBodyAsync(
        StreamReader reader,
        int contentLength,
        CancellationToken cancellationToken)
    {
        if (contentLength <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[contentLength];
        var offset = 0;

        while (offset < contentLength && !cancellationToken.IsCancellationRequested)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(offset, contentLength - offset), cancellationToken);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return new string(buffer, 0, offset);
    }

    private static string GetReasonPhrase(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.OK => "OK",
            HttpStatusCode.NotFound => "Not Found",
            HttpStatusCode.MethodNotAllowed => "Method Not Allowed",
            _ => statusCode.ToString()
        };
    }
}

file sealed class HttpRequestLine
{
    public string Method { get; private init; } = string.Empty;

    public string Path { get; private init; } = "/";

    public IReadOnlyDictionary<string, string> Query { get; private init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static HttpRequestLine Parse(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rawPath = parts.Length > 1 ? parts[1] : "/";
        var pathParts = rawPath.Split('?', 2);
        var path = pathParts[0];

        return new HttpRequestLine
        {
            Method = parts.Length > 0 ? parts[0] : string.Empty,
            Path = string.IsNullOrWhiteSpace(path) ? "/" : path,
            Query = pathParts.Length > 1 ? ParseQuery(pathParts[1]) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(string queryString)
    {
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pairs = queryString.Split('&', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=', 2);
            var name = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            query[name] = value;
        }

        return query;
    }
}
