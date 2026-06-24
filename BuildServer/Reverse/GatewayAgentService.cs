using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using BuildServer.Persistence;
using BuildServer.Security;
using BuildServer.Services;

namespace BuildServer.Reverse;

public sealed class GatewayAgentService(
    BuildServerOptions options,
    AgentCredentialStore credentialStore,
    JsonDatabase database,
    BuildQueueService queue,
    IServiceProvider serviceProvider,
    ILogger<GatewayAgentService> logger) : BackgroundService, IGatewayPushChannel
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AgentMessage>> _pendingResponses = new();
    private ClientWebSocket? _webSocket;
    private string _nodeId = "";
    private string _gatewayUrl = "";
    private AgentCredential? _credential;
    private int _reconnectAttempts;
    private volatile bool _disposed;
    private volatile bool _manualDisconnect;
    private volatile bool _loopStarted;

    public string ConnectionStatus { get; private set; } = "Disconnected";
    public DateTimeOffset? LastHeartbeatAt { get; private set; }
    public int ReconnectCount => _reconnectAttempts;
    public string NodeId => _nodeId;

    public bool IsConnected => _webSocket?.State is WebSocketState.Open;

    public async Task<ConnectResult> ConnectAsync(string gatewayUrl, string enrollmentToken, bool autoConnect)
    {
        ConnectResult result = await EnrollAsync(gatewayUrl, enrollmentToken, autoConnect);
        StartConnectionLoop(CancellationToken.None, requireAutoConnect: false);
        return result;
    }

    private async Task<ConnectResult> EnrollAsync(string gatewayUrl, string enrollmentToken, bool autoConnect)
    {
        if (string.IsNullOrWhiteSpace(gatewayUrl))
        {
            throw new ArgumentException("Gateway 地址不能为空。");
        }

        if (string.IsNullOrWhiteSpace(enrollmentToken))
        {
            throw new ArgumentException("Enrollment Token 不能为空。");
        }

        ConnectionStatus = "Connecting";
        string normalizedUrl = gatewayUrl.TrimEnd('/');

        logger.LogInformation("Enrolling with gateway {Url}", normalizedUrl);

        using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
        var enrollRequest = new
        {
            enrollmentToken,
            nodeName = string.IsNullOrWhiteSpace(options.ReverseNodeName) ? options.WorkerName : options.ReverseNodeName,
            platforms = options.NodePlatforms.ToArray(),
            agentVersion = "1.0.0"
        };

        HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"{normalizedUrl}/api/reverse-nodes/enroll",
            enrollRequest);

        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync();
            ConnectionStatus = "Failed";
            throw new InvalidOperationException($"Enrollment 失败: {response.StatusCode} {error}");
        }

        EnrollResponse? enrollResult = await response.Content.ReadFromJsonAsync<EnrollResponse>();
        if (enrollResult is null)
        {
            ConnectionStatus = "Failed";
            throw new InvalidOperationException("Enrollment 返回无效响应。");
        }

        _credential = new AgentCredential
        {
            NodeId = enrollResult.NodeId,
            Credential = enrollResult.Credential,
            GatewayUrl = normalizedUrl,
            EnrolledAt = DateTimeOffset.Now,
            AutoConnect = autoConnect
        };

        await credentialStore.SaveAsync(_credential);
        _nodeId = _credential.NodeId;
        _gatewayUrl = _credential.GatewayUrl;
        _manualDisconnect = false;
        _reconnectAttempts = 0;

        logger.LogInformation("Enrolled successfully, nodeId={NodeId}", _nodeId);

        return new ConnectResult(enrollResult.NodeId, enrollResult.NodeName);
    }

    public async Task DisconnectAsync()
    {
        _manualDisconnect = true;
        ConnectionStatus = "Disconnected";

        if (_webSocket is not null && _webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Manual disconnect", CancellationToken.None);
            }
            catch { }
        }

        await credentialStore.UpdateAutoConnectAsync(false);
    }

    public AgentStatusInfo GetStatus()
    {
        return new AgentStatusInfo(
            ConnectionStatus,
            _nodeId,
            _gatewayUrl,
            LastHeartbeatAt,
            _reconnectAttempts,
            IsConnected);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("Gateway agent service starting. Enabled={Enabled}", options.ReverseGatewayEnabled);
        if (!options.ReverseGatewayEnabled)
        {
            logger.LogInformation("Gateway agent service disabled.");
            return;
        }

        try
        {
            _credential = await credentialStore.LoadAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load agent credential");
        }

        if (_credential is null)
        {
            if (!string.IsNullOrWhiteSpace(options.ReverseGatewayUrl) && !string.IsNullOrWhiteSpace(options.ReverseEnrollmentToken))
            {
                try
                {
                    await EnrollAsync(options.ReverseGatewayUrl, options.ReverseEnrollmentToken, true);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Auto-enrollment failed");
                    return;
                }
            }
            else
            {
                return;
            }
        }

        await RunConnectionLoopAsync(stoppingToken, requireAutoConnect: true);
    }

    private void StartConnectionLoop(CancellationToken stoppingToken, bool requireAutoConnect)
    {
        if (_loopStarted || _disposed)
        {
            return;
        }

        _ = Task.Run(() => RunConnectionLoopAsync(stoppingToken, requireAutoConnect), CancellationToken.None);
    }

    private async Task RunConnectionLoopAsync(CancellationToken stoppingToken, bool requireAutoConnect)
    {
        if (_loopStarted || _disposed)
        {
            return;
        }

        _loopStarted = true;
        _nodeId = _credential!.NodeId;
        _gatewayUrl = _credential.GatewayUrl;

        if (requireAutoConnect && !_credential.AutoConnect)
        {
            ConnectionStatus = "Disconnected";
            _loopStarted = false;
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested && !_manualDisconnect && !_disposed)
            {
                try
                {
                    await ConnectWebSocketAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "WebSocket connection error");
                }

                if (_manualDisconnect || stoppingToken.IsCancellationRequested || _disposed)
                {
                    break;
                }

                ConnectionStatus = "Reconnecting";
                _reconnectAttempts++;
                int delay = CalculateReconnectDelay(_reconnectAttempts);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            _loopStarted = false;
        }
    }

    private async Task ConnectWebSocketAsync(CancellationToken stoppingToken = default)
    {
        if (_credential is null)
        {
            return;
        }

        ConnectionStatus = "Connecting";
        _manualDisconnect = false;

        string wsUrl = _gatewayUrl.Replace("https://", "wss://").Replace("http://", "ws://");
        wsUrl = $"{wsUrl}/api/reverse-nodes/connect?nodeId={Uri.EscapeDataString(_credential.NodeId)}";

        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();
        _webSocket.Options.SetRequestHeader("X-Node-Credential", _credential.Credential);
        _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_credential.Credential}");

        logger.LogInformation("Connecting WebSocket to {Url} (nodeId={NodeId})", wsUrl, _credential.NodeId);

        await _webSocket.ConnectAsync(new Uri(wsUrl), stoppingToken);

        ConnectionStatus = "Connected";
        _reconnectAttempts = 0;
        LastHeartbeatAt = DateTimeOffset.Now;
        logger.LogInformation("WebSocket connected, nodeId={NodeId}", _nodeId);

        await SendHelloAsync(stoppingToken);
        _ = HeartbeatLoopAsync(stoppingToken);

        await ReceiveLoopAsync(stoppingToken);
    }

    private async Task HeartbeatLoopAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(AgentProtocol.HeartbeatIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                if (!IsConnected) break;

                AgentMessage heartbeat = AgentMessageBuilder.Create(AgentMessageTypes.Heartbeat, _nodeId);
                await SendMessageAsync(heartbeat, stoppingToken);
                LastHeartbeatAt = DateTimeOffset.Now;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Heartbeat failed");
                break;
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken stoppingToken)
    {
        byte[] buffer = new byte[8192];
        MemoryStream messageBuffer = new();

        while (_webSocket?.State is WebSocketState.Open && !stoppingToken.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await _webSocket.ReceiveAsync(buffer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "WebSocket receive error");
                break;
            }

            if (result.MessageType is WebSocketMessageType.Close)
            {
                logger.LogInformation("Gateway closed WebSocket connection");
                break;
            }

            messageBuffer.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;

            messageBuffer.Position = 0;
            string json;
            try
            {
                json = await new StreamReader(messageBuffer).ReadToEndAsync(stoppingToken);
            }
            finally
            {
                messageBuffer.SetLength(0);
            }

            AgentMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<AgentMessage>(json, AgentProtocol.JsonOptions);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse message from gateway");
                continue;
            }

            if (message is not null)
            {
                _ = HandleMessageAsync(message, stoppingToken);
            }
        }

        if (!_manualDisconnect)
        {
            ConnectionStatus = "Reconnecting";
        }
    }

    private async Task HandleMessageAsync(AgentMessage message, CancellationToken cancellationToken)
    {
        try
        {
            AgentMessage? response = message.Type switch
            {
                AgentMessageTypes.GetNode => await HandleGetNodeAsync(message),
                AgentMessageTypes.StartBuild => await HandleStartBuildAsync(message),
                AgentMessageTypes.CancelBuild => await HandleCancelBuildAsync(message),
                AgentMessageTypes.GetJob => await HandleGetJobAsync(message),
                AgentMessageTypes.GetLog => await HandleGetLogAsync(message),
                AgentMessageTypes.ListArtifacts => await HandleListArtifactsAsync(message),
                AgentMessageTypes.DownloadArtifact => await HandleDownloadArtifactAsync(message),
                _ => null
            };

            if (response is not null)
            {
                await SendMessageAsync(response, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling message type {Type}", message.Type);
            AgentMessage errorResponse = AgentMessageBuilder.CreateError(_nodeId, message.CorrelationId, ex.Message);
            await SendMessageAsync(errorResponse, cancellationToken);
        }
    }

    private async Task SendMessageAsync(AgentMessage message, CancellationToken cancellationToken = default)
    {
        if (_webSocket?.State is not WebSocketState.Open)
        {
            return;
        }

        string json = JsonSerializer.Serialize(message, AgentProtocol.JsonOptions);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private async Task SendHelloAsync(CancellationToken cancellationToken)
    {
        AgentMessage hello = AgentMessageBuilder.Create(
            AgentMessageTypes.Hello,
            _nodeId,
            null,
            new
            {
                agentVersion = "1.0.0",
                protocolVersion = AgentProtocol.Version,
                platforms = options.NodePlatforms.ToArray(),
                nodeSnapshot = await BuildNodeSnapshotAsync()
            });

        await SendMessageAsync(hello, cancellationToken);
    }

    private static int CalculateReconnectDelay(int attempt)
    {
        int baseDelay = AgentProtocol.ReconnectBaseDelayMs;
        int maxDelay = AgentProtocol.ReconnectMaxDelayMs;
        int delay = (int)Math.Min(baseDelay * Math.Pow(2, attempt - 1), maxDelay);
        int jitter = Random.Shared.Next(0, 1000);
        return Math.Min(delay + jitter, maxDelay + 1000);
    }

    public async Task PushLogChunkAsync(string jobId, string line)
    {
        if (!IsConnected) return;

        try
        {
            AgentMessage chunk = AgentMessageBuilder.Create(
                AgentMessageTypes.LogChunk, _nodeId, null,
                new { jobId, line, offset = (long)0 });

            await SendMessageAsync(chunk);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to push log chunk for job {JobId}", jobId);
        }
    }

    public async Task PushJobUpdatedAsync(string jobId)
    {
        if (!IsConnected) return;

        try
        {
            BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(j => j.Id == jobId));
            if (job is null) return;

            AgentMessage update = AgentMessageBuilder.Create(
                AgentMessageTypes.JobUpdated, _nodeId, null,
                new
                {
                    job = new
                    {
                        job.Id,
                        job.Status,
                        job.BuildPlatform,
                        job.Branch,
                        job.BuildNumber,
                        job.DryRun,
                        job.Error,
                        job.CreatedAt,
                        job.StartedAt,
                        job.FinishedAt
                    }
                });

            await SendMessageAsync(update);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to push job update for job {JobId}", jobId);
        }
    }

    private async Task<AgentMessage> HandleGetNodeAsync(AgentMessage command)
    {
        return AgentMessageBuilder.Create(AgentMessageTypes.NodeSnapshot, _nodeId, command.CorrelationId, await BuildNodeSnapshotAsync());
    }

    private async Task<object> BuildNodeSnapshotAsync()
    {
        List<ProjectRecord> projects = await database.ReadAsync(db => db.Projects.Where(p => p.Enabled).OrderBy(p => p.Name).ToList());
        List<BuildConfigRecord> configs = await database.ReadAsync(db => db.Configs.Where(c => c.Enabled).OrderBy(c => c.Name).ToList());
        List<BuildJobRecord> jobs = await database.ReadAsync(db => db.Jobs.OrderByDescending(j => j.CreatedAt).Take(50).ToList());
        bool anyRunning = jobs.Any(j => j.Status is BuildStatuses.Queued or BuildStatuses.Running);

        return new
        {
            id = _nodeId,
            name = string.IsNullOrWhiteSpace(options.ReverseNodeName) ? options.WorkerName : options.ReverseNodeName,
            hostName = Environment.MachineName,
            operatingSystem = Environment.OSVersion.ToString(),
            platforms = options.NodePlatforms,
            publicBaseUrl = options.PublicBaseUrl,
            status = anyRunning ? "Running" : "Idle",
            projects = projects.Select(p => new
            {
                p.Id,
                p.Name,
                p.DefaultBranch,
                p.AllowedBranches,
                p.DefaultBuildPlatform
            }),
            configs = configs.Select(c => new
            {
                c.Id,
                c.ProjectId,
                c.Name,
                c.BuildPlatform,
                c.AllowMcpBuild
            }),
            jobs = jobs.Select(j => new
            {
                j.Id,
                j.ProjectId,
                j.ConfigId,
                j.Status,
                j.BuildPlatform,
                j.Branch,
                j.BuildNumber,
                j.DryRun,
                j.Error,
                j.CreatedAt,
                j.StartedAt,
                j.FinishedAt
            })
        };
    }

    private async Task<AgentMessage> HandleStartBuildAsync(AgentMessage command)
    {
        StartBuildPayload? payload = command.GetPayload<StartBuildPayload>();
        if (payload is null)
        {
            return AgentMessageBuilder.CreateError(_nodeId, command.CorrelationId, "Invalid startBuild payload");
        }

        var request = new StartBuildRequest(
            payload.ProjectId,
            payload.ConfigId,
            payload.Branch,
            payload.BuildNumber,
            payload.DryRun,
            payload.SkipGit,
            payload.SkipUnity,
            payload.SkipXcode,
            payload.AllowNonMac,
            payload.ClientRequestId,
            payload.Notes);

        var gatewayUser = new CurrentUser("gateway", "linux-gateway", "Linux Gateway", Roles.Agent);
        BuildJobRecord job = await queue.EnqueueAsync(request, gatewayUser, BuildSources.Gateway);

        _ = PushJobUpdatedAsync(job.Id);

        return AgentMessageBuilder.Create(
            AgentMessageTypes.Ack, _nodeId, command.CorrelationId,
            new
            {
                job.Id,
                job.ProjectId,
                job.ConfigId,
                job.Status,
                job.BuildPlatform,
                job.Branch,
                job.BuildNumber,
                job.DryRun,
                job.Error,
                job.CreatedAt,
                job.StartedAt,
                job.FinishedAt
            });
    }

    private async Task<AgentMessage> HandleCancelBuildAsync(AgentMessage command)
    {
        CancelBuildPayload? payload = command.GetPayload<CancelBuildPayload>();
        if (payload is null)
        {
            return AgentMessageBuilder.CreateError(_nodeId, command.CorrelationId, "Invalid cancelBuild payload");
        }

        var gatewayUser = new CurrentUser("gateway", "linux-gateway", "Linux Gateway", Roles.Agent);
        BuildWorkerService worker = serviceProvider.GetRequiredService<BuildWorkerService>();
        bool canceled = await queue.CancelQueuedAsync(payload.JobId, gatewayUser) ||
                        await worker.CancelRunningAsync(payload.JobId, gatewayUser);

        return AgentMessageBuilder.Create(AgentMessageTypes.Ack, _nodeId, command.CorrelationId, new { canceled });
    }

    private async Task<AgentMessage> HandleGetJobAsync(AgentMessage command)
    {
        JobIdPayload? payload = command.GetPayload<JobIdPayload>();
        if (payload is null)
        {
            return AgentMessageBuilder.CreateError(_nodeId, command.CorrelationId, "Invalid getJob payload");
        }

        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(j => j.Id == payload.JobId));
        if (job is null)
        {
            return AgentMessageBuilder.CreateError(_nodeId, command.CorrelationId, "任务不存在");
        }

        List<BuildArtifactRecord> artifacts = await database.ReadAsync(db => db.Artifacts
            .Where(a => a.JobId == payload.JobId).ToList());

        return AgentMessageBuilder.Create(
            AgentMessageTypes.Ack, _nodeId, command.CorrelationId,
            new
            {
                job = new
                {
                    job.Id,
                    job.ProjectId,
                    job.ConfigId,
                    job.Status,
                    job.BuildPlatform,
                    job.Branch,
                    job.BuildNumber,
                    job.DryRun,
                    job.Error,
                    job.CreatedAt,
                    job.StartedAt,
                    job.FinishedAt
                },
                artifacts = artifacts.Select(a => new
                {
                    a.Id,
                    a.JobId,
                    a.Type,
                    a.Path,
                    a.SizeBytes,
                    a.CreatedAt
                })
            });
    }

    private async Task<AgentMessage> HandleGetLogAsync(AgentMessage command)
    {
        GetLogPayload? payload = command.GetPayload<GetLogPayload>();
        if (payload is null)
        {
            return AgentMessageBuilder.CreateError(_nodeId, command.CorrelationId, "Invalid getLog payload");
        }

        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(j => j.Id == payload.JobId));
        if (job is null || !File.Exists(job.WorkerLogPath))
        {
            return AgentMessageBuilder.Create(AgentMessageTypes.Ack, _nodeId, command.CorrelationId, new { content = "" });
        }

        string log = payload.Full == true
            ? await File.ReadAllTextAsync(job.WorkerLogPath)
            : await TailLogAsync(job.WorkerLogPath, payload.Lines ?? 300);

        return AgentMessageBuilder.Create(AgentMessageTypes.Ack, _nodeId, command.CorrelationId, new { content = log });
    }

    private async Task<AgentMessage> HandleListArtifactsAsync(AgentMessage command)
    {
        JobIdPayload? payload = command.GetPayload<JobIdPayload>();
        if (payload is null)
        {
            return AgentMessageBuilder.CreateError(_nodeId, command.CorrelationId, "Invalid listArtifacts payload");
        }

        List<BuildArtifactRecord> artifacts = await database.ReadAsync(db => db.Artifacts
            .Where(a => a.JobId == payload.JobId).ToList());

        return AgentMessageBuilder.Create(AgentMessageTypes.Ack, _nodeId, command.CorrelationId,
            artifacts.Select(a => new
            {
                a.Id,
                a.JobId,
                a.Type,
                a.Path,
                a.SizeBytes,
                a.CreatedAt
            }));
    }

    private async Task<AgentMessage?> HandleDownloadArtifactAsync(AgentMessage command)
    {
        DownloadArtifactPayload? payload = command.GetPayload<DownloadArtifactPayload>();
        if (payload is null)
        {
            return AgentMessageBuilder.CreateError(_nodeId, command.CorrelationId, "Invalid downloadArtifact payload");
        }

        BuildArtifactRecord? artifact = await database.ReadAsync(db => db.Artifacts.FirstOrDefault(a => a.Id == payload.ArtifactId));
        if (artifact is null || !File.Exists(artifact.Path))
        {
            return AgentMessageBuilder.CreateError(_nodeId, command.CorrelationId, "产物文件不存在");
        }

        string fileName = Path.GetFileName(artifact.Path);
        long totalSize = new FileInfo(artifact.Path).Length;
        const int chunkSize = 64 * 1024;

        await using FileStream fs = File.OpenRead(artifact.Path);
        byte[] buffer = new byte[chunkSize];
        int bytesRead;
        int chunkIndex = 0;

        while ((bytesRead = await fs.ReadAsync(buffer, 0, chunkSize)) > 0)
        {
            byte[] chunkData = bytesRead == chunkSize ? buffer : buffer[..bytesRead];
            bool isLast = fs.Position >= totalSize;

            AgentMessage chunkMsg = AgentMessageBuilder.Create(
                AgentMessageTypes.ArtifactChunk, _nodeId, command.CorrelationId,
                new { data = chunkData, fileName, totalSize, isLast, chunkIndex });

            await SendMessageAsync(chunkMsg);
            chunkIndex++;
        }

        return null;
    }

    private static async Task<string> TailLogAsync(string path, int lines)
    {
        Queue<string> queue = new();
        await foreach (string line in File.ReadLinesAsync(path))
        {
            queue.Enqueue(line);
            while (queue.Count > lines)
            {
                queue.Dequeue();
            }
        }
        return string.Join(Environment.NewLine, queue);
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _webSocket?.Dispose();
        base.Dispose();
    }
}

