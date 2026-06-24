using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using BuildServer.Persistence;
using BuildServer.Services;

namespace BuildServer;

public static class GatewayEndpoint
{
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
        if (job is null || !IsAllowedArtifactPath(artifact.Path, job))
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
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(artifact.Path, zipPath);
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

    private static bool IsAllowedArtifactPath(string path, BuildJobRecord job)
    {
        if (string.IsNullOrWhiteSpace(job.ArtifactRoot))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(job.ArtifactRoot);
        StringComparison comparison = PathComparison();
        return fullPath.Equals(fullRoot, comparison) ||
               fullPath.StartsWith(fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison);
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
}
