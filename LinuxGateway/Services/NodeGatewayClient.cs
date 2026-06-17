using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LinuxGateway.Services;

public sealed class NodeGatewayClient(HttpClient httpClient)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<RemoteNodeInfo> GetNodeAsync(GatewayNodeRecord node, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(node, HttpMethod.Get, "/api/gateway/node");
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, node);
        return await ReadJsonAsync<RemoteNodeInfo>(response, cancellationToken);
    }

    public async Task<RemoteBuildJobRecord> StartBuildAsync(GatewayNodeRecord node, RemoteStartBuildRequest build, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(node, HttpMethod.Post, "/api/gateway/builds");
        request.Content = JsonContent(build);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, node);
        return await ReadJsonAsync<RemoteBuildJobRecord>(response, cancellationToken);
    }

    public async Task<RemoteJobDetails> GetJobAsync(GatewayNodeRecord node, string remoteJobId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(node, HttpMethod.Get, $"/api/gateway/jobs/{Uri.EscapeDataString(remoteJobId)}");
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, node);
        return await ReadJsonAsync<RemoteJobDetails>(response, cancellationToken);
    }

    public async Task<string> GetJobLogAsync(GatewayNodeRecord node, string remoteJobId, int lines, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(node, HttpMethod.Get, $"/api/gateway/jobs/{Uri.EscapeDataString(remoteJobId)}/log?lines={lines}");
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, node);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RemoteArtifactRecord>> ListArtifactsAsync(GatewayNodeRecord node, string remoteJobId, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = CreateRequest(node, HttpMethod.Get, $"/api/gateway/jobs/{Uri.EscapeDataString(remoteJobId)}/artifacts");
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, node);
        return await ReadJsonAsync<List<RemoteArtifactRecord>>(response, cancellationToken);
    }

    public async Task<HttpResponseMessage> DownloadArtifactAsync(GatewayNodeRecord node, string artifactId, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = CreateRequest(node, HttpMethod.Get, $"/api/gateway/artifacts/{Uri.EscapeDataString(artifactId)}/download");
        HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, node);
        return response;
    }

    private HttpRequestMessage CreateRequest(GatewayNodeRecord node, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(new Uri(NormalizeBaseUrl(node.BaseUrl)), path));
        request.Headers.Add("X-Gateway-Token", node.GatewayToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, GatewayNodeRecord node)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync();
        string message = string.IsNullOrWhiteSpace(body)
            ? response.ReasonPhrase ?? response.StatusCode.ToString()
            : body;
        throw new InvalidOperationException($"节点 {node.Name} 请求失败({(int)response.StatusCode}): {message}");
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
