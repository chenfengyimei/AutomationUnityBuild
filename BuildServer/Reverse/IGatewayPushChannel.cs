namespace BuildServer.Reverse;

public interface IGatewayPushChannel
{
    Task PushLogChunkAsync(string jobId, string line);
    Task PushJobUpdatedAsync(string jobId);
    bool IsConnected { get; }
}
