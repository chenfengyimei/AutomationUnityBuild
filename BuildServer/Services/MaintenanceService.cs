using BuildServer.Persistence;
using BuildServer.Security;

namespace BuildServer.Services;

public sealed class MaintenanceService(
    JsonDatabase database,
    BuildServerOptions options,
    AuthService auth,
    StorageCleanupService cleanupService,
    ILogger<MaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        logger.LogInformation("Maintenance service starting.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Maintenance cleanup started.");
                await CleanupAsync();
                logger.LogInformation("Maintenance cleanup completed.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "维护清理失败");
            }

            await Task.Delay(TimeSpan.FromMinutes(options.SessionCleanupIntervalMinutes), stoppingToken);
        }
    }

    private async Task CleanupAsync()
    {
        int expiredSessions = await auth.CleanupExpiredSessionsAsync();
        if (expiredSessions > 0)
        {
            logger.LogInformation("Removed {Count} expired sessions.", expiredSessions);
        }

        if (options.RetentionDays <= 0)
        {
            return;
        }

        DateTimeOffset cutoff = DateTimeOffset.Now.AddDays(-options.RetentionDays);
        await database.UpdateAsync(db =>
        {
            List<BuildJobRecord> removable = db.Jobs
                .Where(job => IsCompleted(job.Status) && job.FinishedAt is not null && job.FinishedAt < cutoff)
                .OrderBy(job => job.FinishedAt)
                .ToList();

            foreach (BuildJobRecord job in removable)
            {
                cleanupService.DeleteJobFiles(job, db);
                db.Artifacts.RemoveAll(artifact => artifact.JobId == job.Id);
                db.Jobs.Remove(job);
            }

            EnforceArtifactQuota(db);
        });
    }

    private void EnforceArtifactQuota(BuildServerDatabase db)
    {
        if (options.MaxArtifactBytes <= 0)
        {
            return;
        }

        long total = db.Artifacts.Sum(artifact => artifact.SizeBytes);
        foreach (BuildJobRecord job in db.Jobs.Where(job => IsCompleted(job.Status)).OrderBy(job => job.FinishedAt))
        {
            if (total <= options.MaxArtifactBytes)
            {
                break;
            }

            long jobBytes = db.Artifacts.Where(artifact => artifact.JobId == job.Id).Sum(artifact => artifact.SizeBytes);
            cleanupService.DeleteJobFiles(job, db);
            db.Artifacts.RemoveAll(artifact => artifact.JobId == job.Id);
            db.Jobs.Remove(job);
            total -= jobBytes;
        }
    }

    private static bool IsCompleted(string status)
    {
        return status is BuildStatuses.Succeeded or BuildStatuses.Failed or BuildStatuses.Canceled;
    }
}
