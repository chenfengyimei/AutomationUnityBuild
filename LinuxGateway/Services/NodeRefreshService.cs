using LinuxGateway.Persistence;

namespace LinuxGateway.Services;

public sealed class NodeRefreshService(
    JsonGatewayDatabase database,
    NodeGatewayClient client,
    ILogger<NodeRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

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
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
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
}
