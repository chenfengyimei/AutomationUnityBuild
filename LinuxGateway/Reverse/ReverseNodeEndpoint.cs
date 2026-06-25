using System.Net.WebSockets;
using System.Text.Json;
using LinuxGateway.Persistence;
using LinuxGateway.Security;

namespace LinuxGateway.Reverse;

public static class ReverseNodeEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/reverse-nodes/connect", ConnectAsync);
        app.MapPost("/api/reverse-nodes/enrollment-tokens", CreateTokenAsync);
        app.MapPost("/api/reverse-nodes/enroll", EnrollAsync);
        app.MapPost("/api/reverse-nodes/{nodeId}/revoke", RevokeAsync);
        app.MapDelete("/api/reverse-nodes/{nodeId}", DeleteAsync);
        app.MapGet("/api/reverse-nodes/tokens", ListTokensAsync);
    }

    private static async Task ConnectAsync(
        HttpContext context,
        EnrollmentService enrollment,
        ReverseNodeConnectionManager connectionManager,
        GatewayCommandDispatcher dispatcher,
        JsonGatewayDatabase database,
        ILogger<ReverseNodeConnectionManager> logger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            await ApiDiagnostics.ClientError(context, new InvalidOperationException("此端点需要 WebSocket 连接。")).ExecuteAsync(context);
            return;
        }

        string? nodeId = context.Request.Query["nodeId"].FirstOrDefault();
        string? credential = null;

        if (context.Request.Headers.TryGetValue("X-Node-Credential", out var headerCred))
        {
            credential = headerCred;
        }
        if (string.IsNullOrEmpty(credential) && context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            string auth = authHeader.ToString();
            if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                credential = auth["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(credential))
        {
            await ApiDiagnostics.Unauthorized(context, "缺少 nodeId 或 credential。").ExecuteAsync(context);
            return;
        }

        bool valid = await enrollment.ValidateCredentialAsync(nodeId, credential!);
        if (!valid)
        {
            await ApiDiagnostics.Unauthorized(context, "节点凭据无效或已被吊销。").ExecuteAsync(context);
            return;
        }

        string remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();

        ReverseConnection conn = connectionManager.AddOrReplace(nodeId!, socket, remoteIp);
        string connectionId = conn.ConnectionId;

        await UpdateNodeConnectedAsync(database, nodeId!, remoteIp);

        _ = dispatcher.ResendPendingCommandsAsync(nodeId!).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                logger.LogWarning(t.Exception, "Failed to resend pending commands to node {NodeId}", nodeId);
            }
        }, TaskScheduler.Default);

        try
        {
            await ReceiveLoopAsync(socket, nodeId!, connectionManager, dispatcher, database, logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "WebSocket receive loop error for node {NodeId}", nodeId);
        }
        finally
        {
            if (connectionManager.Remove(nodeId!, connectionId))
            {
                await UpdateNodeDisconnectedAsync(database, nodeId!);
            }
        }
    }

    private static async Task ReceiveLoopAsync(
        WebSocket socket,
        string nodeId,
        ReverseNodeConnectionManager connectionManager,
        GatewayCommandDispatcher dispatcher,
        JsonGatewayDatabase database,
        ILogger logger)
    {
        byte[] buffer = new byte[8192];
        MemoryStream messageBuffer = new();

        while (socket.State is WebSocketState.Open)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, CancellationToken.None);

            if (result.MessageType is WebSocketMessageType.Close)
            {
                logger.LogInformation("Node {NodeId} initiated WebSocket close", nodeId);
                break;
            }

            messageBuffer.Write(buffer, 0, result.Count);

            if (!result.EndOfMessage)
            {
                continue;
            }

            messageBuffer.Position = 0;
            string json;
            try
            {
                json = await new StreamReader(messageBuffer).ReadToEndAsync();
            }
            finally
            {
                messageBuffer.SetLength(0);
            }

            ReverseMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<ReverseMessage>(json, ReverseProtocol.JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse message from node {NodeId}", nodeId);
                continue;
            }

            if (message is null)
            {
                continue;
            }

            try
            {
                await HandleIncomingMessageAsync(message, nodeId, connectionManager, dispatcher, database, logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to handle message type {Type} from node {NodeId}", message.Type, nodeId);
            }
        }
    }

    private static async Task HandleIncomingMessageAsync(
        ReverseMessage message,
        string nodeId,
        ReverseNodeConnectionManager connectionManager,
        GatewayCommandDispatcher dispatcher,
        JsonGatewayDatabase database,
        ILogger logger)
    {
        switch (message.Type)
        {
            case ReverseMessageTypes.Heartbeat:
                connectionManager.UpdateHeartbeat(nodeId);
                break;

            case ReverseMessageTypes.Ack:
                dispatcher.HandleResponse(message);
                break;

            case ReverseMessageTypes.Error:
                dispatcher.HandleResponse(message);
                break;

            case ReverseMessageTypes.Hello:
                connectionManager.UpdateHeartbeat(nodeId);
                await HandleHelloAsync(message, nodeId, database);
                break;

            case ReverseMessageTypes.NodeSnapshot:
                if (!string.IsNullOrEmpty(message.CorrelationId))
                {
                    dispatcher.HandleResponse(message);
                }
                await HandleNodeSnapshotAsync(message, nodeId, database);
                break;

            case ReverseMessageTypes.JobUpdated:
                if (!string.IsNullOrEmpty(message.CorrelationId))
                {
                    dispatcher.HandleResponse(message);
                }
                await HandleJobUpdatedPushAsync(message, nodeId, database, logger);
                break;

            case ReverseMessageTypes.LogChunk:
                await HandleLogChunkPushAsync(message, nodeId, database, logger);
                break;

            case ReverseMessageTypes.ArtifactChunk:
                dispatcher.HandleResponse(message);
                break;

            default:
                if (!string.IsNullOrEmpty(message.CorrelationId))
                {
                    dispatcher.HandleResponse(message);
                }
                break;
        }
    }

    private static async Task HandleHelloAsync(ReverseMessage hello, string nodeId, JsonGatewayDatabase database)
    {
        HelloPayload? payload = hello.GetPayload<HelloPayload>();
        if (payload is null)
        {
            return;
        }

        await database.UpdateAsync(db =>
        {
            GatewayNodeRecord? node = db.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is not null)
            {
                node.AgentVersion = payload.AgentVersion ?? node.AgentVersion;
                node.ProtocolVersion = payload.ProtocolVersion > 0 ? payload.ProtocolVersion : node.ProtocolVersion;
                node.ConnectionStatus = ReverseConnectionStatus.Online;
                node.LastHeartbeatAt = DateTimeOffset.Now;
                node.LastSeenAt = DateTimeOffset.Now;
                if (payload.Platforms is { Length: > 0 })
                {
                    node.Platforms = payload.Platforms.ToList();
                }
                if (payload.NodeSnapshot is not null)
                {
                    node.LastRemote = payload.NodeSnapshot;
                    node.LastStatus = payload.NodeSnapshot.Status ?? "Idle";
                }
            }
        });
    }

    private static async Task HandleNodeSnapshotAsync(ReverseMessage message, string nodeId, JsonGatewayDatabase database)
    {
        RemoteNodeInfo? snapshot = message.GetPayload<RemoteNodeInfo>();
        if (snapshot is null) return;

        await database.UpdateAsync(db =>
        {
            GatewayNodeRecord? node = db.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is not null)
            {
                node.LastRemote = snapshot;
                node.LastStatus = snapshot.Status ?? "Idle";
                node.LastSeenAt = DateTimeOffset.Now;
                node.LastError = "";
            }
        });
    }

    private static async Task HandleJobUpdatedPushAsync(ReverseMessage message, string nodeId, JsonGatewayDatabase database, ILogger logger)
    {
        JobUpdatedPush? push = message.GetPayload<JobUpdatedPush>();
        if (push?.Job is null) return;

        await database.UpdateAsync(db =>
        {
            List<GatewayJobRecord> jobs = db.Jobs.Where(j => j.NodeId == nodeId && j.RemoteJobId == push.Job.Id).ToList();
            foreach (GatewayJobRecord job in jobs)
            {
                job.Status = push.Job.Status;
                job.Error = push.Job.Error ?? "";
                job.Branch = push.Job.Branch ?? job.Branch;
                job.BuildNumber = push.Job.BuildNumber ?? job.BuildNumber;
                job.UpdatedAt = DateTimeOffset.Now;
            }
        });

        logger.LogDebug("Job updated push from node {NodeId}: jobId={JobId}, status={Status}", nodeId, push.Job.Id, push.Job.Status);
    }

    private static async Task HandleLogChunkPushAsync(ReverseMessage message, string nodeId, JsonGatewayDatabase database, ILogger logger)
    {
        LogChunkPush? push = message.GetPayload<LogChunkPush>();
        if (push is null) return;

        await database.UpdateAsync(db =>
        {
            List<GatewayJobRecord> jobs = db.Jobs.Where(j => j.NodeId == nodeId && j.RemoteJobId == push.JobId).ToList();
            foreach (GatewayJobRecord job in jobs)
            {
                job.LastLogOffset += push.Line.Length + 1;
            }
        });

        logger.LogDebug("Log chunk from node {NodeId}: jobId={JobId}, len={Length}", nodeId, push.JobId, push.Line?.Length ?? 0);
    }

    private static async Task UpdateNodeConnectedAsync(JsonGatewayDatabase database, string nodeId, string remoteIp)
    {
        await database.UpdateAsync(db =>
        {
            GatewayNodeRecord? node = db.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is not null)
            {
                node.ConnectionStatus = ReverseConnectionStatus.Online;
                node.LastHeartbeatAt = DateTimeOffset.Now;
                node.LastSeenAt = DateTimeOffset.Now;
                node.LastError = "";
            }
        });
    }

    private static async Task UpdateNodeDisconnectedAsync(JsonGatewayDatabase database, string nodeId)
    {
        await database.UpdateAsync(db =>
        {
            GatewayNodeRecord? node = db.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node is not null)
            {
                node.ConnectionStatus = ReverseConnectionStatus.Offline;
            }
        });
    }

    private static async Task<IResult> CreateTokenAsync(
        CreateTokenRequest request,
        HttpContext context,
        EnrollmentService enrollment,
        GatewayAuthService auth)
    {
        CurrentGatewayUser? user = await auth.GetUserAsync(context);
        if (user is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(user)) return ApiDiagnostics.Forbidden(context);

        try
        {
            EnrollmentTokenResult result = await enrollment.CreateTokenAsync(user, request.NodeNameHint ?? "");
            return Results.Ok(new
            {
                token = result.Token,
                tokenId = result.TokenId,
                expiresAt = result.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> EnrollAsync(EnrollRequest request, HttpContext context, EnrollmentService enrollment)
    {
        try
        {
            EnrollmentResult result = await enrollment.EnrollAsync(
                request.EnrollmentToken,
                request.NodeName ?? "",
                request.Platforms ?? [],
                request.AgentVersion ?? "1.0.0");

            return Results.Ok(new
            {
                nodeId = result.NodeId,
                credential = result.Credential,
                nodeName = result.NodeName
            });
        }
        catch (Exception ex)
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> RevokeAsync(
        string nodeId,
        HttpContext context,
        EnrollmentService enrollment,
        GatewayAuthService auth,
        ReverseNodeConnectionManager connectionManager)
    {
        CurrentGatewayUser? user = await auth.GetUserAsync(context);
        if (user is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(user)) return ApiDiagnostics.Forbidden(context);

        try
        {
            await enrollment.RevokeCredentialAsync(nodeId, user);
            await connectionManager.CloseAndRemoveAsync(nodeId, WebSocketCloseStatus.PolicyViolation, "Credential revoked");
            return Results.Ok(new { revoked = true });
        }
        catch (Exception ex)
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DeleteAsync(
        string nodeId,
        HttpContext context,
        EnrollmentService enrollment,
        GatewayAuthService auth,
        ReverseNodeConnectionManager connectionManager)
    {
        CurrentGatewayUser? user = await auth.GetUserAsync(context);
        if (user is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(user)) return ApiDiagnostics.Forbidden(context);

        try
        {
            await connectionManager.CloseAndRemoveAsync(nodeId, WebSocketCloseStatus.PolicyViolation, "Node record removed");
            bool deleted = await enrollment.DeleteReverseNodeAsync(nodeId, user);
            return deleted ? Results.Ok(new { deleted = true }) : ApiDiagnostics.NotFound(context);
        }
        catch (Exception ex)
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> ListTokensAsync(
        HttpContext context,
        EnrollmentService enrollment,
        GatewayAuthService auth)
    {
        CurrentGatewayUser? user = await auth.GetUserAsync(context);
        if (user is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(user)) return ApiDiagnostics.Forbidden(context);

        List<EnrollmentTokenRecord> tokens = await enrollment.ListTokensAsync();
        return Results.Ok(tokens.Select(t => new
        {
            t.Id,
            t.CreatedByUserName,
            t.CreatedAt,
            t.ExpiresAt,
            t.NodeNameHint
        }));
    }
}

public sealed record CreateTokenRequest(string? NodeNameHint);

public sealed record EnrollRequest(
    string EnrollmentToken,
    string? NodeName,
    string[]? Platforms,
    string? AgentVersion);

public sealed class HelloPayload
{
    public string AgentVersion { get; set; } = "";
    public int ProtocolVersion { get; set; }
    public string[]? Platforms { get; set; }
    public RemoteNodeInfo? NodeSnapshot { get; set; }
}

public sealed class JobUpdatedPush
{
    public RemoteBuildJobRecord? Job { get; set; }
}

public sealed class LogChunkPush
{
    public string JobId { get; set; } = "";
    public string Line { get; set; } = "";
    public long Offset { get; set; }
}
