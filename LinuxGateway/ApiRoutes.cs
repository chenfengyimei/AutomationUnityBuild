using System.Net.Http.Headers;
using System.Text.Json;
using LinuxGateway.Persistence;
using LinuxGateway.Security;
using LinuxGateway.Services;

namespace LinuxGateway;

public static class ApiRoutes
{
    private static readonly JsonSerializerOptions CamelizeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { ok = true, time = DateTimeOffset.Now }));
        app.MapGet("/api/dashboard", DashboardAsync);
        app.MapGet("/api/events", EventsAsync);
        app.MapPost("/api/auth/login", LoginAsync);
        app.MapPost("/api/auth/logout", LogoutAsync);
        app.MapGet("/api/me", MeAsync);
        app.MapPost("/api/me/password", ChangeMyPasswordAsync);
        app.MapGet("/api/users", ListUsersAsync);
        app.MapPost("/api/users", CreateUserAsync);
        app.MapPut("/api/users/{userId}", UpdateUserAsync);
        app.MapDelete("/api/users/{userId}", DeleteUserAsync);
        app.MapGet("/api/audit", ListAuditAsync);
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

    private static async Task<IResult> DashboardAsync(HttpContext context, GatewayAuthService auth, JsonGatewayDatabase database, LinuxGatewayOptions options)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();
        return Results.Ok(await DashboardSnapshotAsync(database, options));
    }

    private static async Task EventsAsync(HttpContext context, GatewayAuthService auth, JsonGatewayDatabase database, LinuxGatewayOptions options)
    {
        if (!await IsAuthenticatedAsync(context, auth))
        {
            await ApiDiagnostics.Unauthorized(context).ExecuteAsync(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.ContentType = "text/event-stream; charset=utf-8";

        try
        {
            await WriteSseEventAsync(context, "dashboard", await DashboardSnapshotAsync(database, options));
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(context.RequestAborted))
            {
                await WriteSseEventAsync(context, "heartbeat", new { time = DateTimeOffset.Now });
                await WriteSseEventAsync(context, "dashboard", await DashboardSnapshotAsync(database, options));
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
    }

    private static async Task<object> DashboardSnapshotAsync(JsonGatewayDatabase database, LinuxGatewayOptions options)
    {
        return await database.ReadAsync(db => new
        {
            nodes = db.Nodes.OrderBy(node => node.Name).Select(ToStoredNodeView).ToList(),
            jobs = db.Jobs.OrderByDescending(job => job.CreatedAt).Take(100).ToList(),
            settings = new
            {
                options.DataRoot,
                options.PublicBaseUrl
            }
        });
    }

    private static async Task WriteSseEventAsync(HttpContext context, string eventName, object data)
    {
        string json = JsonSerializer.Serialize(data, CamelizeOptions);
        await context.Response.WriteAsync($"event: {eventName}\n", context.RequestAborted);
        await context.Response.WriteAsync($"data: {json}\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    private static readonly Dictionary<string, LoginAttempt> LoginAttempts = new(StringComparer.Ordinal);
    private static readonly object LoginAttemptsLock = new();
    private static readonly TimeSpan LoginAttemptWindow = TimeSpan.FromMinutes(5);
    private const int MaxLoginAttempts = 10;
    private const int MaxTrackedLoginAttempts = 4096;
    private const int TrimmedLoginAttempts = 3072;
    private const int MaxLoginUserNameLength = 128;

    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext context, GatewayAuthService auth)
    {
        string limiterKey = LoginLimiterKey(context, request.UserName);
        if (!IsLoginAllowed(limiterKey))
        {
            return ApiDiagnostics.Problem(
                context,
                StatusCodes.Status429TooManyRequests,
                "请求过于频繁",
                "登录失败次数过多，请稍后再试。",
                "rate_limited");
        }

        GatewayUserRecord? user = await auth.ValidateLoginAsync(request.UserName, request.Password);
        if (user is null)
        {
            RecordLoginFailure(limiterKey);
            return ApiDiagnostics.Unauthorized(context, "Invalid user name or password.");
        }

        RecordLoginSuccess(limiterKey);
        string token = await auth.CreateSessionAsync(user);
        context.Response.Cookies.Append(GatewayAuthService.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Expires = DateTimeOffset.Now.AddDays(7)
        });
        return Results.Ok(GatewayAuthService.ToCurrentUser(user));
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

    private static async Task<IResult> ChangeMyPasswordAsync(
        GatewayChangePasswordRequest request,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database)
    {
        CurrentGatewayUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);

        try
        {
            string newPassword = Required(request.NewPassword, "newPassword");
            ValidatePassword(newPassword);
            await database.UpdateAsync(db =>
            {
                GatewayUserRecord user = db.Users.FirstOrDefault(user => user.Id == current.Id && user.Enabled)
                    ?? throw new UnauthorizedAccessException("Current user no longer exists or is disabled.");
                if (!PasswordHasher.Verify(request.CurrentPassword ?? "", user.PasswordHash))
                {
                    throw new UnauthorizedAccessException("Current password is incorrect.");
                }

                user.PasswordHash = PasswordHasher.Hash(newPassword);
                db.Sessions.RemoveAll(session => session.UserId == user.Id);
                GatewayAuthService.AddAudit(db, user.Id, user.UserName, "user.change-password", "user", user.Id, "User changed own password.");
            });

            context.Response.Cookies.Delete(GatewayAuthService.CookieName);
            return Results.Ok(new { ok = true });
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiDiagnostics.Forbidden(context, ex.Message);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> ListUsersAsync(HttpContext context, GatewayAuthService auth, JsonGatewayDatabase database)
    {
        CurrentGatewayUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

        return Results.Ok(await database.ReadAsync(db => db.Users
            .OrderBy(user => user.UserName)
            .Select(GatewayUserView)
            .ToList()));
    }

    private static async Task<IResult> CreateUserAsync(
        GatewayUserRequest request,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database)
    {
        CurrentGatewayUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

        try
        {
            string userName = NormalizeGatewayUserName(request.UserName);
            string displayName = Required(request.DisplayName, "displayName");
            string role = NormalizeGatewayRole(request.Role);
            string password = Required(request.Password, "password");
            ValidatePassword(password);

            GatewayUserRecord user = await database.UpdateAsync(db =>
            {
                if (db.Users.Any(user => string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("User name already exists.");
                }

                var user = new GatewayUserRecord
                {
                    Id = Ids.New("gusr"),
                    UserName = userName,
                    DisplayName = displayName,
                    Role = role,
                    PasswordHash = PasswordHasher.Hash(password),
                    Enabled = request.Enabled,
                    CreatedAt = DateTimeOffset.Now
                };
                db.Users.Add(user);
                GatewayAuthService.AddAudit(db, current.Id, current.UserName, "user.create", "user", user.Id, $"Created user {user.UserName} role={user.Role}.");
                return user;
            });

            return Results.Ok(GatewayUserView(user));
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UpdateUserAsync(
        string userId,
        GatewayUserRequest request,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database)
    {
        CurrentGatewayUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

        try
        {
            string userName = NormalizeGatewayUserName(request.UserName);
            string displayName = Required(request.DisplayName, "displayName");
            string role = NormalizeGatewayRole(request.Role);
            string? newPassword = string.IsNullOrWhiteSpace(request.Password) ? null : request.Password.Trim();
            if (newPassword is not null) ValidatePassword(newPassword);

            GatewayUserRecord user = await database.UpdateAsync(db =>
            {
                GatewayUserRecord user = db.Users.FirstOrDefault(user => user.Id == userId)
                    ?? throw new FileNotFoundException("User does not exist.");
                if (db.Users.Any(other => other.Id != user.Id && string.Equals(other.UserName, userName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("User name already exists.");
                }

                EnsureGatewayAdminInvariant(db, user, userName, role, request.Enabled);
                bool disabling = user.Enabled && !request.Enabled;
                bool passwordChanged = newPassword is not null;

                user.UserName = userName;
                user.DisplayName = displayName;
                user.Role = role;
                user.Enabled = request.Enabled;
                if (newPassword is not null)
                {
                    user.PasswordHash = PasswordHasher.Hash(newPassword);
                }

                if (disabling || passwordChanged)
                {
                    db.Sessions.RemoveAll(session => session.UserId == user.Id);
                }

                GatewayAuthService.AddAudit(db, current.Id, current.UserName, "user.update", "user", user.Id, $"Updated user {user.UserName} role={user.Role} enabled={user.Enabled}.");
                return user;
            });

            return Results.Ok(GatewayUserView(user));
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DeleteUserAsync(
        string userId,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database)
    {
        CurrentGatewayUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

        try
        {
            GatewayUserRecord user = await database.UpdateAsync(db =>
            {
                GatewayUserRecord user = db.Users.FirstOrDefault(user => user.Id == userId)
                    ?? throw new FileNotFoundException("User does not exist.");
                EnsureGatewayAdminInvariant(db, user, user.UserName, user.Role, enabled: false);
                user.Enabled = false;
                db.Sessions.RemoveAll(session => session.UserId == user.Id);
                GatewayAuthService.AddAudit(db, current.Id, current.UserName, "user.disable", "user", user.Id, $"Disabled user {user.UserName}.");
                return user;
            });

            return Results.Ok(GatewayUserView(user));
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> ListAuditAsync(HttpContext context, GatewayAuthService auth, JsonGatewayDatabase database)
    {
        CurrentGatewayUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

        return Results.Ok(await database.ReadAsync(db => db.AuditLogs
            .OrderByDescending(item => item.CreatedAt)
            .Take(300)
            .ToList()));
    }

    private static async Task<IResult> ListNodesAsync(
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database)
    {
        if (!await IsAuthenticatedAsync(context, auth)) return Results.Unauthorized();

        return Results.Ok(await database.ReadAsync(db => db.Nodes
            .OrderBy(node => node.Name)
            .Select(ToStoredNodeView)
            .ToList()));
    }

    private static async Task<IResult> SaveNodeAsync(
        GatewayNodeRequest request,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database,
        NodeRefreshService refresher)
    {
        CurrentGatewayUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

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
                GatewayAuthService.AddAudit(db, current.Id, current.UserName, "node.save", "node", existing.Id, $"Saved node {existing.Name} enabled={existing.Enabled}.");
                return existing;
            });

            await refresher.RefreshNodeAsync(node.Id, context.RequestAborted);
            GatewayNodeRecord? refreshed = await database.ReadAsync(db => db.Nodes.FirstOrDefault(item => item.Id == node.Id));
            return Results.Ok(ToStoredNodeView(refreshed ?? node));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> RefreshNodeAsync(
        string nodeId,
        HttpContext context,
        GatewayAuthService auth,
        JsonGatewayDatabase database,
        NodeRefreshService refresher)
    {
        CurrentGatewayUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

        GatewayNodeRecord? node = await database.ReadAsync(db => db.Nodes.FirstOrDefault(node => node.Id == nodeId));
        if (node is null) return ApiDiagnostics.NotFound(context);
        await refresher.RefreshNodeAsync(node.Id, context.RequestAborted);
        await database.UpdateAsync(db => GatewayAuthService.AddAudit(db, current.Id, current.UserName, "node.refresh", "node", node.Id, $"Refreshed node {node.Name}."));
        GatewayNodeRecord? refreshed = await database.ReadAsync(db => db.Nodes.FirstOrDefault(item => item.Id == node.Id));
        return Results.Ok(ToStoredNodeView(refreshed ?? node));
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
        CurrentGatewayUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!GatewayAuthService.CanBuild(current)) return ApiDiagnostics.Forbidden(context);

        try
        {
            string clientRequestId = NormalizeClientRequestId(request.ClientRequestId);
            if (!string.IsNullOrWhiteSpace(clientRequestId))
            {
                GatewayJobRecord? existingJob = await database.ReadAsync(db => db.Jobs
                    .OrderByDescending(job => job.CreatedAt)
                    .FirstOrDefault(job =>
                        string.Equals(job.ClientRequestId, clientRequestId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(job.RequestedByUserId, current.Id, StringComparison.OrdinalIgnoreCase)));
                if (existingJob is not null)
                {
                    return Results.Ok(existingJob);
                }
            }

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
                clientRequestId,
                request.Notes);
            RemoteBuildJobRecord remoteJob = await client.StartBuildAsync(node, remoteRequest);

            GatewayJobRecord gatewayJob = await database.UpdateAsync(db =>
            {
                if (!string.IsNullOrWhiteSpace(clientRequestId))
                {
                    GatewayJobRecord? existing = db.Jobs
                        .OrderByDescending(job => job.CreatedAt)
                        .FirstOrDefault(job =>
                            string.Equals(job.ClientRequestId, clientRequestId, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(job.RequestedByUserId, current.Id, StringComparison.OrdinalIgnoreCase));
                    if (existing is not null)
                    {
                        return existing;
                    }
                }

                var job = new GatewayJobRecord
                {
                    Id = Ids.New("gwjob"),
                    NodeId = node.Id,
                    NodeName = node.Name,
                    RemoteJobId = remoteJob.Id,
                    RequestedByUserId = current.Id,
                    RequestedByUserName = current.UserName,
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    ConfigId = config.Id,
                    ConfigName = config.Name,
                    BuildPlatform = config.BuildPlatform,
                    Branch = remoteJob.Branch,
                    BuildNumber = remoteJob.BuildNumber,
                    DryRun = remoteJob.DryRun,
                    ClientRequestId = clientRequestId,
                    Status = remoteJob.Status,
                    Error = remoteJob.Error,
                    CreatedAt = DateTimeOffset.Now,
                    UpdatedAt = DateTimeOffset.Now
                };
                db.Jobs.Add(job);
                GatewayAuthService.AddAudit(db, current.Id, current.UserName, "build.start", "build", job.Id, $"Started build {project.Name}/{config.Name} on {node.Name}.");
                return job;
            });

            return Results.Ok(gatewayJob);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return ApiDiagnostics.ClientError(context, ex);
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
        GatewayNodeRecord node = await GetNodeAsync(database, job.NodeId);
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
        GatewayNodeRecord node = await GetNodeAsync(database, job.NodeId);
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
        GatewayNodeRecord node = await GetNodeAsync(database, job.NodeId);
        HttpResponseMessage response = await client.DownloadArtifactAsync(node, artifactId);
        Stream stream = await response.Content.ReadAsStreamAsync();
        string fileName = FileNameFromContentDisposition(response.Content.Headers.ContentDisposition) ?? artifactId;
        string contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        return Results.Stream(new DisposingStream(stream, response), contentType, fileName);
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

    private static object GatewayUserView(GatewayUserRecord user)
    {
        return new
        {
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Role,
            user.Enabled,
            user.CreatedAt
        };
    }

    private static string NormalizeGatewayUserName(string? value)
    {
        string userName = Required(value, "userName").ToLowerInvariant();
        if (userName.Length is < 3 or > 64 ||
            userName.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-')))
        {
            throw new InvalidOperationException("User name must be 3-64 characters and contain only letters, numbers, dot, underscore, or hyphen.");
        }

        return userName;
    }

    private static string NormalizeGatewayRole(string? value)
    {
        string role = Required(value, "role");
        if (string.Equals(role, GatewayRoles.Admin, StringComparison.OrdinalIgnoreCase)) return GatewayRoles.Admin;
        if (string.Equals(role, GatewayRoles.Builder, StringComparison.OrdinalIgnoreCase)) return GatewayRoles.Builder;
        if (string.Equals(role, GatewayRoles.Viewer, StringComparison.OrdinalIgnoreCase)) return GatewayRoles.Viewer;
        throw new InvalidOperationException("Role must be Admin, Builder, or Viewer.");
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length is < 8 or > 256)
        {
            throw new InvalidOperationException("Password must be 8-256 characters.");
        }
    }

    private static void EnsureGatewayAdminInvariant(GatewayDatabase db, GatewayUserRecord target, string nextUserName, string nextRole, bool enabled)
    {
        if (IsRootAdmin(target))
        {
            if (!string.Equals(nextUserName, "admin", StringComparison.OrdinalIgnoreCase) ||
                !enabled ||
                !string.Equals(nextRole, GatewayRoles.Admin, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Root admin account cannot be renamed, disabled, or demoted.");
            }
        }

        bool targetWillRemainAdmin = enabled && string.Equals(nextRole, GatewayRoles.Admin, StringComparison.Ordinal);
        if (targetWillRemainAdmin)
        {
            return;
        }

        bool anotherEnabledAdmin = db.Users.Any(user =>
            user.Id != target.Id &&
            user.Enabled &&
            string.Equals(user.Role, GatewayRoles.Admin, StringComparison.Ordinal));
        if (!anotherEnabledAdmin)
        {
            throw new InvalidOperationException("At least one enabled administrator is required.");
        }
    }

    private static bool IsRootAdmin(GatewayUserRecord user)
    {
        return string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClientInputError(Exception ex)
    {
        return ex is InvalidOperationException or ArgumentException or FileNotFoundException;
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
            LastError = node.LastError,
            Remote = node.LastRemote
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

    private static async Task<GatewayNodeRecord> GetNodeAsync(JsonGatewayDatabase database, string nodeId)
    {
        return await database.ReadAsync(db => db.Nodes.FirstOrDefault(node => node.Id == nodeId))
            ?? throw new InvalidOperationException("节点不存在。");
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

    private static string NormalizeClientRequestId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        string normalized = value.Trim();
        if (normalized.Length > 128)
        {
            throw new InvalidOperationException("Client Request ID 不能超过 128 个字符。");
        }

        if (normalized.Any(ch => char.IsControl(ch) || char.IsWhiteSpace(ch)))
        {
            throw new InvalidOperationException("Client Request ID 不能包含空白或控制字符。");
        }

        return normalized;
    }

    private static string? FileNameFromContentDisposition(ContentDispositionHeaderValue? contentDisposition)
    {
        string? value = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim('"');
    }

    private static string LoginLimiterKey(HttpContext context, string? userName)
    {
        string remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        string normalizedUserName = (userName ?? "").Trim().ToLowerInvariant();
        if (normalizedUserName.Length > MaxLoginUserNameLength)
        {
            normalizedUserName = normalizedUserName[..MaxLoginUserNameLength];
        }

        return $"{remoteAddress}|{normalizedUserName}";
    }

    private static bool IsLoginAllowed(string key)
    {
        lock (LoginAttemptsLock)
        {
            DateTimeOffset now = DateTimeOffset.Now;
            PruneLoginAttempts(now);
            if (!LoginAttempts.TryGetValue(key, out LoginAttempt? attempt))
            {
                return true;
            }

            if (now - attempt.WindowStart >= LoginAttemptWindow)
            {
                LoginAttempts.Remove(key);
                return true;
            }

            return attempt.Failures < MaxLoginAttempts;
        }
    }

    private static void RecordLoginFailure(string key)
    {
        lock (LoginAttemptsLock)
        {
            DateTimeOffset now = DateTimeOffset.Now;
            PruneLoginAttempts(now);
            if (!LoginAttempts.TryGetValue(key, out LoginAttempt? attempt) ||
                now - attempt.WindowStart >= LoginAttemptWindow)
            {
                attempt = new LoginAttempt { WindowStart = now, Failures = 0 };
                LoginAttempts[key] = attempt;
            }

            attempt.Failures++;
            PruneLoginAttempts(now);
        }
    }

    private static void RecordLoginSuccess(string key)
    {
        lock (LoginAttemptsLock)
        {
            LoginAttempts.Remove(key);
        }
    }

    private static void PruneLoginAttempts(DateTimeOffset now)
    {
        foreach (string key in LoginAttempts
                     .Where(pair => now - pair.Value.WindowStart >= LoginAttemptWindow)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            LoginAttempts.Remove(key);
        }

        if (LoginAttempts.Count <= MaxTrackedLoginAttempts)
        {
            return;
        }

        int removeCount = LoginAttempts.Count - TrimmedLoginAttempts;
        foreach (string key in LoginAttempts
                     .OrderBy(pair => pair.Value.WindowStart)
                     .Take(removeCount)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            LoginAttempts.Remove(key);
        }
    }

    private sealed class LoginAttempt
    {
        public DateTimeOffset WindowStart { get; set; }
        public int Failures { get; set; }
    }

    private sealed class DisposingStream : Stream
    {
        private readonly Stream _inner;
        private readonly IDisposable _owner;

        public DisposingStream(Stream inner, IDisposable owner)
        {
            _inner = inner;
            _owner = owner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override void Flush() => _inner.Flush();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _owner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync();
            _owner.Dispose();
            await base.DisposeAsync();
        }
    }
}
