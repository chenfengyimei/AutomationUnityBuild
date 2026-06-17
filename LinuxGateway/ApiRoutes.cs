using System.Net.Http.Headers;
using LinuxGateway.Persistence;
using LinuxGateway.Security;
using LinuxGateway.Services;

namespace LinuxGateway;

public static class ApiRoutes
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { ok = true, time = DateTimeOffset.Now }));
        app.MapPost("/api/auth/login", LoginAsync);
        app.MapPost("/api/auth/logout", LogoutAsync);
        app.MapGet("/api/me", MeAsync);
        app.MapGet("/api/nodes", ListNodesAsync);
        app.MapPost("/api/nodes", SaveNodeAsync);
        app.MapPost("/api/nodes/{nodeId}/refresh", RefreshNodeAsync);
        app.MapGet("/api/builds", ListBuildsAsync);
        app.MapPost("/api/builds", StartBuildAsync);
        app.MapGet("/api/builds/{jobId}", GetBuildAsync);
        app.MapGet("/api/builds/{jobId}/log", GetBuildLogAsync);
        app.MapGet("/api/builds/{jobId}/artifacts", ListArtifactsAsync);
        app.MapGet("/api/builds/{jobId}/artifacts/{artifactId}/download", DownloadArtifactAsync);
        app.MapGet("/api/settings", SettingsAsync);
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext context, GatewayAuthService auth)
    {
        if (!await auth.ValidateLoginAsync(request.UserName, request.Password))
        {
            return Results.Json(new { error = "账号或密码错误。" }, statusCode: StatusCodes.Status401Unauthorized);
        }

        string token = await auth.CreateSessionAsync();
        context.Response.Cookies.Append(GatewayAuthService.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Expires = DateTimeOffset.Now.AddDays(7)
        });
        return Results.Ok(new CurrentGatewayUser("admin", "管理员"));
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, GatewayAuthService auth)
    {
        await auth.LogoutAsync(context.Request.Cookies[GatewayAuthService.CookieName]);
        context.Response.Cookies.Delete(GatewayAuthService.CookieName);
        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> MeAsync(HttpContext context, GatewayAuthService auth)
    {
        CurrentGatewayUser? user = await auth.GetUserAsync(context);
        return user is null ? Results.Unauthorized() : Results.Ok(user);
    }

    private static async Task<IResult> ListNodesAsync(
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database,
        NodeGatewayClient client)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        List<GatewayNodeRecord> nodes = await database.ReadAsync(db => db.Nodes.OrderBy(node => node.Name).ToList());
        List<GatewayNodeView> views = [];
        foreach (GatewayNodeRecord node in nodes)
        {
            views.Add(await ToNodeViewAsync(node, client, database, refreshRemote: node.Enabled));
        }

        return Results.Ok(views);
    }

    private static async Task<IResult> SaveNodeAsync(
        GatewayNodeRequest request,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        try
        {
            GatewayNodeRecord node = await database.UpdateAsync(db =>
            {
                GatewayNodeRecord? existing = string.IsNullOrWhiteSpace(request.Id)
                    ? null
                    : db.Nodes.FirstOrDefault(node => node.Id == request.Id);

                string token = request.GatewayToken?.Trim() ?? "";
                if (existing is null && string.IsNullOrWhiteSpace(token))
                {
                    throw new InvalidOperationException("新增节点必须填写 Gateway Token。");
                }

                if (existing is null)
                {
                    existing = new GatewayNodeRecord
                    {
                        Id = Ids.New("node"),
                        CreatedAt = DateTimeOffset.Now
                    };
                    db.Nodes.Add(existing);
                }

                existing.Name = Required(request.Name, "节点名称");
                existing.BaseUrl = NormalizeBaseUrl(Required(request.BaseUrl, "节点地址"));
                if (!string.IsNullOrWhiteSpace(token))
                {
                    existing.GatewayToken = token;
                }

                existing.Platforms = NormalizePlatforms(request.Platforms);
                existing.Enabled = request.Enabled;
                return existing;
            });

            return Results.Ok(ToStoredNodeView(node));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> RefreshNodeAsync(
        string nodeId,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database,
        NodeGatewayClient client)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        GatewayNodeRecord? node = await database.ReadAsync(db => db.Nodes.FirstOrDefault(node => node.Id == nodeId));
        if (node is null) return Results.NotFound();
        return Results.Ok(await ToNodeViewAsync(node, client, database, refreshRemote: true));
    }

    private static async Task<IResult> ListBuildsAsync(HttpContext context, GatewayAuthService auth, JsonGatewayDatabase database)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        return Results.Ok(await database.ReadAsync(db => db.Jobs
            .OrderByDescending(job => job.CreatedAt)
            .Take(100)
            .ToList()));
    }

    private static async Task<IResult> StartBuildAsync(
        GatewayStartBuildRequest request,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database,
        NodeGatewayClient client)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        try
        {
            GatewayNodeRecord node = await GetEnabledNodeAsync(database, request.NodeId);
            RemoteNodeInfo remote = await client.GetNodeAsync(node);
            RemoteConfigSummary config = remote.Configs.FirstOrDefault(config => config.Id == request.ConfigId && config.ProjectId == request.ProjectId)
                ?? throw new InvalidOperationException("节点上不存在这个配置。");
            RemoteProjectSummary project = remote.Projects.FirstOrDefault(project => project.Id == request.ProjectId)
                ?? throw new InvalidOperationException("节点上不存在这个项目。");

            EnsurePlatformAllowed(node, remote, config.BuildPlatform);

            var remoteRequest = new RemoteStartBuildRequest(
                request.ProjectId,
                request.ConfigId,
                request.Branch,
                request.BuildNumber,
                request.DryRun,
                request.SkipGit,
                request.SkipUnity,
                request.SkipXcode,
                request.AllowNonMac,
                request.Notes);
            RemoteBuildJobRecord remoteJob = await client.StartBuildAsync(node, remoteRequest);

            GatewayJobRecord gatewayJob = await database.UpdateAsync(db =>
            {
                var job = new GatewayJobRecord
                {
                    Id = Ids.New("gwjob"),
                    NodeId = node.Id,
                    NodeName = node.Name,
                    RemoteJobId = remoteJob.Id,
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    ConfigId = config.Id,
                    ConfigName = config.Name,
                    BuildPlatform = config.BuildPlatform,
                    Branch = remoteJob.Branch,
                    BuildNumber = remoteJob.BuildNumber,
                    DryRun = remoteJob.DryRun,
                    Status = remoteJob.Status,
                    Error = remoteJob.Error,
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now
                };
                db.Jobs.Add(job);
                return job;
            });

            return Results.Ok(gatewayJob);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private static async Task<IResult> GetBuildAsync(
        string jobId,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database,
        NodeGatewayClient client)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        GatewayJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == jobId));
        if (job is null) return Results.NotFound();

        RemoteJobDetails? remote = await TryRefreshJobAsync(database, client, job);
        return Results.Ok(new { job, remote });
    }

    private static async Task<IResult> GetBuildLogAsync(
        string jobId,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database,
        NodeGatewayClient client,
        int? lines)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        GatewayJobRecord job = await GetJobAsync(database, jobId);
        GatewayNodeRecord node = await GetEnabledNodeAsync(database, job.NodeId);
        string log = await client.GetJobLogAsync(node, job.RemoteJobId, Math.Clamp(lines ?? 300, 20, 2000));
        return Results.Text(log, "text/plain; charset=utf-8");
    }

    private static async Task<IResult> ListArtifactsAsync(
        string jobId,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database,
        NodeGatewayClient client)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        GatewayJobRecord job = await GetJobAsync(database, jobId);
        GatewayNodeRecord node = await GetEnabledNodeAsync(database, job.NodeId);
        return Results.Ok(await client.ListArtifactsAsync(node, job.RemoteJobId));
    }

    private static async Task<IResult> DownloadArtifactAsync(
        string jobId,
        string artifactId,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database,
        NodeGatewayClient client)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        GatewayJobRecord job = await GetJobAsync(database, jobId);
        GatewayNodeRecord node = await GetEnabledNodeAsync(database, job.NodeId);
        HttpResponseMessage response = await client.DownloadArtifactAsync(node, artifactId);
        Stream stream = await response.Content.ReadAsStreamAsync();
        string fileName = FileNameFromContentDisposition(response.Content.Headers.ContentDisposition) ?? artifactId;
        string contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        return Results.Stream(stream, contentType, fileName);
    }

    private static async Task<IResult> SettingsAsync(HttpContext context, GatewayAuthService auth, LinuxGatewayOptions options)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        return Results.Ok(new
        {
            options.DataRoot,
            options.PublicBaseUrl
        });
    }

    private static async Task<bool> IsAuthenticatedAsync(HttpContext context, GatewayAuthService auth)
    {
        return await auth.GetUserAsync(context) is not null;
    }

    private static async Task<GatewayNodeView> ToNodeViewAsync(
        GatewayNodeRecord node,
        NodeGatewayClient client,
        JsonGatewayDatabase database,
        bool refreshRemote)
    {
        GatewayNodeView view = ToStoredNodeView(node);
        if (!refreshRemote)
        {
            return view;
        }

        try
        {
            RemoteNodeInfo remote = await client.GetNodeAsync(node);
            await database.UpdateAsync(db =>
            {
                GatewayNodeRecord? stored = db.Nodes.FirstOrDefault(item => item.Id == node.Id);
                if (stored is null) return;
                stored.LastSeenAt = DateTimeOffset.Now;
                stored.LastStatus = remote.Status;
                stored.LastError = "";
                if (stored.Platforms.Count == 0)
                {
                    stored.Platforms = remote.Platforms;
                }
            });

            view.Remote = remote;
            view.LastSeenAt = DateTimeOffset.Now;
            view.LastStatus = remote.Status;
            view.LastError = "";
            if (view.Platforms.Count == 0)
            {
                view.Platforms = remote.Platforms;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            await database.UpdateAsync(db =>
            {
                GatewayNodeRecord? stored = db.Nodes.FirstOrDefault(item => item.Id == node.Id);
                if (stored is null) return;
                stored.LastStatus = "Offline";
                stored.LastError = ex.Message;
            });
            view.LastStatus = "Offline";
            view.LastError = ex.Message;
        }

        return view;
    }

    private static GatewayNodeView ToStoredNodeView(GatewayNodeRecord node)
    {
        return new GatewayNodeView
        {
            Id = node.Id,
            Name = node.Name,
            BaseUrl = node.BaseUrl,
            Platforms = node.Platforms,
            Enabled = node.Enabled,
            TokenConfigured = !string.IsNullOrWhiteSpace(node.GatewayToken),
            LastSeenAt = node.LastSeenAt,
            LastStatus = node.LastStatus,
            LastError = node.LastError
        };
    }

    private static async Task<RemoteJobDetails?> TryRefreshJobAsync(JsonGatewayDatabase database, NodeGatewayClient client, GatewayJobRecord job)
    {
        try
        {
            GatewayNodeRecord node = await GetEnabledNodeAsync(database, job.NodeId);
            RemoteJobDetails details = await client.GetJobAsync(node, job.RemoteJobId);
            if (details.Job is not null)
            {
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

            return details;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<GatewayNodeRecord> GetEnabledNodeAsync(JsonGatewayDatabase database, string nodeId)
    {
        return await database.ReadAsync(db => db.Nodes.FirstOrDefault(node => node.Id == nodeId && node.Enabled))
            ?? throw new InvalidOperationException("节点不存在或已禁用。");
    }

    private static async Task<GatewayJobRecord> GetJobAsync(JsonGatewayDatabase database, string jobId)
    {
        return await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == jobId))
            ?? throw new InvalidOperationException("任务不存在。");
    }

    private static void EnsurePlatformAllowed(GatewayNodeRecord node, RemoteNodeInfo remote, string buildPlatform)
    {
        List<string> platforms = node.Platforms.Count > 0 ? node.Platforms : remote.Platforms;
        if (platforms.Count == 0)
        {
            return;
        }

        if (!platforms.Contains(buildPlatform, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"节点 {node.Name} 不支持 {buildPlatform} 打包。");
        }
    }

    private static List<string> NormalizePlatforms(IEnumerable<string>? platforms)
    {
        return (platforms ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => value is "ios" or "android")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        string value = baseUrl.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("节点地址必须是 http 或 https URL。");
        }

        return value.TrimEnd('/');
    }

    private static string Required(string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{field} 不能为空。")
            : value.Trim();
    }

    private static string? FileNameFromContentDisposition(ContentDispositionHeaderValue? contentDisposition)
    {
        string? value = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim('"');
    }
}
