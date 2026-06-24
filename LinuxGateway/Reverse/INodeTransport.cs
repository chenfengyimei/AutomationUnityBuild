namespace LinuxGateway.Reverse;

public interface INodeTransport
{
    Task<RemoteNodeInfo> GetNodeAsync(GatewayNodeRecord node);
    Task<RemoteBuildJobRecord> StartBuildAsync(GatewayNodeRecord node, RemoteStartBuildRequest request);
    Task<RemoteJobDetails> GetJobAsync(GatewayNodeRecord node, string remoteJobId);
    Task<string> GetJobLogAsync(GatewayNodeRecord node, string remoteJobId, int lines = 300);
    Task<List<RemoteArtifactRecord>> ListArtifactsAsync(GatewayNodeRecord node, string remoteJobId);
    Task<(Stream Stream, string? FileName)> DownloadArtifactAsync(GatewayNodeRecord node, string artifactId);
}
