using System.Text.Json;

namespace LinuxGateway.Reverse;

public sealed class ReverseNodeTransport(GatewayCommandDispatcher dispatcher) : INodeTransport
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StartBuildTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(120);

    public async Task<RemoteNodeInfo> GetNodeAsync(GatewayNodeRecord node)
    {
        ReverseMessage response = await dispatcher.SendCommandAsync(
            node.Id, ReverseMessageTypes.GetJob, new { }, timeout: DefaultTimeout);
        return response.GetPayload<RemoteNodeInfo>() ?? new RemoteNodeInfo();
    }

    public async Task<RemoteBuildJobRecord> StartBuildAsync(GatewayNodeRecord node, RemoteStartBuildRequest request)
    {
        ReverseMessage response = await dispatcher.SendCommandAsync(
            node.Id, ReverseMessageTypes.StartBuild, request,
            clientRequestId: request.ClientRequestId,
            timeout: StartBuildTimeout);
        return response.GetPayload<RemoteBuildJobRecord>() ?? new RemoteBuildJobRecord();
    }

    public async Task<RemoteJobDetails> GetJobAsync(GatewayNodeRecord node, string remoteJobId)
    {
        ReverseMessage response = await dispatcher.SendCommandAsync(
            node.Id, ReverseMessageTypes.GetJob, new { jobId = remoteJobId }, timeout: DefaultTimeout);
        return response.GetPayload<RemoteJobDetails>() ?? new RemoteJobDetails();
    }

    public async Task<string> GetJobLogAsync(GatewayNodeRecord node, string remoteJobId, int lines = 300)
    {
        ReverseMessage response = await dispatcher.SendCommandAsync(
            node.Id, ReverseMessageTypes.GetLog, new { jobId = remoteJobId, lines }, timeout: DefaultTimeout);
        LogPayload? payload = response.GetPayload<LogPayload>();
        return payload?.Content ?? "";
    }

    public async Task<List<RemoteArtifactRecord>> ListArtifactsAsync(GatewayNodeRecord node, string remoteJobId)
    {
        ReverseMessage response = await dispatcher.SendCommandAsync(
            node.Id, ReverseMessageTypes.ListArtifacts, new { jobId = remoteJobId }, timeout: DefaultTimeout);
        List<RemoteArtifactRecord>? artifacts = response.GetPayload<List<RemoteArtifactRecord>>();
        return artifacts ?? [];
    }

    public async Task<(Stream Stream, string? FileName)> DownloadArtifactAsync(GatewayNodeRecord node, string artifactId)
    {
        ReverseMessage response = await dispatcher.SendCommandAsync(
            node.Id, ReverseMessageTypes.DownloadArtifact, new { artifactId }, timeout: DownloadTimeout);

        ArtifactChunkPayload? chunk = response.GetPayload<ArtifactChunkPayload>();
        if (chunk is null || chunk.Data is null || chunk.Data.Length == 0)
        {
            throw new FileNotFoundException("产物文件不存在或为空。");
        }

        Stream stream = new MemoryStream(chunk.Data);
        return (stream, chunk.FileName);
    }
}

public sealed class LogPayload
{
    public string Content { get; set; } = "";
}

public sealed class ArtifactChunkPayload
{
    public byte[] Data { get; set; } = [];
    public string? FileName { get; set; }
    public long TotalSize { get; set; }
    public bool IsLast { get; set; } = true;
}
