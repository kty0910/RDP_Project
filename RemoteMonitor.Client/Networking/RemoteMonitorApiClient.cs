using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using RemoteMonitor.Client.Models;

namespace RemoteMonitor.Client.Networking;

public sealed class RemoteMonitorApiClient
{
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

#if STATUS_PUSH_PROTOTYPE
    private static readonly JsonSerializerOptions StatusStreamJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient statusStreamHttpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };
    private readonly string clientInstanceId = Guid.NewGuid().ToString("N");
#endif

    public async Task<HealthResponse> GetHealthAsync(RemotePcInfo remotePc, CancellationToken cancellationToken = default)
    {
        if (remotePc.UseBridge)
        {
            using var request = CreateBridgeRequest(
                HttpMethod.Get,
                remotePc,
                $"/health");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var bridgeHealth = await response.Content.ReadFromJsonAsync<HealthResponse>(cancellationToken);
            return bridgeHealth ?? new HealthResponse { Status = "Unknown", ServerTime = DateTime.Now };
        }

        var health = await httpClient.GetFromJsonAsync<HealthResponse>(
            $"{remotePc.ApiBaseUrl}/health",
            cancellationToken);

        return health ?? new HealthResponse { Status = "Unknown", ServerTime = DateTime.Now };
    }

    public async Task<RemoteStatusResponse> GetStatusAsync(RemotePcInfo remotePc, CancellationToken cancellationToken = default)
    {
        if (remotePc.UseBridge)
        {
            using var request = CreateBridgeRequest(
                HttpMethod.Get,
                remotePc,
                $"/status?host={Uri.EscapeDataString(remotePc.Host)}" +
                $"&apiPort={remotePc.Port}" +
                $"&rdpPort={remotePc.RdpPort}");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var bridgedStatus = await response.Content.ReadFromJsonAsync<RemoteStatusResponse>(cancellationToken);
            return bridgedStatus ?? new RemoteStatusResponse { CheckedAt = DateTime.Now };
        }

        var status = await httpClient.GetFromJsonAsync<RemoteStatusResponse>(
            $"{remotePc.ApiBaseUrl}/status",
            cancellationToken);

        return status ?? new RemoteStatusResponse { CheckedAt = DateTime.Now };
    }

#if STATUS_PUSH_PROTOTYPE
    public async IAsyncEnumerable<RemoteStatusResponse> StreamStatusAsync(
        RemotePcInfo remotePc,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (remotePc.UseBridge)
        {
            throw new NotSupportedException("Status push prototype supports direct connections only.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{remotePc.ApiBaseUrl}/status/stream");
        request.Headers.Add("X-Client-Id", clientInstanceId);

        using var response = await statusStreamHttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var eventName = string.Empty;
        var data = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                yield break;
            }

            if (line.Length == 0)
            {
                if (data.Length > 0
                    && (eventName.Equals("snapshot", StringComparison.OrdinalIgnoreCase)
                        || eventName.Equals("statusChanged", StringComparison.OrdinalIgnoreCase)))
                {
                    var status = JsonSerializer.Deserialize<RemoteStatusResponse>(
                        data.ToString(),
                        StatusStreamJsonOptions);
                    if (status is not null)
                    {
                        yield return status;
                    }
                }

                eventName = string.Empty;
                data.Clear();
                continue;
            }

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                eventName = line["event:".Length..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (data.Length > 0)
                {
                    data.AppendLine();
                }

                data.Append(line["data:".Length..].TrimStart());
            }
        }
    }
#endif

    public async Task<BridgeRdpStartResponse> StartBridgeRdpAsync(
        RemotePcInfo remotePc,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateBridgeRequest(
            HttpMethod.Post,
            remotePc,
            "/rdp/start");
        var json = JsonSerializer.Serialize(new
        {
            host = remotePc.Host,
            apiPort = remotePc.Port,
            rdpPort = remotePc.RdpPort
        });
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BridgeRdpStartResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Bridge returned an empty RDP start response.");
    }

    private static HttpRequestMessage CreateBridgeRequest(HttpMethod method, RemotePcInfo remotePc, string pathAndQuery)
    {
        if (string.IsNullOrWhiteSpace(remotePc.BridgeHost))
        {
            throw new InvalidOperationException("Bridge host is empty.");
        }

        var request = new HttpRequestMessage(method, $"{remotePc.BridgeApiBaseUrl}{pathAndQuery}");

#if BRIDGE_TOKEN_REQUIRED
        if (!string.IsNullOrWhiteSpace(remotePc.BridgeToken))
        {
            request.Headers.Add("X-Bridge-Token", remotePc.BridgeToken);
        }
#endif

        return request;
    }
}
