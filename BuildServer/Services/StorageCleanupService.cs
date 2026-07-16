using BuildServer.Persistence;
using BuildServer.Security;

namespace BuildServer.Services;

public sealed class StorageCleanupService(
    JsonDatabase database,
    BuildServerOptions options,
    ILogger<StorageCleanupService> logger)
{
    public async Task<(bool Success, string Error)> DeleteJobStorageAsync(string jobId, CurrentUser user)
    {
        try
        {
            bool found = await database.UpdateAsync(db =>
            {
                BuildJobRecord? job = db.Jobs.FirstOrDefault(item => item.Id == jobId);
                if (job is null)
                {
                    return false;
                }

                DeleteJobFiles(job, db);
                db.Artifacts.RemoveAll(artifact => artifact.JobId == jobId);
                job.ArtifactRoot = "";
                job.WorkerLogPath = "";
                AuthService.AddAudit(db, user.Id, user.UserName, "storage.delete", "job", jobId, $"手动删除任务产物文件");
                return true;
            });

            return found ? (true, "") : (false, "任务不存在。");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "删除任务 {JobId} 产物失败", jobId);
            return (false, ex.Message);
        }
    }

    public async Task<(int Deleted, List<string> Errors)> BatchDeleteAsync(string[] jobIds, CurrentUser user)
    {
        int deleted = 0;
        List<string> errors = [];

        foreach (string jobId in jobIds)
        {
            (bool success, string error) = await DeleteJobStorageAsync(jobId, user);
            if (success)
            {
                deleted++;
            }
            else
            {
                errors.Add($"{jobId}: {error}");
            }
        }

        return (deleted, errors);
    }

    public void DeleteJobFiles(BuildJobRecord job, BuildServerDatabase db)
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
                logger.LogInformation("已删除目录: {Path}", fullPath);
            }
            else if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                logger.LogInformation("已删除文件: {Path}", fullPath);
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
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return normalizedPath.Equals(normalizedRoot, comparison) ||
               normalizedPath.StartsWith(normalizedRoot, comparison);
    }

    private static string NormalizeDirectory(string path)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath + Path.DirectorySeparatorChar;
    }
}
