using System.IO.Compression;
using BuildServer.Persistence;
using BuildServer.Security;
using BuildServer.Services;

namespace BuildServer;

public static class ApiRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { ok = true, time = DateTimeOffset.Now }));

        app.MapPost("/api/auth/login", LoginAsync);
        app.MapPost("/api/auth/logout", LogoutAsync);
        app.MapGet("/api/me", MeAsync);

        app.MapGet("/api/projects", ListProjectsAsync);
        app.MapPost("/api/projects", CreateProjectAsync);
        app.MapGet("/api/configs", ListConfigsAsync);
        app.MapPost("/api/configs", CreateConfigAsync);

        app.MapPost("/api/builds", StartBuildAsync);
        app.MapGet("/api/builds", ListJobsAsync);
        app.MapGet("/api/builds/{jobId}", GetJobAsync);
        app.MapGet("/api/builds/{jobId}/log", GetJobLogAsync);
        app.MapPost("/api/builds/{jobId}/cancel", CancelJobAsync);
        app.MapGet("/api/builds/{jobId}/artifacts", ListArtifactsAsync);
        app.MapGet("/api/artifacts/{artifactId}/download", DownloadArtifactAsync);

        app.MapGet("/api/audit", ListAuditAsync);
        app.MapGet("/api/workers", ListWorkersAsync);
        app.MapPost("/api/workers/register", RegisterWorkerAsync);
        app.MapGet("/api/settings", SettingsAsync);
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext context, AuthService auth)
    {
        UserRecord? user = await auth.ValidateLoginAsync(request.UserName, request.Password);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        string token = await auth.CreateSessionAsync(user);
        context.Response.Cookies.Append(AuthService.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Expires = DateTimeOffset.Now.AddDays(7)
        });
        return Results.Ok(AuthService.ToCurrentUser(user));
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, AuthService auth)
    {
        await auth.LogoutAsync(context.Request.Cookies[AuthService.CookieName]);
        context.Response.Cookies.Delete(AuthService.CookieName);
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> MeAsync(HttpContext context, AuthService auth)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        return user is null ? Results.Unauthorized() : Results.Ok(user);
    }

    private static async Task<IResult> ListProjectsAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(await database.ReadAsync(db => db.Projects.OrderBy(project => project.Name).ToList()));
    }

    private static async Task<IResult> CreateProjectAsync(ProjectRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        ProjectRecord project = await database.UpdateAsync(db =>
        {
            var project = new ProjectRecord
            {
                Id = Ids.New("prj"),
                Name = Required(request.Name, "项目名称"),
                RepositoryUrl = Required(request.RepositoryUrl, "Git 仓库"),
                DefaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch) ? "main" : request.DefaultBranch.Trim(),
                AllowedBranches = request.AllowedBranches?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? ["main"],
                WorkspaceRoot = Required(request.WorkspaceRoot, "工作区目录"),
                ArtifactsRoot = Required(request.ArtifactsRoot, "产物目录"),
                Description = request.Description ?? "",
                CreatedAt = DateTimeOffset.Now
            };
            db.Projects.Add(project);
            AuthService.AddAudit(db, user.Id, user.UserName, "project.create", "project", project.Id, $"创建项目 {project.Name}");
            return project;
        });

        return Results.Ok(project);
    }

    private static async Task<IResult> ListConfigsAsync(HttpContext context, AuthService auth, JsonDatabase database, string? projectId)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(await database.ReadAsync(db => db.Configs
            .Where(config => string.IsNullOrWhiteSpace(projectId) || config.ProjectId == projectId)
            .OrderBy(config => config.Name)
            .ToList()));
    }

    private static async Task<IResult> CreateConfigAsync(BuildConfigRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        BuildConfigRecord config = await database.UpdateAsync(db =>
        {
            if (!db.Projects.Any(project => project.Id == request.ProjectId && project.Enabled))
            {
                throw new InvalidOperationException("项目不存在或已禁用。");
            }

            string configPath = Required(request.ConfigPath, "配置文件路径");
            if (!File.Exists(BuildQueueService.ExpandPath(configPath)))
            {
                throw new FileNotFoundException($"配置文件不存在: {configPath}");
            }

            var config = new BuildConfigRecord
            {
                Id = Ids.New("cfg"),
                ProjectId = request.ProjectId,
                Name = Required(request.Name, "配置名称"),
                ConfigPath = configPath,
                AllowMcpBuild = request.AllowMcpBuild,
                CreatedAt = DateTimeOffset.Now
            };
            db.Configs.Add(config);
            AuthService.AddAudit(db, user.Id, user.UserName, "config.create", "config", config.Id, $"创建配置 {config.Name}");
            return config;
        });

        return Results.Ok(config);
    }

    private static async Task<IResult> StartBuildAsync(
        StartBuildRequest request,
        HttpContext context,
        AuthService auth,
        BuildQueueService queue)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanBuild(user)) return Results.Forbid();

        BuildJobRecord job = await queue.EnqueueAsync(request, user, BuildSources.Web);
        return Results.Ok(job);
    }

    private static async Task<IResult> ListJobsAsync(HttpContext context, AuthService auth, JsonDatabase database, string? projectId)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(await database.ReadAsync(db => db.Jobs
            .Where(job => string.IsNullOrWhiteSpace(projectId) || job.ProjectId == projectId)
            .OrderByDescending(job => job.CreatedAt)
            .Take(100)
            .ToList()));
    }

    private static async Task<IResult> GetJobAsync(string jobId, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == jobId));
        return job is null ? Results.NotFound() : Results.Ok(job);
    }

    private static async Task<IResult> GetJobLogAsync(string jobId, HttpContext context, AuthService auth, JsonDatabase database, int? lines)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == jobId));
        if (job is null) return Results.NotFound();
        if (!File.Exists(job.WorkerLogPath)) return Results.Ok("");
        return Results.Text(Tail(job.WorkerLogPath, Math.Clamp(lines ?? 300, 20, 2000)), "text/plain; charset=utf-8");
    }

    private static async Task<IResult> CancelJobAsync(
        string jobId,
        HttpContext context,
        AuthService auth,
        BuildQueueService queue,
        BuildWorkerService worker)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanBuild(user)) return Results.Forbid();

        bool canceled = await queue.CancelQueuedAsync(jobId, user) || await worker.CancelRunningAsync(jobId, user);
        return canceled ? Results.Ok(new { canceled = true }) : Results.NotFound();
    }

    private static async Task<IResult> ListArtifactsAsync(string jobId, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(await database.ReadAsync(db => db.Artifacts.Where(artifact => artifact.JobId == jobId).ToList()));
    }

    private static async Task<IResult> DownloadArtifactAsync(string artifactId, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        BuildArtifactRecord? artifact = await database.ReadAsync(db => db.Artifacts.FirstOrDefault(artifact => artifact.Id == artifactId));
        if (artifact is null) return Results.NotFound();

        if (File.Exists(artifact.Path))
        {
            return Results.File(artifact.Path, "application/octet-stream", Path.GetFileName(artifact.Path));
        }

        if (Directory.Exists(artifact.Path))
        {
            string zipRoot = Path.Combine(options.DataRoot, "downloads");
            Directory.CreateDirectory(zipRoot);
            string zipPath = Path.Combine(zipRoot, $"{Path.GetFileName(artifact.Path)}-{artifact.Id}.zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(artifact.Path, zipPath);
            return Results.File(zipPath, "application/zip", Path.GetFileName(zipPath));
        }

        return Results.NotFound();
    }

    private static async Task<IResult> ListAuditAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();
        return Results.Ok(await database.ReadAsync(db => db.AuditLogs.OrderByDescending(item => item.CreatedAt).Take(200).ToList()));
    }

    private static async Task<IResult> ListWorkersAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(await database.ReadAsync(db => db.Workers.OrderBy(worker => worker.Name).ToList()));
    }

    private static async Task<IResult> RegisterWorkerAsync(WorkerNodeRecord request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.IsAdmin(user)) return Results.Forbid();

        WorkerNodeRecord worker = await database.UpdateAsync(db =>
        {
            WorkerNodeRecord? worker = db.Workers.FirstOrDefault(worker => worker.Id == request.Id);
            if (worker is null)
            {
                worker = request;
                db.Workers.Add(worker);
            }

            worker.Name = request.Name;
            worker.HostName = request.HostName;
            worker.UnityVersions = request.UnityVersions;
            worker.XcodeVersions = request.XcodeVersions;
            worker.ProjectIds = request.ProjectIds;
            worker.Enabled = request.Enabled;
            worker.LastSeenAt = DateTimeOffset.Now;
            AuthService.AddAudit(db, user.Id, user.UserName, "worker.register", "worker", worker.Id, $"注册/更新 Worker {worker.Name}");
            return worker;
        });

        return Results.Ok(worker);
    }

    private static async Task<IResult> SettingsAsync(HttpContext context, AuthService auth, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(new
        {
            options.DataRoot,
            options.WorkerName,
            options.PublicBaseUrl,
            options.RetentionDays,
            options.MaxArtifactBytes
        });
    }

    private static string Required(string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{field} 不能为空。")
            : value.Trim();
    }

    private static string Tail(string path, int lines)
    {
        Queue<string> queue = new();
        foreach (string line in File.ReadLines(path))
        {
            queue.Enqueue(line);
            while (queue.Count > lines)
            {
                queue.Dequeue();
            }
        }

        return string.Join(Environment.NewLine, queue);
    }
}
