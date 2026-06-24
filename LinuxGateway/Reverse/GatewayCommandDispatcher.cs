using System.Collections.Concurrent;
using System.Text.Json;

namespace LinuxGateway.Reverse;

public sealed class GatewayCommandDispatcher(
    ReverseNodeConnectionManager connectionManager,
    GatewayCommandStore commandStore,
    ILogger<GatewayCommandDispatcher> logger)
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ReverseMessage>> _pending = new();
    private readonly ConcurrentDictionary<string, Action<ReverseMessage>> _intermediateHandlers = new();

    public async Task<ReverseMessage> SendCommandAsync(
        string nodeId,
        string type,
        object payload,
        string? clientRequestId = null,
        TimeSpan? timeout = null,
        Action<ReverseMessage>? onIntermediateMessage = null)
    {
        if (!connectionManager.IsOnline(nodeId))
        {
            throw new InvalidOperationException("节点不在线，无法发送命令。");
        }

        string correlationId = $"cmd_{Guid.NewGuid():N}";
        TimeSpan effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);

        await commandStore.CreateAsync(nodeId, type, clientRequestId ?? "", correlationId, payload);

        var tcs = new TaskCompletionSource<ReverseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[correlationId] = tcs;

        using CancellationTokenSource cts = new(effectiveTimeout);
        cts.Token.Register(() =>
        {
            if (_pending.TryRemove(correlationId, out _))
            {
                _intermediateHandlers.TryRemove(correlationId, out _);
                tcs.TrySetException(new TimeoutException($"命令 {type} 超时（{effectiveTimeout.TotalSeconds:F0}秒）。"));
            }
        });

        try
        {
            ReverseMessage command = ReverseMessageBuilder.Create(type, nodeId, correlationId, payload);
            bool sent = await connectionManager.SendAsync(nodeId, command);

            if (!sent)
            {
                _pending.TryRemove(correlationId, out _);
                throw new InvalidOperationException("发送命令失败，节点可能已断开。");
            }

            await commandStore.MarkSentAsync(correlationId);

            if (onIntermediateMessage is not null)
            {
                _intermediateHandlers[correlationId] = onIntermediateMessage;
            }

            ReverseMessage response = await tcs.Task.WaitAsync(cts.Token);

            string? resultJson = response.Type == ReverseMessageTypes.Error
                ? null
                : (response.Payload.HasValue ? response.Payload.Value.GetRawText() : null);
            string? error = response.Type == ReverseMessageTypes.Error
                ? response.GetPayload<ErrorResponse>()?.Error ?? "未知错误"
                : null;

            await commandStore.MarkCompletedAsync(correlationId, resultJson, error);

            if (response.Type == ReverseMessageTypes.Error)
            {
                throw new InvalidOperationException(error ?? "节点返回错误。");
            }

            return response;
        }
        catch (Exception ex) when (ex is TimeoutException || ex is OperationCanceledException && cts.IsCancellationRequested)
        {
            _pending.TryRemove(correlationId, out _);
            _intermediateHandlers.TryRemove(correlationId, out _);
            logger.LogWarning(ex, "Command {Type} timed out for node {NodeId}; keeping command {CorrelationId} recoverable.", type, nodeId, correlationId);
            throw;
        }
        catch (Exception ex) when (ex is not TimeoutException)
        {
            _pending.TryRemove(correlationId, out _);
            _intermediateHandlers.TryRemove(correlationId, out _);
            await commandStore.MarkCompletedAsync(correlationId, null, ex.Message);
            throw;
        }
    }

    public void HandleResponse(ReverseMessage message)
    {
        if (string.IsNullOrEmpty(message.CorrelationId))
        {
            return;
        }

        if (message.Type == ReverseMessageTypes.ArtifactChunk)
        {
            if (_intermediateHandlers.TryGetValue(message.CorrelationId, out Action<ReverseMessage>? handler))
            {
                ArtifactChunkPayload? chunk = message.GetPayload<ArtifactChunkPayload>();
                handler(message);
                if (chunk is not null && chunk.IsLast)
                {
                    _intermediateHandlers.TryRemove(message.CorrelationId, out _);
                    if (_pending.TryRemove(message.CorrelationId, out TaskCompletionSource<ReverseMessage>? tcs))
                    {
                        tcs.TrySetResult(message);
                    }
                }
                return;
            }
        }

        _intermediateHandlers.TryRemove(message.CorrelationId, out _);
        if (_pending.TryRemove(message.CorrelationId, out TaskCompletionSource<ReverseMessage>? pendingTcs))
        {
            pendingTcs.TrySetResult(message);
        }
    }

    public async Task<int> ResendPendingCommandsAsync(string nodeId)
    {
        List<GatewayCommandRecord> pending = await commandStore.GetPendingForNodeAsync(nodeId);
        if (pending.Count == 0)
        {
            return 0;
        }

        logger.LogInformation("Resending {Count} pending commands to node {NodeId}", pending.Count, nodeId);
        int resent = 0;

        foreach (GatewayCommandRecord cmd in pending)
        {
            if (!connectionManager.IsOnline(nodeId))
            {
                break;
            }

            try
            {
                ReverseMessage command = new()
                {
                    MessageId = $"msg_{Guid.NewGuid():N}",
                    CorrelationId = cmd.CorrelationId,
                    NodeId = nodeId,
                    Type = cmd.Type,
                    SentAt = DateTimeOffset.Now,
                    Payload = string.IsNullOrEmpty(cmd.PayloadJson)
                        ? null
                        : JsonDocument.Parse(cmd.PayloadJson).RootElement.Clone()
                };

                bool sent = await connectionManager.SendAsync(nodeId, command);
                if (sent)
                {
                    await commandStore.MarkSentAsync(cmd.Id);
                    resent++;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to resend command {CommandId} to node {NodeId}", cmd.Id, nodeId);
            }
        }

        return resent;
    }

    public bool HasPending(string correlationId)
    {
        return _pending.ContainsKey(correlationId);
    }
}

public sealed record ErrorResponse(string Error);
