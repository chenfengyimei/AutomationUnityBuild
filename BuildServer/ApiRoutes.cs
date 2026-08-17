using System.IO.Compression;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuildServer.Persistence;
using BuildServer.Security;
using BuildServer.Services;

namespace BuildServer;

public static class ApiRoutes
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ZipLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, int> SseConnectionsByUser = new(StringComparer.Ordinal);

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
        app.MapGet("/api/email-settings", GetEmailSettingsAsync);
        app.MapPut("/api/email-settings", UpdateEmailSettingsAsync);
        app.MapPost("/api/email-settings/test", SendTestEmailAsync);
        app.MapGet("/api/notification-contacts", ListNotificationContactsAsync);
        app.MapPost("/api/notification-contacts", CreateNotificationContactAsync);
        app.MapPut("/api/notification-contacts/{contactId}", UpdateNotificationContactAsync);
        app.MapDelete("/api/notification-contacts/{contactId}", DeleteNotificationContactAsync);
        app.MapGet("/api/storage/overview", StorageOverviewAsync);
        app.MapGet("/api/storage/jobs", StorageJobsAsync);
        app.MapDelete("/api/storage/jobs/{jobId}", DeleteJobStorageAsync);
        app.MapPost("/api/storage/cleanup", BatchDeleteStorageAsync);

        app.MapGet("/api/project-profiles", ListProjectProfilesAsync);
        app.MapPost("/api/project-profiles", CreateProjectProfileAsync);
        app.MapPut("/api/project-profiles/{profileId}", UpdateProjectProfileAsync);
        app.MapDelete("/api/project-profiles/{profileId}", DeleteProjectProfileAsync);

        app.MapGet("/api/certificate-profiles", ListCertificateProfilesAsync);
        app.MapPost("/api/certificate-profiles", CreateCertificateProfileAsync);
        app.MapPut("/api/certificate-profiles/{profileId}", UpdateCertificateProfileAsync);
        app.MapDelete("/api/certificate-profiles/{profileId}", DeleteCertificateProfileAsync);

        app.MapGet("/api/signing-profiles", ListSigningProfilesAsync);
        app.MapPost("/api/signing-profiles", CreateSigningProfileAsync);
        app.MapPut("/api/signing-profiles/{profileId}", UpdateSigningProfileAsync);
        app.MapDelete("/api/signing-profiles/{profileId}", DeleteSigningProfileAsync);

        app.MapGet("/api/unity-project-profiles", ListUnityProjectProfilesAsync);
        app.MapPost("/api/unity-project-profiles", CreateUnityProjectProfileAsync);
        app.MapPut("/api/unity-project-profiles/{profileId}", UpdateUnityProjectProfileAsync);
        app.MapDelete("/api/unity-project-profiles/{profileId}", DeleteUnityProjectProfileAsync);

        app.MapGet("/api/version-profiles", ListVersionProfilesAsync);
        app.MapPost("/api/version-profiles", CreateVersionProfileAsync);
        app.MapPut("/api/version-profiles/{profileId}", UpdateVersionProfileAsync);
        app.MapDelete("/api/version-profiles/{profileId}", DeleteVersionProfileAsync);

        app.MapPost("/api/data/export", ExportDataAsync);
        app.MapPost("/api/data/import", ImportDataAsync);

        app.MapPost("/api/config-files/upload", UploadConfigFileAsync);
        app.MapPost("/api/secrets/upload", UploadSecretFileAsync);

        app.MapGet("/api/automation-tool", GetAutomationToolAsync);
        app.MapPut("/api/automation-tool", UpdateAutomationToolAsync);
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

        if (!TryAcquireSseConnection(user.Id, options.MaxSseConnectionsPerUser, out int currentConnections))
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync(
                $"Too many event connections for this user. Current limit is {options.MaxSseConnectionsPerUser}.",
                context.RequestAborted);
            return;
        }

        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
        context.Response.ContentType = "text/event-stream; charset=utf-8";

        try
        {
            context.Response.Headers["X-Sse-Connection-Count"] = currentConnections.ToString();
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
        finally
        {
            ReleaseSseConnection(user.Id);
        }
    }

    private static async Task<object> DashboardSnapshotAsync(JsonDatabase database, BuildServerOptions options)
    {
        await EnsureProjectProfilesImportedAsync(database);
        return await database.ReadAsync(db => new
        {
            projects = db.Projects.OrderBy(project => project.Name).ToList(),
            configs = db.Configs.OrderBy(config => config.Name).ToList(),
            jobs = db.Jobs.OrderByDescending(job => job.CreatedAt).Take(100).ToList(),
            workers = db.Workers.OrderBy(worker => worker.Name).ToList(),
            notificationContacts = db.NotificationContacts.OrderBy(c => c.Title).ThenBy(c => c.Email).ToList(),
            projectProfiles = db.ProjectProfiles.OrderBy(p => p.Name).ToList(),
            certificateProfiles = db.CertificateProfiles.OrderBy(c => c.Name).ToList(),
            signingProfiles = db.SigningProfiles.OrderBy(s => s.Name).ToList(),
            unityProjectProfiles = db.UnityProjectProfiles.OrderBy(u => u.Name).ToList(),
            versionProfiles = db.VersionProfiles.OrderBy(v => v.Name).ToList(),
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

    private static async Task EnsureProjectProfilesImportedAsync(JsonDatabase database)
    {
        await database.UpdateAsync(db =>
        {
            foreach (ProjectRecord project in db.Projects)
            {
                if (db.ProjectProfiles.Any(p => p.ProjectRecordId == project.Id))
                {
                    continue;
                }

                db.ProjectProfiles.Add(new ProjectProfileRecord
                {
                    Id = Ids.New("pp"),
                    ProjectRecordId = project.Id,
                    Name = project.Name,
                    RepositoryUrl = project.RepositoryUrl,
                    DefaultBranch = project.DefaultBranch,
                    AllowedBranches = project.AllowedBranches,
                    DefaultBuildPlatform = project.DefaultBuildPlatform,
                    Description = project.Description,
                    WorkspaceRoot = project.WorkspaceRoot,
                    ArtifactsRoot = project.ArtifactsRoot,
                    CreatedAt = project.CreatedAt
                });
            }

            return true;
        });
    }

    private static async Task WriteSseEventAsync(HttpContext context, string eventName, object data)
    {
        string json = JsonSerializer.Serialize(data, CamelizeOptions);
        await context.Response.WriteAsync($"event: {eventName}\n", context.RequestAborted);
        await context.Response.WriteAsync($"data: {json}\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }

    private static bool TryAcquireSseConnection(string userId, int maxConnections, out int currentConnections)
    {
        currentConnections = SseConnectionsByUser.AddOrUpdate(userId, 1, (_, count) => count + 1);
        if (currentConnections <= maxConnections)
        {
            return true;
        }

        ReleaseSseConnection(userId);
        return false;
    }

    private static void ReleaseSseConnection(string userId)
    {
        SseConnectionsByUser.AddOrUpdate(userId, 0, (_, count) => Math.Max(0, count - 1));
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
                    AllowedProjectIds = NormalizeAllowedProjectIds(request.AllowedProjectIds, db),
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
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
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
                user.AllowedProjectIds = IsRootAdmin(user)
                    ? []
                    : NormalizeAllowedProjectIds(request.AllowedProjectIds, db);
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
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
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
        return Results.Ok(await database.ReadAsync(db => db.Projects
            .Where(project => CanAccessProject(user, project.Id))
            .OrderBy(project => project.Name)
            .ToList()));
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
            .Where(config =>
                CanAccessProject(user, config.ProjectId) &&
                (string.IsNullOrWhiteSpace(projectId) || config.ProjectId == projectId))
            .OrderBy(config => config.Name)
            .ToList()));
    }

    private static async Task<IResult> CreateConfigAsync(BuildConfigRequest request, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!CanAccessProject(user, request.ProjectId)) return Results.Forbid();
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
        if (!CanAccessProject(user, request.ProjectId)) return Results.Forbid();
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
                WriteTextAtomically(
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
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
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
            if (!CanAccessProject(user, config.ProjectId)) return Results.Forbid();
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
        if (!CanAccessProject(user, request.ProjectId)) return Results.Forbid();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            BuildConfigRecord updatedConfig = await database.UpdateAsync(db =>
            {
                BuildConfigRecord record = db.Configs.FirstOrDefault(config => config.Id == configId)
                    ?? throw new FileNotFoundException("配置不存在。");
                if (!CanAccessProject(user, record.ProjectId))
                {
                    throw new UnauthorizedAccessException();
                }
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
                if (!CanAccessProject(user, record.ProjectId))
                {
                    throw new UnauthorizedAccessException();
                }
                ProjectRecord project = db.Projects.FirstOrDefault(project => project.Id == request.ProjectId && project.Enabled)
                    ?? throw new InvalidOperationException("项目不存在或已禁用。");

                string configName = Required(request.Name, "配置名称");
                string buildPlatform = NormalizeBuildPlatform(request.BuildPlatform ?? project.DefaultBuildPlatform);
                string configPath = ValidatePathUnderAllowedRoots(record.ConfigPath, options.AllowedConfigRoots, "配置文件路径");

                string? dir2 = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(dir2)) Directory.CreateDirectory(dir2);
                WriteTextAtomically(
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
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
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
                if (!CanAccessProject(user, record.ProjectId))
                {
                    throw new UnauthorizedAccessException();
                }
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
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
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
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
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
            .Where(job =>
                CanAccessProject(user, job.ProjectId) &&
                (string.IsNullOrWhiteSpace(projectId) || job.ProjectId == projectId))
            .OrderByDescending(job => job.CreatedAt)
            .Take(100)
            .ToList()));
    }

    private static async Task<IResult> GetJobAsync(string jobId, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == jobId));
        if (job is null) return Results.NotFound();
        if (!CanAccessProject(user, job.ProjectId)) return Results.Forbid();
        return Results.Ok(job);
    }

    private static async Task<IResult> GetJobLogAsync(string jobId, HttpContext context, AuthService auth, JsonDatabase database, int? lines, bool full = false)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == jobId));
        if (job is null) return Results.NotFound();
        if (!CanAccessProject(user, job.ProjectId)) return Results.Forbid();
        SetNoStoreHeaders(context);
        if (!File.Exists(job.WorkerLogPath)) return Results.Ok("");
        string log = full
            ? LogFileReader.ReadAll(job.WorkerLogPath)
            : LogFileReader.Tail(job.WorkerLogPath, Math.Clamp(lines ?? 300, 20, 2000));
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
        (BuildJobRecord? job, List<BuildArtifactRecord> artifacts) = await database.ReadAsync(db =>
        {
            BuildJobRecord? job = db.Jobs.FirstOrDefault(job => job.Id == jobId);
            List<BuildArtifactRecord> artifacts = job is null
                ? []
                : db.Artifacts.Where(artifact => artifact.JobId == jobId).ToList();
            return (job, artifacts);
        });

        if (job is null) return Results.NotFound();
        if (!CanAccessProject(user, job.ProjectId)) return Results.Forbid();
        return Results.Ok(artifacts);
    }

    private static async Task<IResult> DownloadArtifactAsync(string artifactId, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        BuildArtifactRecord? artifact = await database.ReadAsync(db => db.Artifacts.FirstOrDefault(artifact => artifact.Id == artifactId));
        if (artifact is null) return Results.NotFound();
        BuildJobRecord? job = await database.ReadAsync(db => db.Jobs.FirstOrDefault(job => job.Id == artifact.JobId));
        if (job is null || !CanAccessProject(user, job.ProjectId) || !IsAllowedArtifactPath(artifact.Path, job, options))
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
            await EnsureZipAsync(artifact.Path, zipPath);
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
            string workerId = string.IsNullOrWhiteSpace(request.Id)
                ? Ids.New("worker")
                : request.Id.Trim();
            WorkerNodeRecord? worker = db.Workers.FirstOrDefault(worker => worker.Id == workerId);
            if (worker is null)
            {
                worker = new WorkerNodeRecord
                {
                    Id = workerId,
                    Enabled = true,
                    Status = WorkerStatuses.Idle
                };
                db.Workers.Add(worker);
            }

            worker.Name = request.Name;
            worker.HostName = request.HostName;
            worker.UnityVersions = request.UnityVersions;
            worker.XcodeVersions = request.XcodeVersions;
            worker.ProjectIds = request.ProjectIds;
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

    private static async Task<IResult> GetAutomationToolAsync(
        HttpContext context,
        AuthService auth,
        JsonDatabase database,
        BuildServerOptions options,
        IWebHostEnvironment environment)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();

        AutomationToolSettingsRecord? settings = await database.ReadAsync(db => db.AutomationToolSettings);
        List<CliCandidate> candidates = AutomationToolLocator.DetectAllCandidates(options, environment);
        AutomationCommand? activeCommand = AutomationToolLocator.TryLocateWithSettings(settings, options, environment);

        return Results.Ok(new
        {
            mode = settings?.Mode ?? "auto",
            manualPath = settings?.ManualPath ?? "",
            updatedAt = settings?.UpdatedAt,
            candidates,
            activePath = activeCommand?.FileName ?? "",
            activeWorkingDirectory = activeCommand?.WorkingDirectory ?? "",
            isActiveDll = activeCommand is not null &&
                          activeCommand.PrefixArgs.Count > 0 &&
                          string.Equals(activeCommand.FileName, "dotnet", StringComparison.OrdinalIgnoreCase),
            found = activeCommand is not null
        });
    }

    private static async Task<IResult> UpdateAutomationToolAsync(
        AutomationToolRequest request,
        HttpContext context,
        AuthService auth,
        JsonDatabase database,
        BuildServerOptions options,
        IWebHostEnvironment environment)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.IsAdmin(user)) return Results.Forbid();

        try
        {
            string mode = string.IsNullOrWhiteSpace(request.Mode) ? "auto" : request.Mode.Trim().ToLowerInvariant();
            if (mode != "auto" && mode != "manual")
            {
                throw new InvalidOperationException("模式必须是 auto 或 manual。");
            }

            string manualPath = (request.ManualPath ?? "").Trim();

            AutomationToolSettingsRecord saved = await database.UpdateAsync(db =>
            {
                AutomationToolSettingsRecord? existing = db.AutomationToolSettings;
                AutomationToolSettingsRecord settings = existing ?? new AutomationToolSettingsRecord { Id = "automation-tool" };

                settings.Mode = mode;
                settings.ManualPath = mode == "manual" ? manualPath : "";
                settings.UpdatedAt = DateTimeOffset.Now;

                db.AutomationToolSettings = settings;
                AuthService.AddAudit(db, user.Id, user.UserName, "automation-tool.update", "settings", "automation-tool",
                    $"更新 CLI 设置 mode={settings.Mode} manualPath={settings.ManualPath}");
                return settings;
            });

            List<CliCandidate> candidates = AutomationToolLocator.DetectAllCandidates(options, environment);
            AutomationCommand? activeCommand = AutomationToolLocator.TryLocateWithSettings(saved, options, environment);

            return Results.Ok(new
            {
                mode = saved.Mode,
                manualPath = saved.ManualPath,
                updatedAt = saved.UpdatedAt,
                candidates,
                activePath = activeCommand?.FileName ?? "",
                activeWorkingDirectory = activeCommand?.WorkingDirectory ?? "",
                isActiveDll = activeCommand is not null &&
                              activeCommand.PrefixArgs.Count > 0 &&
                              string.Equals(activeCommand.FileName, "dotnet", StringComparison.OrdinalIgnoreCase),
                found = activeCommand is not null
            });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> GetEmailSettingsAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        return Results.Ok(await database.ReadAsync(db =>
        {
            EmailSettingsRecord? settings = db.EmailSettings;
            if (settings is null)
            {
                return (object)new { enabled = false, smtpHost = "", smtpPort = 587, smtpUserName = "", fromEmail = "", fromName = "", useSsl = true };
            }

            return (object)new
            {
                settings.Enabled,
                settings.SmtpHost,
                settings.SmtpPort,
                settings.SmtpUserName,
                FromEmail = settings.FromEmail,
                FromName = settings.FromName,
                settings.UseSsl,
                settings.UpdatedAt
            };
        }));
    }

    private static async Task<IResult> UpdateEmailSettingsAsync(EmailSettingsRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            string smtpHost = Required(request.SmtpHost, "SMTP 主机");
            string smtpUserName = (request.SmtpUserName ?? "").Trim();
            string fromEmail = Required(request.FromEmail, "发信邮箱");
            if (!IsValidEmail(fromEmail))
            {
                throw new InvalidOperationException("发信邮箱格式不正确。");
            }

            int smtpPort = request.SmtpPort < 1 || request.SmtpPort > 65535
                ? throw new InvalidOperationException("SMTP 端口必须在 1 到 65535 之间。")
                : request.SmtpPort;

            EmailSettingsRecord saved = await database.UpdateAsync(db =>
            {
                EmailSettingsRecord? existing = db.EmailSettings;
                EmailSettingsRecord settings = existing ?? new EmailSettingsRecord { Id = "email-settings" };

                settings.SmtpHost = smtpHost;
                settings.SmtpPort = smtpPort;
                settings.SmtpUserName = smtpUserName;
                if (!string.IsNullOrWhiteSpace(request.SmtpPassword))
                {
                    settings.SmtpPassword = request.SmtpPassword;
                }
                settings.FromEmail = fromEmail;
                settings.FromName = (request.FromName ?? "").Trim();
                settings.UseSsl = request.UseSsl;
                settings.Enabled = request.Enabled;
                settings.UpdatedAt = DateTimeOffset.Now;

                db.EmailSettings = settings;
                AuthService.AddAudit(db, user.Id, user.UserName, "email-settings.update", "settings", "email-settings", $"更新邮件通知设置 enabled={settings.Enabled} host={settings.SmtpHost}:{settings.SmtpPort}");
                return settings;
            });

            return Results.Ok(new
            {
                saved.Enabled,
                saved.SmtpHost,
                saved.SmtpPort,
                saved.SmtpUserName,
                saved.FromEmail,
                saved.FromName,
                saved.UseSsl,
                saved.UpdatedAt
            });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> SendTestEmailAsync(TestEmailRequest request, HttpContext context, AuthService auth, EmailNotificationService emailService)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            string toEmail = Required(request.ToEmail, "收件邮箱");
            if (!IsValidEmail(toEmail))
            {
                throw new InvalidOperationException("收件邮箱格式不正确。");
            }

            (bool success, string error) = await emailService.SendTestEmailAsync(toEmail);
            return success
                ? Results.Ok(new { ok = true })
                : Results.Ok(new { ok = false, error });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static List<string> NormalizeNotifyEmails(string[]? emails)
    {
        if (emails is null || emails.Length == 0)
        {
            return [];
        }

        List<string> result = emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string email in result)
        {
            if (!IsValidEmail(email))
            {
                throw new InvalidOperationException($"通知邮箱格式不正确: {email}");
            }
        }

        return result;
    }

    private static async Task<IResult> ListNotificationContactsAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();
        return Results.Ok(await database.ReadAsync(db => db.NotificationContacts
            .OrderBy(contact => contact.Title)
            .ThenBy(contact => contact.Email)
            .ToList()));
    }

    private static async Task<IResult> CreateNotificationContactAsync(NotificationContactRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            string title = Required(request.Title, "职位名称");
            string email = Required(request.Email, "邮箱");
            if (!IsValidEmail(email))
            {
                throw new InvalidOperationException("邮箱格式不正确。");
            }

            NotificationContactRecord contact = await database.UpdateAsync(db =>
            {
                if (db.NotificationContacts.Any(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("该邮箱已存在于通知联系人列表中。");
                }

                var contact = new NotificationContactRecord
                {
                    Id = Ids.New("contact"),
                    Title = title,
                    Email = email,
                    Enabled = request.Enabled,
                    CreatedAt = DateTimeOffset.Now
                };
                db.NotificationContacts.Add(contact);
                AuthService.AddAudit(db, user.Id, user.UserName, "contact.create", "contact", contact.Id, $"新增通知联系人 {contact.Title} <{contact.Email}>");
                return contact;
            });

            return Results.Ok(contact);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UpdateNotificationContactAsync(string contactId, NotificationContactRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            string title = Required(request.Title, "职位名称");
            string email = Required(request.Email, "邮箱");
            if (!IsValidEmail(email))
            {
                throw new InvalidOperationException("邮箱格式不正确。");
            }

            NotificationContactRecord contact = await database.UpdateAsync(db =>
            {
                NotificationContactRecord? contact = db.NotificationContacts.FirstOrDefault(c => c.Id == contactId)
                    ?? throw new FileNotFoundException("联系人不存在。");
                if (db.NotificationContacts.Any(c => c.Id != contactId && string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("该邮箱已存在于通知联系人列表中。");
                }

                contact.Title = title;
                contact.Email = email;
                contact.Enabled = request.Enabled;
                AuthService.AddAudit(db, user.Id, user.UserName, "contact.update", "contact", contact.Id, $"更新通知联系人 {contact.Title} <{contact.Email}>");
                return contact;
            });

            return Results.Ok(contact);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DeleteNotificationContactAsync(string contactId, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            NotificationContactRecord contact = await database.UpdateAsync(db =>
            {
                NotificationContactRecord? contact = db.NotificationContacts.FirstOrDefault(c => c.Id == contactId)
                    ?? throw new FileNotFoundException("联系人不存在。");
                db.NotificationContacts.Remove(contact);
                AuthService.AddAudit(db, user.Id, user.UserName, "contact.delete", "contact", contact.Id, $"删除通知联系人 {contact.Title} <{contact.Email}>");
                return contact;
            });

            return Results.Ok(new { deleted = true, contact });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> StorageOverviewAsync(HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        return Results.Ok(await database.ReadAsync(db =>
        {
            var completedJobs = db.Jobs.Where(job => IsCompleted(job.Status)).ToList();
            long artifactBytes = db.Artifacts.Sum(a => a.SizeBytes);
            long logBytes = completedJobs.Sum(job => EstimateLogSize(job));
            return new
            {
                totalJobs = db.Jobs.Count,
                completedJobs = completedJobs.Count,
                totalArtifactBytes = artifactBytes,
                totalLogBytes = logBytes,
                artifactCount = db.Artifacts.Count,
                retentionDays = options.RetentionDays,
                maxArtifactBytes = options.MaxArtifactBytes,
                dataRoot = options.DataRoot
            };
        }));
    }

    private static async Task<IResult> StorageJobsAsync(HttpContext context, AuthService auth, JsonDatabase database, string? status)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        return Results.Ok(await database.ReadAsync(db =>
        {
            return db.Jobs
                .Where(job => string.IsNullOrWhiteSpace(status) || job.Status == status)
                .OrderByDescending(job => job.CreatedAt)
                .Select(job => new
                {
                    jobId = job.Id,
                    projectName = db.Projects.FirstOrDefault(p => p.Id == job.ProjectId)?.Name ?? job.ProjectId,
                    configName = db.Configs.FirstOrDefault(c => c.Id == job.ConfigId)?.Name ?? job.ConfigId,
                    status = job.Status,
                    buildNumber = job.BuildNumber,
                    platform = job.BuildPlatform,
                    createdAt = job.CreatedAt,
                    finishedAt = job.FinishedAt,
                    artifactRoot = job.ArtifactRoot,
                    workerLogPath = job.WorkerLogPath,
                    artifactCount = db.Artifacts.Count(a => a.JobId == job.Id),
                    artifactBytes = db.Artifacts.Where(a => a.JobId == job.Id).Sum(a => a.SizeBytes),
                    hasFilesOnDisk = !string.IsNullOrWhiteSpace(job.ArtifactRoot) || !string.IsNullOrWhiteSpace(job.WorkerLogPath)
                })
                .ToList();
        }));
    }

    private static async Task<IResult> DeleteJobStorageAsync(string jobId, HttpContext context, AuthService auth, StorageCleanupService cleanupService)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        (bool success, string error) = await cleanupService.DeleteJobStorageAsync(jobId, user);
        return success ? Results.Ok(new { deleted = true }) : Results.NotFound(new { error });
    }

    private static async Task<IResult> BatchDeleteStorageAsync(BatchDeleteRequest request, HttpContext context, AuthService auth, StorageCleanupService cleanupService)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        (int deleted, List<string> errors) = await cleanupService.BatchDeleteAsync(request.JobIds, user);
        return Results.Ok(new { deleted, errors });
    }

    // ---- Project Profiles ----

    private static async Task<IResult> ListProjectProfilesAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();
        return Results.Ok(await database.ReadAsync(db => db.ProjectProfiles.OrderBy(p => p.Name).ToList()));
    }

    private static async Task<IResult> CreateProjectProfileAsync(ProjectProfileRequest request, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            ProjectProfileRecord profile = await database.UpdateAsync(db =>
            {
                string repoUrl = ValidateGitUrl(Required(request.RepositoryUrl, "Git 仓库"), options);
                string workspaceRoot = ValidatePathUnderAllowedRoots(
                    string.IsNullOrWhiteSpace(request.WorkspaceRoot) ? "~/UnityBuildWorkspace" : request.WorkspaceRoot.Trim(),
                    options.AllowedWorkspaceRoots, "工作区目录");
                string artifactsRoot = ValidatePathUnderAllowedRoots(
                    string.IsNullOrWhiteSpace(request.ArtifactsRoot) ? "~/UnityBuildArtifacts" : request.ArtifactsRoot.Trim(),
                    options.AllowedArtifactsRoots, "产物目录");

                var project = new ProjectRecord
                {
                    Id = Ids.New("prj"),
                    Name = Required(request.Name, "项目名称"),
                    RepositoryUrl = repoUrl,
                    DefaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch) ? "main" : request.DefaultBranch.Trim(),
                    AllowedBranches = request.AllowedBranches?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? ["main"],
                    WorkspaceRoot = workspaceRoot,
                    ArtifactsRoot = artifactsRoot,
                    DefaultBuildPlatform = NormalizeBuildPlatform(request.DefaultBuildPlatform),
                    Description = request.Description ?? "",
                    CreatedAt = DateTimeOffset.Now
                };
                db.Projects.Add(project);

                var profile = new ProjectProfileRecord
                {
                    Id = Ids.New("pp"),
                    ProjectRecordId = project.Id,
                    Name = project.Name,
                    RepositoryUrl = project.RepositoryUrl,
                    DefaultBranch = project.DefaultBranch,
                    AllowedBranches = project.AllowedBranches,
                    DefaultBuildPlatform = project.DefaultBuildPlatform,
                    Description = project.Description,
                    ProjectDirectoryName = request.ProjectDirectoryName ?? "",
                    WorkspaceRoot = project.WorkspaceRoot,
                    ArtifactsRoot = project.ArtifactsRoot,
                    CreatedAt = DateTimeOffset.Now
                };
                db.ProjectProfiles.Add(profile);
                AuthService.AddAudit(db, user.Id, user.UserName, "project-profile.create", "project-profile", profile.Id, $"创建项目 {profile.Name}");
                return profile;
            });
            return Results.Ok(profile);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UpdateProjectProfileAsync(string profileId, ProjectProfileRequest request, HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            ProjectProfileRecord profile = await database.UpdateAsync(db =>
            {
                ProjectProfileRecord? profile = db.ProjectProfiles.FirstOrDefault(p => p.Id == profileId)
                    ?? throw new FileNotFoundException("项目不存在。");

                string repoUrl = ValidateGitUrl(Required(request.RepositoryUrl, "Git 仓库"), options);
                string workspaceRoot = ValidatePathUnderAllowedRoots(
                    string.IsNullOrWhiteSpace(request.WorkspaceRoot) ? "~/UnityBuildWorkspace" : request.WorkspaceRoot.Trim(),
                    options.AllowedWorkspaceRoots, "工作区目录");
                string artifactsRoot = ValidatePathUnderAllowedRoots(
                    string.IsNullOrWhiteSpace(request.ArtifactsRoot) ? "~/UnityBuildArtifacts" : request.ArtifactsRoot.Trim(),
                    options.AllowedArtifactsRoots, "产物目录");

                profile.Name = Required(request.Name, "项目名称");
                profile.RepositoryUrl = repoUrl;
                profile.DefaultBranch = string.IsNullOrWhiteSpace(request.DefaultBranch) ? "main" : request.DefaultBranch.Trim();
                profile.AllowedBranches = request.AllowedBranches?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? ["main"];
                profile.DefaultBuildPlatform = NormalizeBuildPlatform(request.DefaultBuildPlatform);
                profile.Description = request.Description ?? "";
                profile.ProjectDirectoryName = request.ProjectDirectoryName ?? "";
                profile.WorkspaceRoot = workspaceRoot;
                profile.ArtifactsRoot = artifactsRoot;

                ProjectRecord? project = db.Projects.FirstOrDefault(p => p.Id == profile.ProjectRecordId);
                if (project is not null)
                {
                    project.Name = profile.Name;
                    project.RepositoryUrl = repoUrl;
                    project.DefaultBranch = profile.DefaultBranch;
                    project.AllowedBranches = profile.AllowedBranches;
                    project.DefaultBuildPlatform = profile.DefaultBuildPlatform;
                    project.Description = profile.Description;
                    project.WorkspaceRoot = workspaceRoot;
                    project.ArtifactsRoot = artifactsRoot;
                }

                AuthService.AddAudit(db, user.Id, user.UserName, "project-profile.update", "project-profile", profile.Id, $"更新项目 {profile.Name}");
                return profile;
            });
            return Results.Ok(profile);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DeleteProjectProfileAsync(string profileId, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            ProjectProfileRecord profile = await database.UpdateAsync(db =>
            {
                ProjectProfileRecord? profile = db.ProjectProfiles.FirstOrDefault(p => p.Id == profileId)
                    ?? throw new FileNotFoundException("项目不存在。");

                if (db.Jobs.Any(job => job.ProjectId == profile.ProjectRecordId && (job.Status == BuildStatuses.Queued || job.Status == BuildStatuses.Running)))
                {
                    throw new InvalidOperationException("这个项目还有排队中或运行中的任务，不能删除。");
                }

                if (!string.IsNullOrEmpty(profile.ProjectRecordId))
                {
                    ProjectRecord? project = db.Projects.FirstOrDefault(p => p.Id == profile.ProjectRecordId);
                    if (project is not null)
                    {
                        db.Projects.Remove(project);
                    }
                }

                db.ProjectProfiles.Remove(profile);
                AuthService.AddAudit(db, user.Id, user.UserName, "project-profile.delete", "project-profile", profile.Id, $"删除项目 {profile.Name}");
                return profile;
            });
            return Results.Ok(new { deleted = true, profile });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    // ---- Certificate Profiles ----

    private static async Task<IResult> ListCertificateProfilesAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();
        return Results.Ok(await database.ReadAsync(db => db.CertificateProfiles.OrderBy(c => c.Name).ToList()));
    }

    private static async Task<IResult> CreateCertificateProfileAsync(CertificateProfileRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            CertificateProfileRecord profile = await database.UpdateAsync(db =>
            {
                var profile = new CertificateProfileRecord
                {
                    Id = Ids.New("cp"),
                    Name = Required(request.Name, "模板名称"),
                    Platform = string.IsNullOrWhiteSpace(request.Platform) ? "ios" : request.Platform.Trim().ToLowerInvariant(),
                    AppStoreConnectApiKeyPath = request.AppStoreConnectApiKeyPath ?? "",
                    AppStoreConnectApiKeyId = request.AppStoreConnectApiKeyId ?? "",
                    AppStoreConnectApiIssuerId = request.AppStoreConnectApiIssuerId ?? "",
                    AppStoreConnectUploadEnabled = request.AppStoreConnectUploadEnabled,
                    AppStoreConnectUploadTarget = string.IsNullOrWhiteSpace(request.AppStoreConnectUploadTarget) ? "testflight" : request.AppStoreConnectUploadTarget.Trim().ToLowerInvariant(),
                    GooglePlayUploadEnabled = request.GooglePlayUploadEnabled,
                    GooglePlayPackageName = request.GooglePlayPackageName ?? "",
                    GooglePlayServiceAccountJsonPath = request.GooglePlayServiceAccountJsonPath ?? "",
                    GooglePlayTrack = string.IsNullOrWhiteSpace(request.GooglePlayTrack) ? "internal" : request.GooglePlayTrack.Trim(),
                    GooglePlayReleaseStatus = string.IsNullOrWhiteSpace(request.GooglePlayReleaseStatus) ? "draft" : request.GooglePlayReleaseStatus.Trim(),
                    GooglePlayReleaseName = request.GooglePlayReleaseName ?? "",
                    GooglePlayUploadArtifact = string.IsNullOrWhiteSpace(request.GooglePlayUploadArtifact) ? "aab" : request.GooglePlayUploadArtifact.Trim(),
                    GooglePlayChangesNotSentForReview = request.GooglePlayChangesNotSentForReview,
                    GooglePlayUserFraction = request.GooglePlayUserFraction,
                    TiktokAppId = request.TiktokAppId ?? "",
                    TiktokAccessToken = request.TiktokAccessToken ?? "",
                    TiktokGameName = request.TiktokGameName ?? "",
                    TiktokApiEndpoint = string.IsNullOrWhiteSpace(request.TiktokApiEndpoint) ? "https://open-api.tiktokglobalshop.com" : request.TiktokApiEndpoint.Trim(),
                    TiktokUploadEnabled = request.TiktokUploadEnabled,
                    CreatedAt = DateTimeOffset.Now
                };
                db.CertificateProfiles.Add(profile);
                AuthService.AddAudit(db, user.Id, user.UserName, "cert-profile.create", "cert-profile", profile.Id, $"创建证书模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(profile);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UpdateCertificateProfileAsync(string profileId, CertificateProfileRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            CertificateProfileRecord profile = await database.UpdateAsync(db =>
            {
                CertificateProfileRecord? profile = db.CertificateProfiles.FirstOrDefault(p => p.Id == profileId)
                    ?? throw new FileNotFoundException("证书模板不存在。");
                profile.Name = Required(request.Name, "模板名称");
                profile.Platform = string.IsNullOrWhiteSpace(request.Platform) ? "ios" : request.Platform.Trim().ToLowerInvariant();
                profile.AppStoreConnectApiKeyPath = request.AppStoreConnectApiKeyPath ?? "";
                profile.AppStoreConnectApiKeyId = request.AppStoreConnectApiKeyId ?? "";
                profile.AppStoreConnectApiIssuerId = request.AppStoreConnectApiIssuerId ?? "";
                profile.AppStoreConnectUploadEnabled = request.AppStoreConnectUploadEnabled;
                profile.AppStoreConnectUploadTarget = string.IsNullOrWhiteSpace(request.AppStoreConnectUploadTarget) ? "testflight" : request.AppStoreConnectUploadTarget.Trim().ToLowerInvariant();
                profile.GooglePlayUploadEnabled = request.GooglePlayUploadEnabled;
                profile.GooglePlayPackageName = request.GooglePlayPackageName ?? "";
                profile.GooglePlayServiceAccountJsonPath = request.GooglePlayServiceAccountJsonPath ?? "";
                profile.GooglePlayTrack = string.IsNullOrWhiteSpace(request.GooglePlayTrack) ? "internal" : request.GooglePlayTrack.Trim();
                profile.GooglePlayReleaseStatus = string.IsNullOrWhiteSpace(request.GooglePlayReleaseStatus) ? "draft" : request.GooglePlayReleaseStatus.Trim();
                profile.GooglePlayReleaseName = request.GooglePlayReleaseName ?? "";
                profile.GooglePlayUploadArtifact = string.IsNullOrWhiteSpace(request.GooglePlayUploadArtifact) ? "aab" : request.GooglePlayUploadArtifact.Trim();
                profile.GooglePlayChangesNotSentForReview = request.GooglePlayChangesNotSentForReview;
                profile.GooglePlayUserFraction = request.GooglePlayUserFraction;
                profile.TiktokAppId = request.TiktokAppId ?? "";
                profile.TiktokAccessToken = request.TiktokAccessToken ?? "";
                profile.TiktokGameName = request.TiktokGameName ?? "";
                profile.TiktokApiEndpoint = string.IsNullOrWhiteSpace(request.TiktokApiEndpoint) ? "https://open-api.tiktokglobalshop.com" : request.TiktokApiEndpoint.Trim();
                profile.TiktokUploadEnabled = request.TiktokUploadEnabled;
                AuthService.AddAudit(db, user.Id, user.UserName, "cert-profile.update", "cert-profile", profile.Id, $"更新证书模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(profile);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DeleteCertificateProfileAsync(string profileId, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            CertificateProfileRecord profile = await database.UpdateAsync(db =>
            {
                CertificateProfileRecord? profile = db.CertificateProfiles.FirstOrDefault(p => p.Id == profileId)
                    ?? throw new FileNotFoundException("证书模板不存在。");
                db.CertificateProfiles.Remove(profile);
                AuthService.AddAudit(db, user.Id, user.UserName, "cert-profile.delete", "cert-profile", profile.Id, $"删除证书模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(new { deleted = true, profile });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    // ---- Signing Profiles ----

    private static async Task<IResult> ListSigningProfilesAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();
        return Results.Ok(await database.ReadAsync(db => db.SigningProfiles.OrderBy(s => s.Name).ToList()));
    }

    private static async Task<IResult> CreateSigningProfileAsync(SigningProfileRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            SigningProfileRecord profile = await database.UpdateAsync(db =>
            {
                var profile = new SigningProfileRecord
                {
                    Id = Ids.New("sp"),
                    Name = Required(request.Name, "模板名称"),
                    Platform = string.IsNullOrWhiteSpace(request.Platform) ? "ios" : request.Platform.Trim().ToLowerInvariant(),
                    TeamId = request.TeamId ?? "",
                    ExportMethod = string.IsNullOrWhiteSpace(request.ExportMethod) ? "development" : request.ExportMethod.Trim(),
                    SigningStyle = string.IsNullOrWhiteSpace(request.SigningStyle) ? "automatic" : request.SigningStyle.Trim(),
                    IosDeploymentTarget = request.IosDeploymentTarget ?? "",
                    AndroidKeystoreName = request.AndroidKeystoreName ?? "",
                    AndroidKeystorePass = request.AndroidKeystorePass ?? "",
                    AndroidKeyaliasName = request.AndroidKeyaliasName ?? "",
                    AndroidKeyaliasPass = request.AndroidKeyaliasPass ?? "",
                    CreatedAt = DateTimeOffset.Now
                };
                db.SigningProfiles.Add(profile);
                AuthService.AddAudit(db, user.Id, user.UserName, "signing-profile.create", "signing-profile", profile.Id, $"创建签名模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(profile);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UpdateSigningProfileAsync(string profileId, SigningProfileRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            SigningProfileRecord profile = await database.UpdateAsync(db =>
            {
                SigningProfileRecord? profile = db.SigningProfiles.FirstOrDefault(p => p.Id == profileId)
                    ?? throw new FileNotFoundException("签名模板不存在。");
                profile.Name = Required(request.Name, "模板名称");
                profile.Platform = string.IsNullOrWhiteSpace(request.Platform) ? "ios" : request.Platform.Trim().ToLowerInvariant();
                profile.TeamId = request.TeamId ?? "";
                profile.ExportMethod = string.IsNullOrWhiteSpace(request.ExportMethod) ? "development" : request.ExportMethod.Trim();
                profile.SigningStyle = string.IsNullOrWhiteSpace(request.SigningStyle) ? "automatic" : request.SigningStyle.Trim();
                profile.IosDeploymentTarget = request.IosDeploymentTarget ?? "";
                profile.AndroidKeystoreName = request.AndroidKeystoreName ?? "";
                profile.AndroidKeystorePass = request.AndroidKeystorePass ?? "";
                profile.AndroidKeyaliasName = request.AndroidKeyaliasName ?? "";
                profile.AndroidKeyaliasPass = request.AndroidKeyaliasPass ?? "";
                AuthService.AddAudit(db, user.Id, user.UserName, "signing-profile.update", "signing-profile", profile.Id, $"更新签名模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(profile);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DeleteSigningProfileAsync(string profileId, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            SigningProfileRecord profile = await database.UpdateAsync(db =>
            {
                SigningProfileRecord? profile = db.SigningProfiles.FirstOrDefault(p => p.Id == profileId)
                    ?? throw new FileNotFoundException("签名模板不存在。");
                db.SigningProfiles.Remove(profile);
                AuthService.AddAudit(db, user.Id, user.UserName, "signing-profile.delete", "signing-profile", profile.Id, $"删除签名模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(new { deleted = true, profile });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    // ---- Unity Project Profiles ----

    private static async Task<IResult> ListUnityProjectProfilesAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();
        return Results.Ok(await database.ReadAsync(db => db.UnityProjectProfiles.OrderBy(u => u.Name).ToList()));
    }

    private static async Task<IResult> CreateUnityProjectProfileAsync(UnityProjectProfileRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            UnityProjectProfileRecord profile = await database.UpdateAsync(db =>
            {
                var profile = new UnityProjectProfileRecord
                {
                    Id = Ids.New("up"),
                    Name = Required(request.Name, "模板名称"),
                    UnityProjectRelativePath = string.IsNullOrWhiteSpace(request.UnityProjectRelativePath) ? "." : request.UnityProjectRelativePath.Trim(),
                    UnityVersion = request.UnityVersion ?? "",
                    UnityExecutablePath = request.UnityExecutablePath ?? "",
                    CreatedAt = DateTimeOffset.Now
                };
                db.UnityProjectProfiles.Add(profile);
                AuthService.AddAudit(db, user.Id, user.UserName, "unity-project-profile.create", "unity-project-profile", profile.Id, $"创建工程模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(profile);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UpdateUnityProjectProfileAsync(string profileId, UnityProjectProfileRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            UnityProjectProfileRecord profile = await database.UpdateAsync(db =>
            {
                UnityProjectProfileRecord? profile = db.UnityProjectProfiles.FirstOrDefault(p => p.Id == profileId)
                    ?? throw new FileNotFoundException("工程模板不存在。");
                profile.Name = Required(request.Name, "模板名称");
                profile.UnityProjectRelativePath = string.IsNullOrWhiteSpace(request.UnityProjectRelativePath) ? "." : request.UnityProjectRelativePath.Trim();
                profile.UnityVersion = request.UnityVersion ?? "";
                profile.UnityExecutablePath = request.UnityExecutablePath ?? "";
                AuthService.AddAudit(db, user.Id, user.UserName, "unity-project-profile.update", "unity-project-profile", profile.Id, $"更新工程模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(profile);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DeleteUnityProjectProfileAsync(string profileId, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            UnityProjectProfileRecord profile = await database.UpdateAsync(db =>
            {
                UnityProjectProfileRecord? profile = db.UnityProjectProfiles.FirstOrDefault(p => p.Id == profileId)
                    ?? throw new FileNotFoundException("工程模板不存在。");
                db.UnityProjectProfiles.Remove(profile);
                AuthService.AddAudit(db, user.Id, user.UserName, "unity-project-profile.delete", "unity-project-profile", profile.Id, $"删除工程模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(new { deleted = true, profile });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    // ---- Version Profiles ----

    private static async Task<IResult> ListVersionProfilesAsync(HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(await database.ReadAsync(db => db.VersionProfiles.OrderBy(v => v.Name).ToList()));
    }

    private static async Task<IResult> CreateVersionProfileAsync(VersionProfileRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            VersionProfileRecord profile = await database.UpdateAsync(db =>
            {
                var profile = new VersionProfileRecord
                {
                    Id = Ids.New("vp"),
                    Name = Required(request.Name, "模板名称"),
                    ProductName = request.ProductName ?? "",
                    BundleIdentifier = request.BundleIdentifier ?? "",
                    BundleVersion = string.IsNullOrWhiteSpace(request.BundleVersion) ? "1.0.0" : request.BundleVersion.Trim(),
                    BuildNumber = string.IsNullOrWhiteSpace(request.BuildNumber) ? "1" : request.BuildNumber.Trim(),
                    SyncBundleVersionFromUnity = request.SyncBundleVersionFromUnity,
                    AutoIncrementBuildNumber = request.AutoIncrementBuildNumber,
                    CreatedAt = DateTimeOffset.Now
                };
                db.VersionProfiles.Add(profile);
                AuthService.AddAudit(db, user.Id, user.UserName, "version-profile.create", "version-profile", profile.Id, $"创建版本模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(profile);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UpdateVersionProfileAsync(string profileId, VersionProfileRequest request, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            VersionProfileRecord profile = await database.UpdateAsync(db =>
            {
                VersionProfileRecord? profile = db.VersionProfiles.FirstOrDefault(p => p.Id == profileId)
                    ?? throw new FileNotFoundException("版本模板不存在。");
                profile.Name = Required(request.Name, "模板名称");
                profile.ProductName = request.ProductName ?? "";
                profile.BundleIdentifier = request.BundleIdentifier ?? "";
                profile.BundleVersion = string.IsNullOrWhiteSpace(request.BundleVersion) ? "1.0.0" : request.BundleVersion.Trim();
                profile.BuildNumber = string.IsNullOrWhiteSpace(request.BuildNumber) ? "1" : request.BuildNumber.Trim();
                profile.SyncBundleVersionFromUnity = request.SyncBundleVersionFromUnity;
                profile.AutoIncrementBuildNumber = request.AutoIncrementBuildNumber;
                AuthService.AddAudit(db, user.Id, user.UserName, "version-profile.update", "version-profile", profile.Id, $"更新版本模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(profile);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> DeleteVersionProfileAsync(string profileId, HttpContext context, AuthService auth, JsonDatabase database)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            VersionProfileRecord profile = await database.UpdateAsync(db =>
            {
                VersionProfileRecord? profile = db.VersionProfiles.FirstOrDefault(p => p.Id == profileId)
                    ?? throw new FileNotFoundException("版本模板不存在。");
                db.VersionProfiles.Remove(profile);
                AuthService.AddAudit(db, user.Id, user.UserName, "version-profile.delete", "version-profile", profile.Id, $"删除版本模板 {profile.Name}");
                return profile;
            });
            return Results.Ok(new { deleted = true, profile });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UploadConfigFileAsync(HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            IFormCollection form = await context.Request.ReadFormAsync();
            IFormFile? file = form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                throw new InvalidOperationException("没有收到文件。");
            }

            string fileName = SafeConfigFileName(file.FileName, "uploaded", "ios");
            string configRoot = options.AllowedConfigRoots.FirstOrDefault()
                ?? throw new InvalidOperationException("服务端没有配置允许的配置文件目录。");
            string configPath = ValidatePathUnderAllowedRoots(Path.Combine(configRoot, fileName), options.AllowedConfigRoots, "配置文件路径");

            string? dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            await using (var stream = new FileStream(configPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            await database.UpdateAsync(db =>
            {
                AuthService.AddAudit(db, user.Id, user.UserName, "config-file.upload", "config", configPath, $"上传配置文件 {configPath}");
                return true;
            });

            return Results.Ok(new { path = configPath, name = fileName });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static async Task<IResult> UploadSecretFileAsync(HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        try
        {
            IFormCollection form = await context.Request.ReadFormAsync();
            IFormFile? file = form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                throw new InvalidOperationException("没有收到文件。");
            }

            string fileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(fileName) ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fileName.Contains("..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("文件名不合法。");
            }

            string secretsDir = Path.Combine(options.DataRoot, "secrets");
            Directory.CreateDirectory(secretsDir);
            string filePath = Path.Combine(secretsDir, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            await database.UpdateAsync(db =>
            {
                AuthService.AddAudit(db, user.Id, user.UserName, "secret-file.upload", "file", filePath, $"上传密钥文件 {filePath}");
                return true;
            });

            return Results.Ok(new { path = filePath, name = fileName });
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    // ---- Data Export / Import ----

    /// <summary>
    /// 跨平台文件名提取：同时兼容 Windows 反斜杠和 Unix 正斜杠分隔符。
    /// Path.GetFileName 在 Unix 上不认识 '\'，会把整个 Windows 路径当作文件名。
    /// </summary>
    private static string GetCrossPlatformFileName(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        int lastSlash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
    }

    private static readonly string[] ExportCategories =
    [
        "projects", "configs", "projectProfiles", "unityProjectProfiles",
        "signingProfiles", "certificateProfiles", "versionProfiles", "notificationContacts", "emailSettings"
    ];

    private static async Task<IResult> ExportDataAsync(HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.CanManage(user)) return Results.Forbid();

        string[]? categories = await context.Request.ReadFromJsonAsync<string[]>();
        if (categories is null || categories.Length == 0)
        {
            categories = ExportCategories;
        }

        var result = await database.ReadAsync(db =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (string category in categories.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                switch (category.ToLowerInvariant())
                {
                    case "projects":
                        dict["projects"] = db.Projects.OrderBy(p => p.Name).ToList();
                        break;
                    case "configs":
                        dict["configs"] = db.Configs.OrderBy(c => c.Name).ToList();
                        break;
                    case "projectprofiles":
                        dict["projectProfiles"] = db.ProjectProfiles.OrderBy(p => p.Name).ToList();
                        break;
                    case "unityprojectprofiles":
                        dict["unityProjectProfiles"] = db.UnityProjectProfiles.OrderBy(u => u.Name).ToList();
                        break;
                    case "signingprofiles":
                        dict["signingProfiles"] = db.SigningProfiles.OrderBy(s => s.Name).ToList();
                        break;
                    case "certificateprofiles":
                        dict["certificateProfiles"] = db.CertificateProfiles.OrderBy(c => c.Name).ToList();
                        break;
                    case "versionprofiles":
                        dict["versionProfiles"] = db.VersionProfiles.OrderBy(v => v.Name).ToList();
                        break;
                    case "notificationcontacts":
                        dict["notificationContacts"] = db.NotificationContacts.OrderBy(c => c.Title).ToList();
                        break;
                    case "emailsettings":
                        dict["emailSettings"] = db.EmailSettings;
                        break;
                }
            }

            // 收集所有密钥/配置文件路径并打包文件内容
            var bundledFiles = new Dictionary<string, string>();
            var pathFields = new List<string?>();
            pathFields.AddRange(db.CertificateProfiles.Select(c => c.AppStoreConnectApiKeyPath));
            pathFields.AddRange(db.CertificateProfiles.Select(c => c.GooglePlayServiceAccountJsonPath));
            pathFields.AddRange(db.SigningProfiles.Select(s => s.AndroidKeystoreName));
            // 配置文件（build-*.json）也要打包，否则跨机器导入后 ConfigPath 指向不存在的文件
            pathFields.AddRange(db.Configs.Select(c => c.ConfigPath));

            foreach (string? path in pathFields)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                // 候选位置：原样路径、DataRoot/secrets/<文件名>（处理只存文件名的情况）
                string fileName = GetCrossPlatformFileName(path);
                var candidates = new List<string>();
                if (Path.IsPathRooted(path)) candidates.Add(path);
                if (!string.IsNullOrWhiteSpace(fileName))
                    candidates.Add(Path.Combine(options.DataRoot, "secrets", fileName));

                foreach (string candidate in candidates)
                {
                    string full;
                    try { full = Path.GetFullPath(candidate); }
                    catch { continue; }
                    if (bundledFiles.ContainsKey(full)) continue;
                    if (File.Exists(full))
                    {
                        byte[] bytes = File.ReadAllBytes(full);
                        bundledFiles[full] = Convert.ToBase64String(bytes);
                        break;
                    }
                }
            }

            if (bundledFiles.Count > 0)
            {
                dict["_bundledFiles"] = bundledFiles;
            }

            return dict;
        });

        return Results.Ok(result);
    }

    private static async Task<IResult> ImportDataAsync(HttpContext context, AuthService auth, JsonDatabase database, BuildServerOptions options)
    {
        CurrentUser? user = await auth.GetUserAsync(context);
        if (user is null) return Results.Unauthorized();
        if (!AuthService.IsAdmin(user)) return Results.Forbid();

        try
        {
            JsonNode? payload = await context.Request.ReadFromJsonAsync<JsonNode>();
            if (payload is null)
            {
                throw new InvalidOperationException("导入数据为空。");
            }

            // 解包并写入密钥/配置文件到目标机器，构建路径映射表
            var pathRemap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (payload["_bundledFiles"] is JsonNode bfNode && bfNode.AsObject().Count > 0)
            {
                string secretsDir = Path.Combine(options.DataRoot, "secrets");
                Directory.CreateDirectory(secretsDir);
                string configRoot = options.AllowedConfigRoots.FirstOrDefault() ?? Path.Combine(options.DataRoot, "configs");
                Directory.CreateDirectory(configRoot);

                foreach (var entry in bfNode.AsObject())
                {
                    string sourcePath = entry.Key;
                    string? base64 = entry.Value?.GetValue<string>();
                    if (string.IsNullOrEmpty(base64)) continue;
                    string fileName = GetCrossPlatformFileName(sourcePath);
                    if (string.IsNullOrWhiteSpace(fileName)) continue;

                    // 配置文件（.json）写入 configRoot，密钥文件写入 secretsDir
                    bool isConfigFile = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        && !fileName.EndsWith(".keystore", StringComparison.OrdinalIgnoreCase)
                        && !fileName.EndsWith(".p8", StringComparison.OrdinalIgnoreCase)
                        && (fileName.StartsWith("build-", StringComparison.OrdinalIgnoreCase)
                            || sourcePath.Contains("configs", StringComparison.OrdinalIgnoreCase));
                    string targetDir = isConfigFile ? configRoot : secretsDir;
                    string targetPath = Path.Combine(targetDir, fileName);
                    File.WriteAllBytes(targetPath, Convert.FromBase64String(base64));
                    pathRemap[sourcePath] = targetPath;
                }
            }

            // 辅助：重写路径字段
            string RemapPath(string? original)
            {
                if (string.IsNullOrWhiteSpace(original)) return "";
                // 精确匹配
                if (pathRemap.TryGetValue(original, out var mapped)) return mapped;
                // 尝试文件名匹配（跨平台路径差异）
                string fileName = GetCrossPlatformFileName(original);
                foreach (var kv in pathRemap)
                {
                    if (GetCrossPlatformFileName(kv.Key) == fileName) return kv.Value;
                }
                return original;
            }

            var result = await database.UpdateAsync(db =>
            {
                int imported = 0;

                if (payload["projects"] is JsonNode projectsNode)
                {
                    var items = projectsNode.Deserialize<List<ProjectRecord>>(CamelizeOptions) ?? [];
                    foreach (var item in items)
                    {
                        if (!db.Projects.Any(p => p.Id == item.Id))
                        {
                            db.Projects.Add(item);
                            imported++;
                        }
                    }
                }

                if (payload["configs"] is JsonNode configsNode)
                {
                    var items = configsNode.Deserialize<List<BuildConfigRecord>>(CamelizeOptions) ?? [];
                    foreach (var item in items)
                    {
                        if (!db.Configs.Any(c => c.Id == item.Id))
                        {
                            item.ConfigPath = RemapPath(item.ConfigPath);
                            db.Configs.Add(item);
                            imported++;
                        }
                    }
                }

                if (payload["projectProfiles"] is JsonNode ppNode)
                {
                    var items = ppNode.Deserialize<List<ProjectProfileRecord>>(CamelizeOptions) ?? [];
                    foreach (var item in items)
                    {
                        if (!db.ProjectProfiles.Any(p => p.Id == item.Id))
                        {
                            db.ProjectProfiles.Add(item);
                            imported++;
                        }
                    }
                }

                if (payload["unityProjectProfiles"] is JsonNode upNode)
                {
                    var items = upNode.Deserialize<List<UnityProjectProfileRecord>>(CamelizeOptions) ?? [];
                    foreach (var item in items)
                    {
                        if (!db.UnityProjectProfiles.Any(p => p.Id == item.Id))
                        {
                            db.UnityProjectProfiles.Add(item);
                            imported++;
                        }
                    }
                }

                if (payload["signingProfiles"] is JsonNode spNode)
                {
                    var items = spNode.Deserialize<List<SigningProfileRecord>>(CamelizeOptions) ?? [];
                    foreach (var item in items)
                    {
                        if (!db.SigningProfiles.Any(p => p.Id == item.Id))
                        {
                            item.AndroidKeystoreName = RemapPath(item.AndroidKeystoreName);
                            db.SigningProfiles.Add(item);
                            imported++;
                        }
                    }
                }

                if (payload["certificateProfiles"] is JsonNode cpNode)
                {
                    var items = cpNode.Deserialize<List<CertificateProfileRecord>>(CamelizeOptions) ?? [];
                    foreach (var item in items)
                    {
                        if (!db.CertificateProfiles.Any(p => p.Id == item.Id))
                        {
                            item.AppStoreConnectApiKeyPath = RemapPath(item.AppStoreConnectApiKeyPath);
                            item.GooglePlayServiceAccountJsonPath = RemapPath(item.GooglePlayServiceAccountJsonPath);
                            db.CertificateProfiles.Add(item);
                            imported++;
                        }
                    }
                }

                if (payload["versionProfiles"] is JsonNode vNode)
                {
                    var items = vNode.Deserialize<List<VersionProfileRecord>>(CamelizeOptions) ?? [];
                    foreach (var item in items)
                    {
                        if (!db.VersionProfiles.Any(p => p.Id == item.Id))
                        {
                            db.VersionProfiles.Add(item);
                            imported++;
                        }
                    }
                }

                if (payload["notificationContacts"] is JsonNode ncNode)
                {
                    var items = ncNode.Deserialize<List<NotificationContactRecord>>(CamelizeOptions) ?? [];
                    foreach (var item in items)
                    {
                        if (!db.NotificationContacts.Any(c => c.Id == item.Id))
                        {
                            db.NotificationContacts.Add(item);
                            imported++;
                        }
                    }
                }

                if (payload["emailSettings"] is JsonNode esNode)
                {
                    var settings = esNode.Deserialize<EmailSettingsRecord>(CamelizeOptions);
                    if (settings is not null)
                    {
                        db.EmailSettings = settings;
                        imported++;
                    }
                }

                AuthService.AddAudit(db, user.Id, user.UserName, "data.import", "system", "import", $"导入数据 {imported} 条");
                return new { imported };
            });

            return Results.Ok(result);
        }
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ApiDiagnostics.ClientError(context, ex);
        }
    }

    private static long EstimateLogSize(BuildJobRecord job)
    {
        try
        {
            return File.Exists(job.WorkerLogPath) ? new FileInfo(job.WorkerLogPath).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static bool IsCompleted(string status)
    {
        return status is BuildStatuses.Succeeded or BuildStatuses.Failed or BuildStatuses.Canceled;
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
            user.AllowedProjectIds,
            user.Enabled,
            user.CreatedAt
        };
    }

    private static List<string> NormalizeAllowedProjectIds(IEnumerable<string>? values, BuildServerDatabase db)
    {
        if (values is null)
        {
            return [];
        }

        List<string> ids = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (string id in ids)
        {
            if (!db.Projects.Any(project => project.Id == id))
            {
                throw new InvalidOperationException($"Project does not exist: {id}");
            }
        }

        return ids;
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
        else if (buildPlatform == BuildPlatforms.Tiktok)
        {
            AddTiktokConfig(json, request);
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
        json["appStoreConnectUploadTarget"] = string.IsNullOrWhiteSpace(request.AppStoreConnectUploadTarget) ? "testflight" : request.AppStoreConnectUploadTarget.Trim().ToLowerInvariant();
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
        if (request.GooglePlayUploadEnabled && request.GooglePlayUserFraction is <= 0 or > 1)
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

    private static void AddTiktokConfig(JsonObject json, BuildConfigFileRequest request)
    {
        json["tiktokAppId"] = (request.TiktokAppId ?? "").Trim();
        json["tiktokAccessToken"] = (request.TiktokAccessToken ?? "").Trim();
        json["tiktokGameName"] = (request.TiktokGameName ?? "").Trim();
        json["tiktokWebglOutputDirectory"] = (request.TiktokWebglOutputDirectory ?? "").Trim();
        json["tiktokUploadEnabled"] = request.TiktokUploadEnabled;
        json["tiktokApiEndpoint"] = (request.TiktokApiEndpoint ?? "").Trim();
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
            throw new InvalidOperationException("Build Platform 只能是 ios、android 或 tiktok。");
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
        return buildPlatform switch
        {
            BuildPlatforms.Android => DefaultUnityBuildMethods.Android,
            BuildPlatforms.Tiktok => DefaultUnityBuildMethods.Tiktok,
            _ => DefaultUnityBuildMethods.Ios
        };
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

    private static bool IsAllowedArtifactPath(string path, BuildJobRecord job, BuildServerOptions options)
    {
        string? jobRoot = string.IsNullOrWhiteSpace(job.WorkerLogPath) ? null : Path.GetDirectoryName(job.WorkerLogPath);
        bool underJobRoot = (!string.IsNullOrWhiteSpace(job.ArtifactRoot) && IsSameOrChild(path, job.ArtifactRoot)) ||
                            (!string.IsNullOrWhiteSpace(jobRoot) && IsSameOrChild(path, jobRoot));
        bool underAllowedRoot = options.AllowedArtifactsRoots.Count == 0 ||
                                options.AllowedArtifactsRoots.Any(root => IsSameOrChild(path, root));
        return underJobRoot && underAllowedRoot;
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

    private static bool CanAccessProject(CurrentUser user, string projectId)
    {
        return user.AllowedProjectIds is null ||
               user.AllowedProjectIds.Count == 0 ||
               user.AllowedProjectIds.Contains(projectId, StringComparer.OrdinalIgnoreCase);
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

    private static void SetNoStoreHeaders(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }

    private static void WriteTextAtomically(string targetPath, string content)
    {
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = $"{targetPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, content);
            File.Move(tempPath, targetPath, overwrite: true);
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
