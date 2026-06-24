using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinuxGateway.Reverse;

public sealed class ReverseMessage
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = ReverseProtocol.Version;

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
        return JsonSerializer.Deserialize<T>(element.GetRawText(), ReverseProtocol.JsonOptions);
    }
}

public static class ReverseProtocol
{
    public const int Version = 1;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public static class ReverseMessageTypes
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

public static class ReverseCommandStatus
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Canceled = "Canceled";
}

public static class ReverseConnectionModes
{
    public const string Direct = "Direct";
    public const string Reverse = "Reverse";
}

public static class ReverseConnectionStatus
{
    public const string Online = "Online";
    public const string Degraded = "Degraded";
    public const string Offline = "Offline";
    public const string Unknown = "Unknown";
}

public sealed class ReverseMessageBuilder
{
    public static ReverseMessage Create(string type, string nodeId, string? correlationId = null, object? payload = null)
    {
        return new ReverseMessage
        {
            MessageId = $"msg_{Guid.NewGuid():N}",
            CorrelationId = correlationId ?? "",
            NodeId = nodeId,
            Type = type,
            SentAt = DateTimeOffset.Now,
            Payload = payload is null ? null : JsonSerializer.SerializeToElement(payload, ReverseProtocol.JsonOptions),
        };
    }

    public static ReverseMessage CreateAck(string nodeId, string correlationId)
    {
        return Create(ReverseMessageTypes.Ack, nodeId, correlationId);
    }

    public static ReverseMessage CreateError(string nodeId, string correlationId, string error)
    {
        return Create(ReverseMessageTypes.Error, nodeId, correlationId, new { error });
    }
}
