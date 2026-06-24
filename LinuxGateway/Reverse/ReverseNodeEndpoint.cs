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
        string? credential = context.Request.Query["credential"].FirstOrDefault();

        if (context.Request.Headers.TryGetValue("X-Node-Credential", out var headerCred))
        {
            credential = headerCred;
        }
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
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
            connectionManager.Remove(nodeId!);
            await UpdateNodeDisconnectedAsync(database, nodeId!);
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

            await HandleIncomingMessageAsync(message, nodeId, connectionManager, dispatcher, database, logger);
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
                break;

            case ReverseMessageTypes.Error:
            case ReverseMessageTypes.JobUpdated:
            case ReverseMessageTypes.ArtifactChunk:
            case ReverseMessageTypes.NodeSnapshot:
            case ReverseMessageTypes.LogChunk:
                dispatcher.HandleResponse(message);
                break;

            case ReverseMessageTypes.Hello:
                connectionManager.UpdateHeartbeat(nodeId);
                await HandleHelloAsync(message, nodeId, database);
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
        GatewayAuthService auth)
    {
        CurrentGatewayUser? user = await auth.GetUserAsync(context);
        if (user is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(user)) return ApiDiagnostics.Forbidden(context);

        try
        {
            await enrollment.RevokeCredentialAsync(nodeId, user);
            return Results.Ok(new { revoked = true });
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
