using LinuxGateway.Services;

namespace LinuxGateway.Reverse;

public sealed class DirectNodeTransport(NodeGatewayClient client) : INodeTransport
{
    public async Task<RemoteNodeInfo> GetNodeAsync(GatewayNodeRecord node)
    {
        return await client.GetNodeAsync(node);
    }

    public async Task<RemoteBuildJobRecord> StartBuildAsync(GatewayNodeRecord node, RemoteStartBuildRequest request)
    {
        return await client.StartBuildAsync(node, request);
    }

    public async Task<RemoteJobDetails> GetJobAsync(GatewayNodeRecord node, string remoteJobId)
    {
        return await client.GetJobAsync(node, remoteJobId);
    }

    public async Task<string> GetJobLogAsync(GatewayNodeRecord node, string remoteJobId, int lines = 300)
    {
        return await client.GetJobLogAsync(node, remoteJobId, lines);
    }

    public async Task<List<RemoteArtifactRecord>> ListArtifactsAsync(GatewayNodeRecord node, string remoteJobId)
    {
        IReadOnlyList<RemoteArtifactRecord> artifacts = await client.ListArtifactsAsync(node, remoteJobId);
        return artifacts.ToList();
    }

    public async Task<(Stream Stream, string? FileName)> DownloadArtifactAsync(GatewayNodeRecord node, string artifactId)
    {
        HttpResponseMessage response = await client.DownloadArtifactAsync(node, artifactId);
        string? fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        Stream stream = await response.Content.ReadAsStreamAsync();
        return (stream, fileName);
    }
}
