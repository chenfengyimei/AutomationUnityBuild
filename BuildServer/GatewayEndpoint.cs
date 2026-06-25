using System.IO.Compression;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BuildServer.Persistence;
using BuildServer.Services;

namespace BuildServer;

public static class GatewayEndpoint
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ZipLocks = new(StringComparer.OrdinalIgnoreCase);

    public static void Map(WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/gateway");
        group.MapGet("/health", HealthAsync);
        group.MapGet("/node", NodeAsync);
        group.MapPost("/builds", StartBuildAsync);
        group.MapGet("/jobs/{jobId}", GetJobAsync);
        group.MapGet("/jobs/{jobId}/log", GetJobLogAsync);
        group.MapGet("/jobs/{jobId}/artifacts", ListArtifactsAsync);
        group.MapGet("/artifacts/{artifactId}/download", DownloadArtifactAsync);
    }

    private static IResult HealthAsync(HttpContext context, BuildServerOptions options)
    {
        IResult? authFailure = RequireGateway(context, options);
        if (authFailure is not null) return authFailure;

        return Results.Ok(new
        {
            ok = true,
            time = DateTimeOffset.Now,
            machine = Environment.MachineName,
            name = string.IsNullOrWhiteSpace(options.WorkerName) ? Environment.MachineName : options.WorkerName,
            platforms = options.NodePlatforms
        });
    }

    private static async Task<IResult> NodeAsync(HttpContext context, JsonDatabase database, BuildServerOptions options)
    {
        IResult? authFailure = RequireGateway(context, options);
        if (authFailure is not null) return authFailure;

        return Results.Ok(await database.ReadAsync(db => new
        {
            id = $"node-{Environment.MachineName}",
            name = string.IsNullOrWhiteSpace(options.WorkerName) ? Environment.MachineName : options.WorkerName,
            hostName = Environment.MachineName,
            operatingSystem = OperatingSystemName(),
            platforms = options.NodePlatforms,
            publicBaseUrl = options.PublicBaseUrl,
            status = db.Jobs.Any(job => job.Status == BuildStatuses.Running) ? WorkerStatuses.Running : WorkerStatuses.Idle,
            workers = db.Workers.OrderBy(worker => worker.Name).ToList(),
            projects = db.Projects
                .Where(project => project.Enabled)
                .OrderBy(project => project.Name)
                .Select(project => new
                {
                    project.Id,
                    project.Name,
                    project.DefaultBranch,
                    project.AllowedBranches,
                    project.DefaultBuildPlatform
                })
                .ToList(),
            configs = db.Configs
                .Where(config => config.Enabled)
                .OrderBy(config => config.Name)
                .Select(config => new
                {
                    config.Id,
                    config.ProjectId,
                    config.Name,
                    config.BuildPlatform,
                    config.AllowMcpBuild
                })
                .ToList(),
            jobs = db.Jobs
                .OrderByDescending(job => job.CreatedAt)
                .Take(50)
                .ToList()
        }));
    }

    private static async Task<IResult> StartBuildAsync(
        StartBuildRequest request,
        HttpContext context,
        BuildQueueService queue,
        BuildServerOptions options)
    {
        IResult? authFailure = RequireGateway(context, options);
        if (authFailure is not null) return authFailure;

        try
        {
            var user = new CurrentUser("gateway", "linux-gateway", "Linux Gateway", Roles.Agent);
            BuildJobRecord job = await queue.EnqueueAsync(request, user, BuildSources.Gateway);
            return Results.Ok(job);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or ArgumentException)
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> GetJobAsync(string jobId, HttpContext context, JsonDatabase database, BuildServerOptions options)
    {
        IResult? authFailure = RequireGateway(context, options);
        if (authFailure is not null) return authFailure;

        object? result = await database.ReadAsync<object?>(db =>
        {
            BuildJobRecord? job = db.Jobs.FirstOrDefault(job => job.Id == jobId);
            if (job is null)
            {
                return null;
            }

            return new
            {
                job,
                artifacts = db.Artifacts.Where(artifact => artifact.JobId == jobId).ToList()
            };
        });

        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> GetJobLogAsync(
        string jobId,
        HttpContext context,
        JsonDatabase database,
        BuildServerOptions options,
        int? lines,
        bool full = false)
    {
        IResult? authFailure = RequireGateway(context, options);
        if (authFailure is not null) return authFailure;

        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == jobId));
        if (job is null) return Results.NotFound();
        SetNoStoreHeaders(context);
        if (!File.Exists(job.WorkerLogPath)) return Results.Ok("");

        string log = full
            ? LogFileReader.ReadAll(job.WorkerLogPath)
            : LogFileReader.Tail(job.WorkerLogPath, Math.Clamp(lines ?? 300, 20, 2000));
        return Results.Text(log, "text/plain; charset=utf-8");
    }

    private static async Task<IResult> ListArtifactsAsync(string jobId, HttpContext context, JsonDatabase database, BuildServerOptions options)
    {
        IResult? authFailure = RequireGateway(context, options);
        if (authFailure is not null) return authFailure;

        return Results.Ok(await database.ReadAsync(db => db.Artifacts.Where(artifact => artifact.JobId == jobId).ToList()));
    }

    private static async Task<IResult> DownloadArtifactAsync(
        string artifactId,
        HttpContext context,
        JsonDatabase database,
        BuildServerOptions options)
    {
        IResult? authFailure = RequireGateway(context, options);
        if (authFailure is not null) return authFailure;

        BuildArtifactRecord? artifact = await database.ReadAsync(db => db.Artifacts.FirstOrDefault(artifact => artifact.Id == artifactId));
        if (artifact is null) return Results.NotFound();

        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == artifact.JobId));
        if (job is null || !IsAllowedArtifactPath(artifact.Path, job, options))
        {
            return Results.Forbid();
        }

        if (File.Exists(artifact.Path))
        {
            return Results.File(artifact.Path, "application/octet-stream", Path.GetFileName(artifact.Path));
        }

        if (Directory.Exists(artifact.Path))
        {
            string zipRoot = Path.Combine(options.DataRoot, "gateway-downloads");
            Directory.CreateDirectory(zipRoot);
            string zipPath = Path.Combine(zipRoot, $"{Path.GetFileName(artifact.Path)}-{artifact.Id}.zip");
            await EnsureZipAsync(artifact.Path, zipPath);
            return Results.File(zipPath, "application/zip", Path.GetFileName(zipPath));
        }

        return Results.NotFound();
    }

    private static IResult? RequireGateway(HttpContext context, BuildServerOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.GatewayToken))
        {
            return ApiDiagnostics.NotFound(context, "Gateway 接口未启用。请设置 BUILD_SERVER_GATEWAY_TOKEN。");
        }

        string token = context.Request.Headers["X-Gateway-Token"].ToString();
        if (string.IsNullOrWhiteSpace(token) &&
            context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = context.Request.Headers.Authorization.ToString()["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(token) || !FixedTimeEquals(token, options.GatewayToken))
        {
            return ApiDiagnostics.Unauthorized(context, "Gateway Token 无效。");
        }

        return null;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static bool IsAllowedArtifactPath(string path, BuildJobRecord job, BuildServerOptions options)
    {
        if (string.IsNullOrWhiteSpace(job.ArtifactRoot))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(job.ArtifactRoot);
        bool underAllowedRoot = options.AllowedArtifactsRoots.Count == 0 ||
                                options.AllowedArtifactsRoots.Any(root => IsSameOrChild(fullPath, root));
        StringComparison comparison = PathComparison();
        return underAllowedRoot &&
               (fullPath.Equals(fullRoot, comparison) ||
                fullPath.StartsWith(fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison));
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
        string fullPath = Path.GetFullPath(BuildServerEnvironment.ExpandHome(path))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath + Path.DirectorySeparatorChar;
    }

    private static async Task EnsureZipAsync(string sourceDirectory, string zipPath)
    {
        string lockKey = Path.GetFullPath(zipPath);
        SemaphoreSlim semaphore = ZipLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            if (!File.Exists(zipPath))
            {
                string tempPath = $"{zipPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
                try
                {
                    ZipFile.CreateFromDirectory(sourceDirectory, tempPath);
                    File.Move(tempPath, zipPath, overwrite: true);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static StringComparison PathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static string OperatingSystemName()
    {
        if (OperatingSystem.IsMacOS()) return "macOS";
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        return "Unknown";
    }

    private static void SetNoStoreHeaders(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }
}