public sealed record ConnectResult(string NodeId, string NodeName);

public sealed record AgentStatusInfo(
    string Status,
    string NodeId,
    string GatewayUrl,
    DateTimeOffset? LastHeartbeatAt,
    int ReconnectAttempts,
    bool IsConnected);

public sealed class EnrollResponse
{
    public string NodeId { get; set; } = "";
    public string Credential { get; set; } = "";
    public string NodeName { get; set; } = "";
}

public sealed class StartBuildPayload
{
    public string ProjectId { get; set; } = "";
    public string ConfigId { get; set; } = "";
    public string? Branch { get; set; }
    public string? BuildNumber { get; set; }
    public bool DryRun { get; set; }
    public bool SkipGit { get; set; }
    public bool SkipUnity { get; set; }
    public bool SkipXcode { get; set; }
    public bool AllowNonMac { get; set; }
    public string? ClientRequestId { get; set; }
    public string? Notes { get; set; }
}

public sealed class CancelBuildPayload
{
    public string JobId { get; set; } = "";
}

public sealed class JobIdPayload
{
    public string JobId { get; set; } = "";
}

public sealed class GetLogPayload
{
    public string JobId { get; set; } = "";
    public int? Lines { get; set; }
    public bool? Full { get; set; }
}

public sealed class DownloadArtifactPayload
{
    public string ArtifactId { get; set; } = "";
}
