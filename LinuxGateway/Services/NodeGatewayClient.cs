using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LinuxGateway.Services;

public sealed class NodeGatewayClient(HttpClient httpClient)
{
    private static readonly TimeSpan HealthTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan NodeTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StartBuildTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DownloadHeadersTimeout = TimeSpan.FromSeconds(30);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<RemoteGatewayHealth> GetHealthAsync(GatewayNodeRecord node, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendWithPolicyAsync(
            node,
            () => CreateRequest(node, HttpMethod.Get, "/api/gateway/health"),
            HealthTimeout,
            retryTransient: true,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        return await ReadJsonAsync<RemoteGatewayHealth>(response, cancellationToken);
    }

    public async Task<RemoteNodeInfo> GetNodeAsync(GatewayNodeRecord node, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendWithPolicyAsync(
            node,
            () => CreateRequest(node, HttpMethod.Get, "/api/gateway/node"),
            NodeTimeout,
            retryTransient: true,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        return await ReadJsonAsync<RemoteNodeInfo>(response, cancellationToken);
    }

    public async Task<RemoteBuildJobRecord> StartBuildAsync(GatewayNodeRecord node, RemoteStartBuildRequest build, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendWithPolicyAsync(
            node,
            () =>
            {
                HttpRequestMessage request = CreateRequest(node, HttpMethod.Post, "/api/gateway/builds");
                request.Content = JsonContent(build);
                return request;
            },
            StartBuildTimeout,
            retryTransient: false,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        return await ReadJsonAsync<RemoteBuildJobRecord>(response, cancellationToken);
    }

    public async Task<RemoteJobDetails> GetJobAsync(GatewayNodeRecord node, string remoteJobId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendWithPolicyAsync(
            node,
            () => CreateRequest(node, HttpMethod.Get, $"/api/gateway/jobs/{Uri.EscapeDataString(remoteJobId)}"),
            JobTimeout,
            retryTransient: true,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        return await ReadJsonAsync<RemoteJobDetails>(response, cancellationToken);
    }

    public async Task<string> GetJobLogAsync(GatewayNodeRecord node, string remoteJobId, int lines, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendWithPolicyAsync(
            node,
            () => CreateRequest(node, HttpMethod.Get, $"/api/gateway/jobs/{Uri.EscapeDataString(remoteJobId)}/log?lines={lines}"),
            JobTimeout,
            retryTransient: true,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteArtifactRecord>> ListArtifactsAsync(GatewayNodeRecord node, string remoteJobId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendWithPolicyAsync(
            node,
            () => CreateRequest(node, HttpMethod.Get, $"/api/gateway/jobs/{Uri.EscapeDataString(remoteJobId)}/artifacts"),
            JobTimeout,
            retryTransient: true,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);
        return await ReadJsonAsync<List<RemoteArtifactRecord>>(response, cancellationToken);
    }

    public async Task<HttpResponseMessage> DownloadArtifactAsync(GatewayNodeRecord node, string artifactId, CancellationToken cancellationToken = default)
    {
        return await SendWithPolicyAsync(
            node,
            () => CreateRequest(node, HttpMethod.Get, $"/api/gateway/artifacts/{Uri.EscapeDataString(artifactId)}/download"),
            DownloadHeadersTimeout,
            retryTransient: true,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithPolicyAsync(
        GatewayNodeRecord node,
        Func<HttpRequestMessage> requestFactory,
        TimeSpan timeout,
        bool retryTransient,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        int attempts = retryTransient ? 2 : 1;
        Exception? lastException = null;

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            HttpRequestMessage request = requestFactory();
            try
            {
                HttpResponseMessage response = await httpClient.SendAsync(request, completionOption, timeoutCts.Token);
                if (retryTransient && attempt < attempts && IsTransient(response.StatusCode))
                {
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                    continue;
                }

                await EnsureSuccessAsync(response, node, cancellationToken);
                return response;
            }
            catch (Exception ex) when (retryTransient && attempt < attempts && IsTransientException(ex, cancellationToken))
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
            finally
            {
                request.Dispose();
            }
        }

        throw lastException ?? new InvalidOperationException($"节点 {node.Name} 请求超时或失败。");
    }

    private HttpRequestMessage CreateRequest(GatewayNodeRecord node, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(NormalizeBaseUrl(node.BaseUrl)), path));
        request.Headers.Add("X-Gateway-Token", node.GatewayToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add(ApiDiagnostics.RequestIdHeader, Guid.NewGuid().ToString("N"));
        return request;
    }

    private HttpContent JsonContent<T>(T value)
    {
        string json = JsonSerializer.Serialize(value, _jsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("节点返回空响应。");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, GatewayNodeRecord node, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        string message = ParseErrorMessage(body) ?? response.ReasonPhrase ?? response.StatusCode.ToString();
        throw new InvalidOperationException($"节点 {node.Name} 请求失败({(int)response.StatusCode}): {message}");
    }

    private static string? ParseErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            JsonNode? json = JsonNode.Parse(body);
            string? detail = json?["detail"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(detail)) return detail;
            string? error = json?["error"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(error)) return error;
            string? title = json?["title"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(title)) return title;
            string? message = json?["message"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(message)) return message;
        }
        catch (JsonException)
        {
            return body.Length > 1000 ? body[..1000] : body;
        }

        return body.Length > 1000 ? body[..1000] : body;
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout ||
            (int)statusCode >= 500;
    }

    private static bool IsTransientException(Exception exception, CancellationToken callerToken)
    {
        return !callerToken.IsCancellationRequested &&
            exception is HttpRequestException or TaskCanceledException or TimeoutException;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        string value = baseUrl.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("节点地址必须是 http 或 https URL。");
        }

        return value.TrimEnd('/') + "/";
    }
}
