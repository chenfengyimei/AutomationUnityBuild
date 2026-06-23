using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuildServer.Persistence;
using BuildServer.Security;
using BuildServer.Services;

namespace BuildServer;

public static class ApiRoutes
{
    private static readonly JsonSerializerOptions CamelizeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions IndentedCamelizeOptions = new()
    {
        WriteIndented = true,
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

        app.MapGet("/api/projects", ListProjectsAsync);
        app.MapPost("/api/projects", CreateProjectAsync);
        app.MapGet("/api/configs", ListConfigsAsync);
        app.MapPost("/api/configs", CreateConfigAsync);
        app.MapGet("/api/configs/{configId}/file", GetConfigFileAsync);
        app.MapPut("/api/configs/{configId}", UpdateConfigAsync);
        app.MapDelete("/api/configs/{configId}", DeleteConfigAsync);
        app.MapPost("/api/config-files", CreateConfigFileAsync);
        app.MapPut("/api/config-files/{configId}", UpdateConfigFileAsync);

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

    private static async Task<IResult> DashboardAsync(HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(await DashboardSnapshotAsync(database, options));
    }

    private static async Task EventsAsync(HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null)
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

    private static async Task<object> DashboardSnapshotAsync(JsonDatabase database, BuildServerOptions options)
    {
        return await database.ReadAsync(db => new
        {
            projects = db.Projects.OrderBy(project => project.Name).ToList(),
            configs = db.Configs.OrderBy(config => config.Name).ToList(),
            jobs = db.Jobs.OrderByDescending(job => job.CreatedAt).Take(100).ToList(),
            workers = db.Workers.OrderBy(worker => worker.Name).ToList(),
            settings = new
            {
                options.DataRoot,
                options.WorkerName,
                options.PublicBaseUrl,
                options.RetentionDays,
                options.MaxArtifactBytes,
                ConfigRoot = options.AllowedConfigRoots.FirstOrDefault() ?? ""
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

    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext context, AuthService auth, LoginRateLimiter limiter)
    {
        string limiterKey = $"{context.Connection.RemoteIpAddress}|{request.UserName}";
        if (!limiter.IsAllowed(limiterKey))
        {
            return ApiDiagnostics.Problem(
                context,
                StatusCodes.Status429TooManyRequests,
                "请求过于频繁",
                "登录失败次数过多，请稍后再试。",
                "rate_limited");
        }

        UserRecord? user = await auth.ValidateLoginAsync(request.UserName, request.Password);
        if (user is null)
        {
            limiter.RecordFailure(limiterKey);
            return ApiDiagnostics.Unauthorized(
                context,
                "账号或密码错误。请确认账号是 admin，只复制 initial-admin.txt 里 admin password: 后面的密码，并且这个文件来自当前服务的数据目录。");
        }

        limiter.RecordSuccess(limiterKey);
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

    private static async Task<IResult> ChangeMyPasswordAsync(ChangePasswordRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);

        try
        {
            string newPassword = Required(request.NewPassword, "新密码");
            ValidatePassword(newPassword);
            await database.UpdateAsync(db =>
            {
                UserRecord user = db.Users.FirstOrDefault(user => user.Id == current.Id && user.Enabled)
                    ?? throw new UnauthorizedAccessException("当前用户不存在或已禁用。");
                if (!PasswordHasher.Verify(request.CurrentPassword ?? "", user.PasswordHash))
                {
                    throw new UnauthorizedAccessException("当前密码不正确。");
                }

                user.PasswordHash = PasswordHasher.Hash(newPassword);
                db.Sessions.RemoveAll(session => session.UserId == user.Id);
                AuthService.AddAudit(db, user.Id, user.UserName, "user.change-password", "user", user.Id, "用户修改自己的密码。");
            });

            context.Response.Cookies.Delete(AuthService.CookieName);
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

    private static async Task<IResult> ListUsersAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!AuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

        return Results.Ok(await database.ReadAsync(db => db.Users
            .OrderBy(user => user.UserName)
            .Select(UserView)
            .ToList()));
    }

    private static async Task<IResult> CreateUserAsync(UserRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!AuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

        try
        {
            string userName = NormalizeUserName(request.UserName);
            string displayName = Required(request.DisplayName, "显示名称");
            string role = NormalizeHumanRole(request.Role);
            string password = Required(request.Password, "密码");
            ValidatePassword(password);

            UserRecord user = await database.UpdateAsync(db =>
            {
                if (db.Users.Any(user => string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("用户名已存在。");
                }

                var user = new UserRecord
                {
                    Id = Ids.New("usr"),
                    UserName = userName,
                    DisplayName = displayName,
                    Role = role,
                    PasswordHash = PasswordHasher.Hash(password),
                    Enabled = request.Enabled,
                    CreatedAt = DateTimeOffset.Now
                };
                db.Users.Add(user);
                AuthService.AddAudit(db, current.Id, current.UserName, "user.create", "user", user.Id, $"创建用户 {user.UserName} role={user.Role}");
                return user;
            });

            return Results.Ok(UserView(user));
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UpdateUserAsync(string userId, UserRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!AuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

        try
        {
            string userName = NormalizeUserName(request.UserName);
            string displayName = Required(request.DisplayName, "显示名称");
            string role = NormalizeHumanRole(request.Role);
            string? newPassword = string.IsNullOrWhiteSpace(request.Password) ? null : request.Password.Trim();
            if (newPassword is not null) ValidatePassword(newPassword);

            UserRecord user = await database.UpdateAsync(db =>
            {
                UserRecord user = db.Users.FirstOrDefault(user => user.Id == userId)
                    ?? throw new FileNotFoundException("用户不存在。");
                if (db.Users.Any(other => other.Id != user.Id && string.Equals(other.UserName, userName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("用户名已存在。");
                }

                EnsureAdminInvariant(db, user, userName, role, request.Enabled);
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

                AuthService.AddAudit(db, current.Id, current.UserName, "user.update", "user", user.Id, $"更新用户 {user.UserName} role={user.Role} enabled={user.Enabled}");
                return user;
            });

            return Results.Ok(UserView(user));
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DeleteUserAsync(string userId, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? current = await auth.GetUserAsync(context);
        if (current is null) return ApiDiagnostics.Unauthorized(context);
        if (!AuthService.IsAdmin(current)) return ApiDiagnostics.Forbidden(context);

        try
        {
            UserRecord user = await database.UpdateAsync(db =>
            {
                UserRecord user = db.Users.FirstOrDefault(user => user.Id == userId)
                    ?? throw new FileNotFoundException("用户不存在。");
                EnsureAdminInvariant(db, user, user.UserName, user.Role, enabled: false);
                user.Enabled = false;
                db.Sessions.RemoveAll(session => session.UserId == user.Id);
                AuthService.AddAudit(db, current.Id, current.UserName, "user.disable", "user", user.Id, $"禁用用户 {user.UserName}");
                return user;
            });

            return Results.Ok(UserView(user));
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> ListProjectsAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(await database.ReadAsync(db => db.Projects.OrderBy(project => project.Name).ToList()));
    }

    private static async Task<IResult> CreateProjectAsync(ProjectRequest request, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            ProjectRecord project = await database.UpdateAsync(db =>
        {
            var project = new ProjectRecord
            {
                Id = Ids.New("prj"),
                Name = Required(request.Name, "项目名称"),
                RepositoryUrl = ValidateGitUrl(Required(request.RepositoryUrl, "Git 仓库"), options),
                DefaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch) ? "main" : request.DefaultBranch.Trim(),
                AllowedBranches = request.AllowedBranches?.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? ["main"],
                WorkspaceRoot = ValidatePathUnderAllowedRoots(Required(request.WorkspaceRoot, "工作区目录"), options.AllowedWorkspaceRoots, "工作区目录"),
                ArtifactsRoot = ValidatePathUnderAllowedRoots(Required(request.ArtifactsRoot, "产物目录"), options.AllowedArtifactsRoots, "产物目录"),
                DefaultBuildPlatform = NormalizeBuildPlatform(request.DefaultBuildPlatform),
                Description = request.Description ?? "",
                CreatedAt = DateTimeOffset.Now
            };
            db.Projects.Add(project);
            AuthService.AddAudit(db, user.Id, user.UserName, "project.create", "project", project.Id, $"创建项目 {project.Name}");
            return project;
        });

            return Results.Ok(project);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
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

    private static async Task<IResult> CreateConfigAsync(BuildConfigRequest request, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            BuildConfigRecord config = await database.UpdateAsync(db =>
        {
            if (!db.Projects.Any(project => project.Id == request.ProjectId && project.Enabled))
            {
                throw new InvalidOperationException("项目不存在或已禁用。");
            }

            string configPath = ValidatePathUnderAllowedRoots(Required(request.ConfigPath, "配置文件路径"), options.AllowedConfigRoots, "配置文件路径");
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"配置文件不存在: {configPath}");
            }

            string buildPlatform = string.IsNullOrWhiteSpace(request.BuildPlatform)
                ? DetectBuildPlatformFromConfig(configPath)
                : NormalizeBuildPlatform(request.BuildPlatform);

            var config = new BuildConfigRecord
            {
                Id = Ids.New("cfg"),
                ProjectId = request.ProjectId,
                Name = Required(request.Name, "配置名称"),
                BuildPlatform = buildPlatform,
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
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> CreateConfigFileAsync(BuildConfigFileRequest request, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            BuildConfigRecord config = await database.UpdateAsync(db =>
            {
                ProjectRecord project = db.Projects.FirstOrDefault(project => project.Id == request.ProjectId && project.Enabled)
                    ?? throw new InvalidOperationException("项目不存在或已禁用。");

                string configName = Required(request.Name, "配置名称");
                string buildPlatform = NormalizeBuildPlatform(request.BuildPlatform ?? project.DefaultBuildPlatform);
                string configRoot = options.AllowedConfigRoots.FirstOrDefault()
                    ?? throw new InvalidOperationException("服务端没有配置允许的配置文件目录。");
                string fileName = SafeConfigFileName(request.FileName, configName, buildPlatform);
                string configPath = ValidatePathUnderAllowedRoots(Path.Combine(configRoot, fileName), options.AllowedConfigRoots, "配置文件路径");
                if (File.Exists(configPath) && !request.OverwriteExisting)
                {
                    throw new InvalidOperationException($"配置文件已存在: {configPath}");
                }

                string? dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(
                    configPath,
                    BuildConfigJson(project, request, configName, buildPlatform).ToJsonString(IndentedCamelizeOptions) + Environment.NewLine);

                BuildConfigRecord? existingConfig = db.Configs.FirstOrDefault(config =>
                    config.ProjectId == project.Id &&
                    string.Equals(config.ConfigPath, configPath, StringComparison.OrdinalIgnoreCase));
                if (existingConfig is not null)
                {
                    existingConfig.Name = configName;
                    existingConfig.BuildPlatform = buildPlatform;
                    existingConfig.AllowMcpBuild = request.AllowMcpBuild;
                    existingConfig.Enabled = true;
                    AuthService.AddAudit(db, user.Id, user.UserName, "config-file.update", "config", existingConfig.Id, $"更新配置文件 {configPath}");
                    return existingConfig;
                }

                var config = new BuildConfigRecord
                {
                    Id = Ids.New("cfg"),
                    ProjectId = project.Id,
                    Name = configName,
                    BuildPlatform = buildPlatform,
                    ConfigPath = configPath,
                    AllowMcpBuild = request.AllowMcpBuild,
                    CreatedAt = DateTimeOffset.Now
                };
                db.Configs.Add(config);
                AuthService.AddAudit(db, user.Id, user.UserName, "config-file.create", "config", config.Id, $"创建配置文件 {configPath}");
                return config;
            });

            return Results.Ok(config);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> GetConfigFileAsync(string configId, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            BuildConfigRecord config = await database.ReadAsync(db => db.Configs.FirstOrDefault(config => config.Id == configId))
                ?? throw new FileNotFoundException("配置不存在。");
            string configPath = ValidatePathUnderAllowedRoots(config.ConfigPath, options.AllowedConfigRoots, "配置文件路径");
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"配置文件不存在: {configPath}");
            }

            JsonNode content = JsonNode.Parse(File.ReadAllText(configPath))
                ?? throw new InvalidOperationException($"配置文件不是有效 JSON: {configPath}");
            return Results.Ok(new { config, content });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UpdateConfigAsync(string configId, BuildConfigRequest request, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            BuildConfigRecord updatedConfig = await database.UpdateAsync(db =>
            {
                BuildConfigRecord record = db.Configs.FirstOrDefault(config => config.Id == configId)
                    ?? throw new FileNotFoundException("配置不存在。");
                if (!db.Projects.Any(project => project.Id == request.ProjectId && project.Enabled))
                {
                    throw new InvalidOperationException("项目不存在或已禁用。");
                }

                string configPath = ValidatePathUnderAllowedRoots(Required(request.ConfigPath, "配置文件路径"), options.AllowedConfigRoots, "配置文件路径");
                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"配置文件不存在: {configPath}");
                }

                string buildPlatform = string.IsNullOrWhiteSpace(request.BuildPlatform)
                    ? DetectBuildPlatformFromConfig(configPath)
                    : NormalizeBuildPlatform(request.BuildPlatform);
                EnsureConfigPathUnique(db, record.Id, request.ProjectId, configPath);

                record.ProjectId = request.ProjectId;
                record.Name = Required(request.Name, "配置名称");
                record.BuildPlatform = buildPlatform;
                record.ConfigPath = configPath;
                record.AllowMcpBuild = request.AllowMcpBuild;
                record.Enabled = true;
                AuthService.AddAudit(db, user.Id, user.UserName, "config.update", "config", record.Id, $"更新配置 {record.Name}");
                return record;
            });

            return Results.Ok(updatedConfig);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UpdateConfigFileAsync(string configId, BuildConfigFileRequest request, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            BuildConfigRecord updatedConfig = await database.UpdateAsync(db =>
            {
                BuildConfigRecord record = db.Configs.FirstOrDefault(config => config.Id == configId)
                    ?? throw new FileNotFoundException("配置不存在。");
                ProjectRecord project = db.Projects.FirstOrDefault(project => project.Id == request.ProjectId && project.Enabled)
                    ?? throw new InvalidOperationException("项目不存在或已禁用。");

                string configName = Required(request.Name, "配置名称");
                string buildPlatform = NormalizeBuildPlatform(request.BuildPlatform ?? project.DefaultBuildPlatform);
                string configPath = ValidatePathUnderAllowedRoots(record.ConfigPath, options.AllowedConfigRoots, "配置文件路径");

                string? dir2 = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(dir2)) Directory.CreateDirectory(dir2);
                File.WriteAllText(
                    configPath,
                    BuildConfigJson(project, request, configName, buildPlatform).ToJsonString(IndentedCamelizeOptions) + Environment.NewLine);

                record.ProjectId = project.Id;
                record.Name = configName;
                record.BuildPlatform = buildPlatform;
                record.AllowMcpBuild = request.AllowMcpBuild;
                record.Enabled = true;
                AuthService.AddAudit(db, user.Id, user.UserName, "config-file.update", "config", record.Id, $"更新配置文件 {configPath}");
                return record;
            });

            return Results.Ok(updatedConfig);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DeleteConfigAsync(string configId, bool deleteFile, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            bool deletedFile = false;
            BuildConfigRecord deletedConfig = await database.UpdateAsync(db =>
            {
                BuildConfigRecord record = db.Configs.FirstOrDefault(config => config.Id == configId)
                    ?? throw new FileNotFoundException("配置不存在。");
                if (db.Jobs.Any(job => job.ConfigId == record.Id && (job.Status == BuildStatuses.Queued || job.Status == BuildStatuses.Running)))
                {
                    throw new InvalidOperationException("这个配置还有排队中或运行中的任务，不能删除。");
                }

                if (deleteFile)
                {
                    string configPath = ValidatePathUnderAllowedRoots(record.ConfigPath, options.AllowedConfigRoots, "配置文件路径");
                    if (db.Configs.Any(other => other.Id != record.Id && string.Equals(Path.GetFullPath(BuildServerEnvironment.ExpandHome(other.ConfigPath)), configPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException("还有其他配置引用同一个 JSON 文件，不能同时删除文件。");
                    }

                    if (!string.Equals(Path.GetExtension(configPath), ".json", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("只能删除 .json 配置文件。");
                    }

                    if (File.Exists(configPath))
                    {
                        File.Delete(configPath);
                        deletedFile = true;
                    }
                }

                db.Configs.Remove(record);
                AuthService.AddAudit(db, user.Id, user.UserName, "config.delete", "config", record.Id, $"删除配置 {record.Name}, deleteFile={deleteFile}");
                return record;
            });

            return Results.Ok(new { deleted = true, deletedFile, config = deletedConfig });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
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

        try
        {
            BuildJobRecord job = await queue.EnqueueAsync(request, user, BuildSources.Web);
            return Results.Ok(job);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
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

    private static async Task<IResult> GetJobLogAsync(string jobId, HttpContext context, AuthService auth, JsonDatabase database, int? lines, bool full = false)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == jobId));
        if (job is null) return Results.NotFound();
        if (!File.Exists(job.WorkerLogPath)) return Results.Ok("");
        string log = full
            ? File.ReadAllText(job.WorkerLogPath)
            : Tail(job.WorkerLogPath, Math.Clamp(lines ?? 300, 20, 2000));
        return Results.Text(log, "text/plain; charset=utf-8");
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
            options.MaxArtifactBytes,
            ConfigRoot = options.AllowedConfigRoots.FirstOrDefault() ?? ""
        });
    }

    private static bool IsClientInputError(Exception ex)
    {
        return ex is InvalidOperationException or FileNotFoundException or ArgumentException;
    }

    private static object UserView(UserRecord user)
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

    private static string NormalizeUserName(string? value)
    {
        string userName = Required(value, "用户名");
        if (userName.Length is < 3 or > 64)
        {
            throw new InvalidOperationException("用户名长度必须在 3 到 64 个字符之间。");
        }

        if (!userName.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-'))
        {
            throw new InvalidOperationException("用户名只能包含字母、数字、点、下划线或短横线。");
        }

        return userName;
    }

    private static string NormalizeHumanRole(string? value)
    {
        string role = Required(value, "角色");
        string[] allowed = [Roles.Admin, Roles.ProjectOwner, Roles.Builder, Roles.Viewer];
        string? normalized = allowed.FirstOrDefault(item => string.Equals(item, role, StringComparison.OrdinalIgnoreCase));
        if (normalized is null)
        {
            throw new InvalidOperationException($"角色只能是 {string.Join(", ", allowed)}。");
        }

        return normalized;
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 8)
        {
            throw new InvalidOperationException("密码至少需要 8 个字符。");
        }

        if (password.Length > 256)
        {
            throw new InvalidOperationException("密码不能超过 256 个字符。");
        }
    }

    private static void EnsureAdminInvariant(BuildServerDatabase db, UserRecord targetUser, string newUserName, string newRole, bool enabled)
    {
        if (IsRootAdmin(targetUser))
        {
            if (!string.Equals(newUserName, "admin", StringComparison.OrdinalIgnoreCase) ||
                !enabled ||
                !string.Equals(newRole, Roles.Admin, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Root admin account cannot be renamed, disabled, or demoted.");
            }
        }

        bool targetWillBeEnabledAdmin = enabled && string.Equals(newRole, Roles.Admin, StringComparison.OrdinalIgnoreCase);
        if (targetWillBeEnabledAdmin)
        {
            return;
        }

        bool hasOtherEnabledAdmin = db.Users.Any(user =>
            user.Id != targetUser.Id &&
            user.Enabled &&
            string.Equals(user.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase));
        if (!hasOtherEnabledAdmin && targetUser.Enabled && string.Equals(targetUser.Role, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("不能禁用或降级最后一个启用的管理员。");
        }
    }

    private static bool IsRootAdmin(UserRecord user)
    {
        return string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject BuildConfigJson(ProjectRecord project, BuildConfigFileRequest request, string configName, string buildPlatform)
    {
        string projectDirectoryName = string.IsNullOrWhiteSpace(request.ProjectDirectoryName)
            ? DeriveProjectDirectoryName(project)
            : SafePathComponent(request.ProjectDirectoryName.Trim(), "仓库目录名");
        string unityProjectRelativePath = string.IsNullOrWhiteSpace(request.UnityProjectRelativePath)
            ? "."
            : request.UnityProjectRelativePath.Trim();
        string unityBuildMethod = string.IsNullOrWhiteSpace(request.UnityBuildMethod)
            ? DefaultUnityBuildMethod(buildPlatform)
            : request.UnityBuildMethod.Trim();
        string bundleVersion = string.IsNullOrWhiteSpace(request.BundleVersion) ? "1.0.0" : request.BundleVersion.Trim();
        string buildNumber = string.IsNullOrWhiteSpace(request.BuildNumber) ? "1" : request.BuildNumber.Trim();

        if (!request.SyncBundleVersionFromUnity && string.IsNullOrWhiteSpace(bundleVersion))
        {
            throw new InvalidOperationException("不同步 Unity 版本号时，必须填写 Bundle Version。");
        }

        JsonObject json = new()
        {
            ["configName"] = configName,
            ["buildPlatform"] = buildPlatform,
            ["repositoryUrl"] = project.RepositoryUrl,
            ["allowedRepositoryUrls"] = new JsonArray(project.RepositoryUrl),
            ["branch"] = string.IsNullOrWhiteSpace(project.DefaultBranch) ? "main" : project.DefaultBranch,
            ["workspaceRoot"] = project.WorkspaceRoot,
            ["allowedWorkspaceRoots"] = new JsonArray(project.WorkspaceRoot),
            ["projectDirectoryName"] = projectDirectoryName,
            ["unityProjectRelativePath"] = unityProjectRelativePath,
            ["unityVersion"] = (request.UnityVersion ?? "").Trim(),
            ["unityExecutablePath"] = (request.UnityExecutablePath ?? "").Trim(),
            ["unityBuildMethod"] = unityBuildMethod,
            ["artifactsRoot"] = project.ArtifactsRoot,
            ["allowedArtifactsRoots"] = new JsonArray(project.ArtifactsRoot),
            ["logsDirectory"] = "",
            ["bundleIdentifier"] = (request.BundleIdentifier ?? "").Trim(),
            ["productName"] = (request.ProductName ?? "").Trim(),
            ["bundleVersion"] = bundleVersion,
            ["syncBundleVersionFromUnity"] = request.SyncBundleVersionFromUnity,
            ["buildNumber"] = buildNumber,
            ["autoIncrementBuildNumber"] = request.AutoIncrementBuildNumber,
            ["resetRepository"] = true,
            ["preserveUnityLibraryOnReset"] = true,
            ["saveConfigSnapshot"] = true,
            ["environment"] = new JsonObject()
        };

        if (buildPlatform == BuildPlatforms.Android)
        {
            AddAndroidConfig(json, request);
        }
        else
        {
            AddIosConfig(json, request);
        }

        return json;
    }

    private static void AddIosConfig(JsonObject json, BuildConfigFileRequest request)
    {
        string exportMethod = ChoiceOrDefault(request.ExportMethod, ["development", "ad-hoc", "app-store", "enterprise"], "development", "Export Method");
        string signingStyle = ChoiceOrDefault(request.SigningStyle, ["automatic", "manual"], "automatic", "Signing Style");
        string teamId = (request.TeamId ?? "").Trim();
        string iosDeploymentTarget = (request.IosDeploymentTarget ?? "").Trim();
        string appStoreConnectApiKeyPath = (request.AppStoreConnectApiKeyPath ?? "").Trim();
        string appStoreConnectApiKeyId = (request.AppStoreConnectApiKeyId ?? "").Trim();
        string appStoreConnectApiIssuerId = (request.AppStoreConnectApiIssuerId ?? "").Trim();

        if (!string.IsNullOrWhiteSpace(teamId) && (teamId.Length != 10 || !teamId.All(char.IsLetterOrDigit)))
        {
            throw new InvalidOperationException("Apple Team ID 必须是 10 位，例如 ABCDE12345，不能填公司名称。");
        }

        if (!string.IsNullOrWhiteSpace(iosDeploymentTarget) && !Version.TryParse(iosDeploymentTarget, out _))
        {
            throw new InvalidOperationException("iOS Deployment Target 必须是版本号格式，例如 13.0。");
        }

        if (request.AppStoreConnectUploadEnabled)
        {
            if (!exportMethod.Equals("app-store", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("启用 App Store Connect 上传时，Export Method 必须选择 app-store。");
            }

            if (string.IsNullOrWhiteSpace(appStoreConnectApiKeyPath))
            {
                throw new InvalidOperationException("启用 App Store Connect 上传时，必须填写 API Key .p8 文件路径。");
            }

            if (string.IsNullOrWhiteSpace(appStoreConnectApiKeyId))
            {
                throw new InvalidOperationException("启用 App Store Connect 上传时，必须填写 API Key ID。");
            }

            if (string.IsNullOrWhiteSpace(appStoreConnectApiIssuerId))
            {
                throw new InvalidOperationException("启用 App Store Connect 上传时，必须填写 Issuer ID。");
            }
        }

        json["xcodeOutputDirectory"] = "";
        json["archivePath"] = "";
        json["exportPath"] = "";
        json["scheme"] = "Unity-iPhone";
        json["configuration"] = "Release";
        json["exportMethod"] = exportMethod;
        json["teamId"] = teamId;
        json["signingStyle"] = signingStyle;
        json["exportOptionsPlistPath"] = "";
        json["iosDeploymentTarget"] = iosDeploymentTarget;
        json["allowProvisioningUpdates"] = request.AllowProvisioningUpdates;
        json["cleanXcodeOutputBeforeBuild"] = true;
        json["useWorkspaceIfPresent"] = true;
        json["generateExportOptionsPlist"] = true;
        json["copyArchiveToOrganizer"] = request.CopyArchiveToOrganizer;
        json["compileBitcode"] = null;
        json["uploadSymbols"] = true;
        json["appStoreConnectUploadEnabled"] = request.AppStoreConnectUploadEnabled;
        json["appStoreConnectApiKeyPath"] = appStoreConnectApiKeyPath;
        json["appStoreConnectApiKeyId"] = appStoreConnectApiKeyId;
        json["appStoreConnectApiIssuerId"] = appStoreConnectApiIssuerId;
        json["xcodeBuildSettings"] = new JsonObject();
        json["provisioningProfiles"] = new JsonObject();
    }

    private static void AddAndroidConfig(JsonObject json, BuildConfigFileRequest request)
    {
        string androidBuildFormat = ChoiceOrDefault(request.AndroidBuildFormat, [AndroidBuildFormats.Apk, AndroidBuildFormats.Aab, AndroidBuildFormats.Both], AndroidBuildFormats.Aab, "Android Build Format");
        string googlePlayUploadArtifact = ChoiceOrDefault(request.GooglePlayUploadArtifact, [AndroidBuildFormats.Apk, AndroidBuildFormats.Aab, AndroidBuildFormats.Both], AndroidBuildFormats.Aab, "Google Play Upload Artifact");
        string googlePlayTrack = ChoiceOrDefault(request.GooglePlayTrack, ["internal", "alpha", "beta", "production"], "internal", "Google Play Track");
        string googlePlayReleaseStatus = ChoiceOrDefault(request.GooglePlayReleaseStatus, ["draft", "inProgress", "halted", "completed"], "draft", "Google Play Release Status");
        string androidMinSdkVersion = (request.AndroidMinSdkVersion ?? "").Trim();
        string androidTargetSdkVersion = (request.AndroidTargetSdkVersion ?? "").Trim();
        string googlePlayPackageName = (request.GooglePlayPackageName ?? request.BundleIdentifier ?? "").Trim();
        string googlePlayServiceAccountJsonPath = (request.GooglePlayServiceAccountJsonPath ?? "").Trim();

        ValidateOptionalInteger(androidMinSdkVersion, "Android Min SDK Version");
        ValidateOptionalInteger(androidTargetSdkVersion, "Android Target SDK Version");
        if (request.GooglePlayUserFraction is <= 0 or > 1)
        {
            throw new InvalidOperationException("Google Play User Fraction 必须大于 0 且小于等于 1。");
        }

        if (request.GooglePlayUploadEnabled)
        {
            if (string.IsNullOrWhiteSpace(googlePlayPackageName))
            {
                throw new InvalidOperationException("启用 Google Play 上传时，必须填写 Google Play Package 或 Bundle Identifier。");
            }

            if (string.IsNullOrWhiteSpace(googlePlayServiceAccountJsonPath))
            {
                throw new InvalidOperationException("启用 Google Play 上传时，必须填写 Service Account JSON 路径。");
            }

            if (googlePlayUploadArtifact == AndroidBuildFormats.Apk && androidBuildFormat == AndroidBuildFormats.Aab)
            {
                throw new InvalidOperationException("上传 APK 时，Android Build Format 不能只选择 aab。");
            }

            if (googlePlayUploadArtifact == AndroidBuildFormats.Aab && androidBuildFormat == AndroidBuildFormats.Apk)
            {
                throw new InvalidOperationException("上传 AAB 时，Android Build Format 不能只选择 apk。");
            }

            if (googlePlayUploadArtifact == AndroidBuildFormats.Both && androidBuildFormat != AndroidBuildFormats.Both)
            {
                throw new InvalidOperationException("上传 APK + AAB 时，Android Build Format 必须选择 both。");
            }
        }

        json["androidBuildFormat"] = androidBuildFormat;
        json["androidOutputDirectory"] = (request.AndroidOutputDirectory ?? "").Trim();
        json["apkOutputPath"] = (request.ApkOutputPath ?? "").Trim();
        json["aabOutputPath"] = (request.AabOutputPath ?? "").Trim();
        json["androidMinSdkVersion"] = androidMinSdkVersion;
        json["androidTargetSdkVersion"] = androidTargetSdkVersion;
        json["androidKeystoreName"] = (request.AndroidKeystoreName ?? "").Trim();
        json["androidKeystorePass"] = request.AndroidKeystorePass ?? "";
        json["androidKeyaliasName"] = (request.AndroidKeyaliasName ?? "").Trim();
        json["androidKeyaliasPass"] = request.AndroidKeyaliasPass ?? "";
        json["googlePlayUploadEnabled"] = request.GooglePlayUploadEnabled;
        json["googlePlayPackageName"] = googlePlayPackageName;
        json["googlePlayServiceAccountJsonPath"] = googlePlayServiceAccountJsonPath;
        json["googlePlayTrack"] = googlePlayTrack;
        json["googlePlayReleaseStatus"] = googlePlayReleaseStatus;
        json["googlePlayReleaseName"] = (request.GooglePlayReleaseName ?? "").Trim();
        json["googlePlayUploadArtifact"] = googlePlayUploadArtifact;
        json["googlePlayChangesNotSentForReview"] = request.GooglePlayChangesNotSentForReview;
        json["googlePlayUserFraction"] = request.GooglePlayUserFraction;
    }

    private static void EnsureConfigPathUnique(BuildServerDatabase db, string currentConfigId, string projectId, string configPath)
    {
        if (db.Configs.Any(config =>
                config.Id != currentConfigId &&
                config.ProjectId == projectId &&
                string.Equals(Path.GetFullPath(BuildServerEnvironment.ExpandHome(config.ConfigPath)), configPath, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("同一个项目下已经存在使用这个配置文件路径的配置。");
        }
    }

    private static string SafeConfigFileName(string? requestedFileName, string configName, string buildPlatform)
    {
        string fileName = string.IsNullOrWhiteSpace(requestedFileName)
            ? $"build-{buildPlatform}.{SafeFileNamePart(configName)}.json"
            : requestedFileName.Trim();
        fileName = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? fileName : $"{fileName}.json";

        if (fileName != Path.GetFileName(fileName) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            fileName.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("配置文件名只能填写文件名，不能包含目录或特殊字符。");
        }

        return fileName;
    }

    private static string SafeFileNamePart(string value)
    {
        string safe = new(value.Trim().Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray());
        safe = safe.Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? "config" : safe;
    }

    private static string SafePathComponent(string value, string field)
    {
        if (value != Path.GetFileName(value) ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{field} 只能填写单个文件夹名，不能包含路径。");
        }

        return value;
    }

    private static string DeriveProjectDirectoryName(ProjectRecord project)
    {
        string source = project.RepositoryUrl.TrimEnd('/');
        int slashIndex = Math.Max(source.LastIndexOf('/'), source.LastIndexOf(':'));
        string name = slashIndex >= 0 ? source[(slashIndex + 1)..] : project.Name;
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return SafePathComponent(string.IsNullOrWhiteSpace(name) ? project.Name : name, "仓库目录名");
    }

    private static string ChoiceOrDefault(string? value, IReadOnlyList<string> allowedValues, string defaultValue, string field)
    {
        string result = string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        if (!allowedValues.Contains(result, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{field} 只能是: {string.Join(", ", allowedValues)}");
        }

        return result;
    }

    private static string NormalizeBuildPlatform(string? value)
    {
        string buildPlatform = BuildPlatforms.Normalize(value);
        if (!BuildPlatforms.IsKnown(buildPlatform))
        {
            throw new InvalidOperationException("Build Platform 只能是 ios 或 android。");
        }

        return buildPlatform;
    }

    private static string DetectBuildPlatformFromConfig(string configPath)
    {
        try
        {
            JsonObject json = JsonNode.Parse(File.ReadAllText(configPath))?.AsObject() ?? [];
            return NormalizeBuildPlatform(json["buildPlatform"]?.GetValue<string>());
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"配置文件不是有效 JSON: {configPath}。{ex.Message}");
        }
    }

    private static string DefaultUnityBuildMethod(string buildPlatform)
    {
        return buildPlatform == BuildPlatforms.Android
            ? DefaultUnityBuildMethods.Android
            : DefaultUnityBuildMethods.Ios;
    }

    private static void ValidateOptionalInteger(string value, string field)
    {
        if (!string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _))
        {
            throw new InvalidOperationException($"{field} 必须是整数。");
        }
    }

    private static string Required(string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{field} 不能为空。")
            : value.Trim();
    }

    private static string ValidateGitUrl(string value, BuildServerOptions options)
    {
        value = NormalizeGitUrlInput(value);
        if (value.Any(char.IsWhiteSpace) || value.Contains('[') || value.Contains(']'))
        {
            throw new InvalidOperationException("Git 仓库地址格式不正确。");
        }

        bool looksLikeGitUrl =
            value.StartsWith("git@", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase);
        if (!looksLikeGitUrl)
        {
            throw new InvalidOperationException("Git 仓库地址必须是 git clone 可用的 HTTPS 或 SSH 地址。");
        }

        string? host = TryGetGitHost(value);
        if (options.AllowedRepositoryHosts.Count > 0 &&
            (string.IsNullOrWhiteSpace(host) || !options.AllowedRepositoryHosts.Contains(host, StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Git 仓库 Host 不在服务端白名单内: {host ?? "(无法识别)"}");
        }

        return value;
    }

    private static string NormalizeGitUrlInput(string value)
    {
        string normalized = value.Trim();
        int markdownStart = normalized.IndexOf('(');
        int markdownEnd = normalized.LastIndexOf(')');
        if (markdownStart >= 0 && markdownEnd > markdownStart)
        {
            string candidate = normalized[(markdownStart + 1)..markdownEnd].Trim();
            if (candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
            {
                normalized = candidate;
            }
        }

        int queryIndex = normalized.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            normalized = normalized[..queryIndex];
        }

        normalized = normalized.Trim().TrimEnd('/');
        if (IsBareGitHubHttpsUrl(normalized))
        {
            normalized += ".git";
        }

        if (normalized.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase) &&
            !normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalized += ".git";
        }

        return normalized;
    }

    private static bool IsBareGitHubHttpsUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 2;
    }

    private static string? TryGetGitHost(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            return uri.Host.ToLowerInvariant();
        }

        if (value.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            string withoutUser = value["git@".Length..];
            int separatorIndex = withoutUser.IndexOfAny([':', '/']);
            return separatorIndex <= 0 ? null : withoutUser[..separatorIndex].ToLowerInvariant();
        }

        return null;
    }

    private static string ValidatePathUnderAllowedRoots(string value, IReadOnlyList<string> allowedRoots, string field)
    {
        string fullPath = Path.GetFullPath(BuildServerEnvironment.ExpandHome(value));
        if (IsUnsafeRoot(fullPath))
        {
            throw new InvalidOperationException($"{field} 不能指向磁盘根目录。");
        }

        if (allowedRoots.Count > 0 && !allowedRoots.Any(root => IsSameOrChild(fullPath, root)))
        {
            throw new InvalidOperationException($"{field} 不在服务端允许目录内: {fullPath}");
        }

        return fullPath;
    }

    private static bool IsUnsafeRoot(string path)
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

    private static bool IsAllowedArtifactPath(string path, BuildJobRecord job)
    {
        string? jobRoot = string.IsNullOrWhiteSpace(job.WorkerLogPath) ? null : Path.GetDirectoryName(job.WorkerLogPath);
        return (!string.IsNullOrWhiteSpace(job.ArtifactRoot) && IsSameOrChild(path, job.ArtifactRoot)) ||
               (!string.IsNullOrWhiteSpace(jobRoot) && IsSameOrChild(path, jobRoot));
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
