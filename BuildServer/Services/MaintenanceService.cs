using BuildServer.Persistence;

namespace BuildServer.Services;

public sealed class MaintenanceService(
    JsonDatabase database,
    BuildServerOptions options,
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

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupAsync()
    {
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
                DeleteJobFiles(job, db);
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
            DeleteJobFiles(job, db);
            db.Artifacts.RemoveAll(artifact => artifact.JobId == job.Id);
            db.Jobs.Remove(job);
            total -= jobBytes;
        }
    }

    private void DeleteJobFiles(BuildJobRecord job, BuildServerDatabase db)
    {
        ProjectRecord? project = db.Projects.FirstOrDefault(project => project.Id == job.ProjectId);
        List<string> allowedRoots = [options.DataRoot];
        if (project is not null && !string.IsNullOrWhiteSpace(project.ArtifactsRoot))
        {
            allowedRoots.Add(BuildServerEnvironment.ExpandHome(project.ArtifactsRoot));
        }

        TryDelete(job.ArtifactRoot, allowedRoots);
        string? jobRoot = string.IsNullOrWhiteSpace(job.WorkerLogPath) ? null : Path.GetDirectoryName(job.WorkerLogPath);
        TryDelete(jobRoot, allowedRoots);
    }

    private void TryDelete(string? path, IReadOnlyList<string> allowedRoots)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string fullPath = Path.GetFullPath(path);
            if (IsUnsafeDeleteTarget(fullPath) || !allowedRoots.Any(root => IsSameOrChild(fullPath, root)))
            {
                logger.LogWarning("拒绝清理危险路径: {Path}", fullPath);
                return;
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }
            else if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "清理路径失败: {Path}", path);
        }
    }

    private static bool IsUnsafeDeleteTarget(string path)
    {
        string? root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            return true;
        }

        string normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalized.Length == 0 || normalized.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameOrChild(string path, string root)
    {
        string normalizedPath = NormalizeDirectory(path);
        string normalizedRoot = NormalizeDirectory(root);
        StringComparison comparison = PathComparison();
        return normalizedPath.Equals(normalizedRoot, comparison) ||
               normalizedPath.StartsWith(normalizedRoot, comparison);
    }

    private static string NormalizeDirectory(string path)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath + Path.DirectorySeparatorChar;
    }

    private static StringComparison PathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static bool IsCompleted(string status)
    {
        return status is BuildStatuses.Succeeded or BuildStatuses.Failed or BuildStatuses.Canceled;
    }
}
