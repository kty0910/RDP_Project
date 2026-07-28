using System.Net;
using System.Text.Json;
using RemoteMonitor.Server.Logging;

namespace RemoteMonitor.Server.Bridge;

public sealed class BridgeService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object optionsLock = new();
    private readonly RdpForwarderManager forwarderManager;
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };
    private readonly FileLogger logger;
    private BridgeOptions options;

    public BridgeService(FileLogger logger)
    {
        this.logger = logger;
        options = BridgeOptions.Load(logger);
        forwarderManager = new RdpForwarderManager();
    }

    public bool Enabled => options.Enabled;

    public BridgeStatus GetStatus()
    {
        BridgeOptions currentOptions;
        lock (optionsLock)
        {
            currentOptions = ReloadOptions();
        }

        return new BridgeStatus
        {
            Enabled = currentOptions.Enabled,
            AllowedTargetCount = currentOptions.AllowedTargets.Count,
            ActiveForwarderCount = forwarderManager.ActiveForwarderCount,
            RdpPortStart = currentOptions.RdpPortStart,
            RdpPortEnd = currentOptions.RdpPortEnd,
            TargetNames = currentOptions.AllowedTargets.Select(target => target.Name).ToArray()
        };
    }

    public BridgeStatus SetEnabled(bool enabled)
    {
        lock (optionsLock)
        {
            ReloadOptions();

            if (options.Enabled == enabled)
            {
                return GetStatus();
            }

            options = options.WithEnabled(enabled);
            BridgeOptions.Save(options);

            if (!enabled)
            {
                forwarderManager.StopAll();
            }
        }

        logger.Info($"Bridge enabled changed to {enabled}.");
        return GetStatus();
    }

    public bool IsAuthorized(IReadOnlyDictionary<string, string> headers, IReadOnlyDictionary<string, string> query)
    {
        BridgeOptions currentOptions;
        lock (optionsLock)
        {
            currentOptions = ReloadOptions();
        }

        if (!currentOptions.Enabled)
        {
            return false;
        }

#if BRIDGE_TOKEN_REQUIRED
        if (string.IsNullOrWhiteSpace(currentOptions.Token))
        {
            return false;
        }

        if (headers.TryGetValue("X-Bridge-Token", out var headerToken)
            && string.Equals(headerToken, currentOptions.Token, StringComparison.Ordinal))
        {
            return true;
        }

        return query.TryGetValue("token", out var queryToken)
            && string.Equals(queryToken, currentOptions.Token, StringComparison.Ordinal);
#else
        return true;
#endif
    }

    public async Task<BridgeProxyResult> GetTargetStatusAsync(
        string host,
        int apiPort,
        int rdpPort,
        CancellationToken cancellationToken)
    {
        BridgeOptions currentOptions;
        lock (optionsLock)
        {
            currentOptions = ReloadOptions();
        }

        if (!currentOptions.Enabled)
        {
            return BridgeProxyResult.Json(HttpStatusCode.ServiceUnavailable, new { error = "Bridge is disabled." });
        }

        if (!currentOptions.TryGetTarget(host, apiPort, rdpPort, out var target))
        {
            return BridgeProxyResult.Json(HttpStatusCode.BadRequest, new { error = "Target is not allowed by bridge_settings.json." });
        }

        var targetUrl = $"http://{target.Host}:{target.ApiPort}/status";

        try
        {
            using var response = await httpClient.GetAsync(targetUrl, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json; charset=utf-8";
            return BridgeProxyResult.Content((HttpStatusCode)(int)response.StatusCode, body, contentType);
        }
        catch (Exception exception)
        {
            logger.Error($"Bridge status proxy failed: host={host}, apiPort={apiPort}, rdpPort={rdpPort}", exception);
            return BridgeProxyResult.Json(HttpStatusCode.BadGateway, new { error = "Status proxy failed.", detail = exception.Message });
        }
    }

    public BridgeProxyResult StartRdpForwarder(RdpStartRequest request, CancellationToken cancellationToken)
    {
        BridgeOptions currentOptions;
        lock (optionsLock)
        {
            currentOptions = ReloadOptions();
        }

        if (!currentOptions.Enabled)
        {
            return BridgeProxyResult.Json(HttpStatusCode.ServiceUnavailable, new { error = "Bridge is disabled." });
        }

        if (!currentOptions.TryGetTarget(request.Host, request.ApiPort, request.RdpPort, out var target))
        {
            return BridgeProxyResult.Json(HttpStatusCode.BadRequest, new { error = "Target is not allowed by bridge_settings.json." });
        }

        try
        {
            var bridgeRdpPort = forwarderManager.EnsureForwarder(
                target.Host,
                target.RdpPort,
                currentOptions.RdpPortStart,
                currentOptions.RdpPortEnd,
                cancellationToken);
            return BridgeProxyResult.Json(HttpStatusCode.OK, new RdpStartResponse(bridgeRdpPort));
        }
        catch (Exception exception)
        {
            logger.Error($"Bridge RDP forwarder failed: host={request.Host}, apiPort={request.ApiPort}, rdpPort={request.RdpPort}", exception);
            return BridgeProxyResult.Json(HttpStatusCode.BadGateway, new { error = "RDP forwarder failed.", detail = exception.Message });
        }
    }

    public void Dispose()
    {
        forwarderManager.Dispose();
        httpClient.Dispose();
    }

    private static string Serialize(object value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private BridgeOptions ReloadOptions()
    {
        options = BridgeOptions.Load(logger);
        return options;
    }

    public sealed class BridgeProxyResult
    {
        private BridgeProxyResult(HttpStatusCode statusCode, string body, string contentType)
        {
            StatusCode = statusCode;
            Body = body;
            ContentType = contentType;
        }

        public HttpStatusCode StatusCode { get; }

        public string Body { get; }

        public string ContentType { get; }

        public static BridgeProxyResult Content(HttpStatusCode statusCode, string body, string contentType)
        {
            return new BridgeProxyResult(statusCode, body, contentType);
        }

        public static BridgeProxyResult Json(HttpStatusCode statusCode, object value)
        {
            return new BridgeProxyResult(statusCode, Serialize(value), "application/json; charset=utf-8");
        }
    }
}
