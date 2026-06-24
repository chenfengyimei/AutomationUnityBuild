using System.Net.WebSockets;
using LinuxGateway.Persistence;
using LinuxGateway.Reverse;

namespace LinuxGateway.Services;

public sealed class NodeRefreshService(
    JsonGatewayDatabase database,
    NodeGatewayClient client,
    ReverseNodeConnectionManager connectionManager,
    ReverseNodeTransport reverseTransport,
    ILogger<NodeRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DegradedTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan OfflineTimeout = TimeSpan.FromSeconds(90);

    public async Task RefreshNodeAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        GatewayNodeRecord? node = await database.ReadAsync(db => db.Nodes.FirstOrDefault(item => item.Id == nodeId));
        if (node is null)
        {
            return;
        }

        await RefreshNodeRecordAsync(node, cancellationToken);
    }

    public async Task RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        List<GatewayNodeRecord> nodes = await database.ReadAsync(db => db.Nodes
            .Where(node => node.Enabled)
            .OrderBy(node => node.Name)
            .ToList());

        using SemaphoreSlim throttle = new(8);
        IEnumerable<Task> tasks = nodes.Select(async node =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                await RefreshNodeRecordAsync(node, cancellationToken);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "刷新节点状态失败。");
            }

            try
            {
                await Task.Delay(RefreshInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RefreshNodeRecordAsync(GatewayNodeRecord node, CancellationToken cancellationToken)
    {
        if (!node.Enabled)
        {
            await database.UpdateAsync(db =>
            {
                GatewayNodeRecord? stored = db.Nodes.FirstOrDefault(item => item.Id == node.Id);
                if (stored is null) return;
                stored.LastStatus = "Disabled";
                stored.LastError = "";
                stored.LastRemote = null;
            });
            return;
        }

        if (node.ConnectionMode == ReverseConnectionModes.Reverse)
        {
            await RefreshReverseNodeAsync(node, cancellationToken);
            return;
        }

        try
        {
            await client.GetHealthAsync(node, cancellationToken);
            RemoteNodeInfo remote = await client.GetNodeAsync(node, cancellationToken);
            await database.UpdateAsync(db =>
            {
                GatewayNodeRecord? stored = db.Nodes.FirstOrDefault(item => item.Id == node.Id);
                if (stored is null) return;
                stored.LastSeenAt = DateTimeOffset.Now;
                stored.LastStatus = remote.Status;
                stored.LastError = "";
                stored.LastRemote = remote;
                if (stored.Platforms.Count == 0)
                {
                    stored.Platforms = remote.Platforms;
                }
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await database.UpdateAsync(db =>
            {
                GatewayNodeRecord? stored = db.Nodes.FirstOrDefault(item => item.Id == node.Id);
                if (stored is null) return;
                stored.LastStatus = "Offline";
                stored.LastError = ex.Message;
                stored.LastRemote = null;
            });
        }
    }

    private async Task RefreshReverseNodeAsync(GatewayNodeRecord node, CancellationToken cancellationToken)
    {
        ReverseConnection? conn = connectionManager.GetConnection(node.Id);
        DateTimeOffset now = DateTimeOffset.Now;

        string status;
        string error = "";
        DateTimeOffset? lastHeartbeat = conn?.LastHeartbeatAt ?? node.LastHeartbeatAt;
        RemoteNodeInfo? remote = null;

        if (conn is null || conn.Socket.State is not WebSocketState.Open)
        {
            status = "Offline";
            error = "WebSocket 连接已断开。";
        }
        else if (now - lastHeartbeat > OfflineTimeout)
        {
            status = "Offline";
            error = "心跳超时（90秒无心跳）。";
        }
        else if (now - lastHeartbeat > DegradedTimeout)
        {
            status = "Degraded";
            error = "心跳延迟（45秒无心跳）。";
        }
        else
        {
            status = "Online";
        }

        if (status is "Online" or "Degraded")
        {
            try
            {
                remote = await reverseTransport.GetNodeAsync(node);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (status == "Online")
                {
                    status = "Degraded";
                }
                error = ex.Message;
            }
        }

        await database.UpdateAsync(db =>
        {
            GatewayNodeRecord? stored = db.Nodes.FirstOrDefault(item => item.Id == node.Id);
            if (stored is null) return;
            stored.ConnectionStatus = status;
            stored.LastHeartbeatAt = lastHeartbeat;
            stored.LastError = error;
            if (remote is not null)
            {
                stored.LastRemote = remote;
                if (stored.Platforms.Count == 0)
                {
                    stored.Platforms = remote.Platforms;
                }
            }

            if (status == "Online" || status == "Degraded")
            {
                stored.LastSeenAt = now;
                stored.LastStatus = string.IsNullOrWhiteSpace(remote?.Status) ? status : remote.Status;
            }
            else if (status == "Offline")
            {
                stored.LastStatus = "Offline";
                stored.LastRemote = null;
            }
        });
    }
}
