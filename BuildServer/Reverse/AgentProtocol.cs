using System.Text.Json;
using System.Text.Json.Serialization;

namespace BuildServer.Reverse;

public sealed class AgentMessage
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = AgentProtocol.Version;

    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = "";

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; set; } = "";

    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("sentAt")]
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.Now;

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }

    public T? GetPayload<T>()
    {
        if (!Payload.HasValue) return default;
        JsonElement element = Payload.Value;
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return default;
        return JsonSerializer.Deserialize<T>(element.GetRawText(), AgentProtocol.JsonOptions);
    }
}

public static class AgentProtocol
{
    public const int Version = 1;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public const int HeartbeatIntervalSeconds = 15;
    public const int ReconnectBaseDelayMs = 1000;
    public const int ReconnectMaxDelayMs = 60000;
    public const int HeartbeatTimeoutMs = 30000;
}

public static class AgentMessageTypes
{
    public const string Hello = "hello";
    public const string Heartbeat = "heartbeat";
    public const string Ack = "ack";
    public const string Error = "error";
    public const string NodeSnapshot = "nodeSnapshot";
    public const string StartBuild = "startBuild";
    public const string CancelBuild = "cancelBuild";
    public const string JobUpdated = "jobUpdated";
    public const string GetJob = "getJob";
    public const string GetLog = "getLog";
    public const string LogChunk = "logChunk";
    public const string ListArtifacts = "listArtifacts";
    public const string DownloadArtifact = "downloadArtifact";
    public const string ArtifactChunk = "artifactChunk";
}

public sealed class AgentMessageBuilder
{
    public static AgentMessage Create(string type, string nodeId, string? correlationId = null, object? payload = null)
    {
        return new AgentMessage
        {
            MessageId = $"msg_{Guid.NewGuid():N}",
            CorrelationId = correlationId ?? "",
            NodeId = nodeId,
            Type = type,
            SentAt = DateTimeOffset.Now,
            Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload, AgentProtocol.JsonOptions),
        };
    }

    public static AgentMessage CreateAck(string nodeId, string correlationId)
    {
        return Create(AgentMessageTypes.Ack, nodeId, correlationId);
    }

    public static AgentMessage CreateError(string nodeId, string correlationId, string error)
    {
        return Create(AgentMessageTypes.Error, nodeId, correlationId, new { error });
    }
}
