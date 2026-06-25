using LinuxGateway.Persistence;
using LinuxGateway.Reverse;

namespace LinuxGateway.Services;

public sealed class JobRefreshService(
    JsonGatewayDatabase database,
    NodeTransportFactory transportFactory,
    LinuxGatewayOptions options,
    ILogger<JobRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UnconfirmedCreatingTimeout = TimeSpan.FromMinutes(5);

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
                await RefreshActiveJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to refresh gateway jobs.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.JobRefreshIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task RefreshActiveJobsAsync(CancellationToken cancellationToken = default)
    {
        List<GatewayJobRecord> activeJobs = await database.ReadAsync(db => db.Jobs
            .Where(job => job.Status is GatewayBuildStatuses.Creating or GatewayBuildStatuses.Queued or GatewayBuildStatuses.Running)
            .OrderBy(job => job.CreatedAt)
            .ToList());

        foreach (GatewayJobRecord job in activeJobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(job.RemoteJobId))
            {
                await FailStaleUnconfirmedJobAsync(job);
                continue;
            }

            try
            {
                await RefreshJobAsync(job, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Failed to refresh gateway job {JobId} from node {NodeId}.", job.Id, job.NodeId);
            }
        }
    }

    private async Task RefreshJobAsync(GatewayJobRecord job, CancellationToken cancellationToken)
    {
        GatewayNodeRecord? node = await database.ReadAsync(db => db.Nodes.FirstOrDefault(node => node.Id == job.NodeId && node.Enabled));
        if (node is null)
        {
            return;
        }

        INodeTransport transport = transportFactory.Create(node);
        RemoteJobDetails details = await transport.GetJobAsync(node, job.RemoteJobId);
        if (details.Job is null)
        {
            return;
        }

        await database.UpdateAsync(db =>
        {
            GatewayJobRecord? stored = db.Jobs.FirstOrDefault(item => item.Id == job.Id);
            if (stored is null) return;
            stored.Status = details.Job.Status;
            stored.Error = details.Job.Error;
            stored.Branch = details.Job.Branch;
            stored.BuildNumber = details.Job.BuildNumber;
            stored.UpdatedAt = DateTimeOffset.Now;
        });
    }

    private async Task FailStaleUnconfirmedJobAsync(GatewayJobRecord job)
    {
        if (DateTimeOffset.Now - job.UpdatedAt < UnconfirmedCreatingTimeout)
        {
            return;
        }

        await database.UpdateAsync(db =>
        {
            GatewayJobRecord? stored = db.Jobs.FirstOrDefault(item => item.Id == job.Id);
            if (stored is null || stored.Status != GatewayBuildStatuses.Creating || !string.IsNullOrWhiteSpace(stored.RemoteJobId))
            {
                return;
            }

            stored.Status = GatewayBuildStatuses.Failed;
            stored.Error = "Remote build was not confirmed. The gateway may have stopped before the node returned a job id.";
            stored.UpdatedAt = DateTimeOffset.Now;
        });
    }
}
