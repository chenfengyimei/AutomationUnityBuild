using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        app.MapPost("/api/config-files", CreateConfigFileAsync);

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

    private static async Task<IResult> LoginAsync(LoginRequest request, HttpContext context, AuthService auth, LoginRateLimiter limiter)
    {
        string limiterKey = $"{context.Connection.RemoteIpAddress}|{request.UserName}";
        if (!limiter.IsAllowed(limiterKey))
        {
            return Results.Json(new
            {
                error = "登录失败次数过多，请稍后再试。"
            }, statusCode: StatusCodes.Status429TooManyRequests);
        }

        UserRecord? user = await auth.ValidateLoginAsync(request.UserName, request.Password);
        if (user is null)
        {
            limiter.RecordFailure(limiterKey);
            return Results.Json(new
            {
                error = "账号或密码错误。请确认账号是 admin，只复制 initial-admin.txt 里 admin password: 后面的密码，并且这个文件来自当前服务的数据目录。"
            }, statusCode: StatusCodes.Status401Unauthorized);
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
            return ClientInputError(ex);
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
        catch (Exception ex) when (IsClientInputError(ex))
        {
            return ClientInputError(ex);
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
                string configRoot = options.AllowedConfigRoots.FirstOrDefault()
                    ?? throw new InvalidOperationException("服务端没有配置允许的配置文件目录。");
                string fileName = SafeConfigFileName(request.FileName, configName);
                string configPath = ValidatePathUnderAllowedRoots(Path.Combine(configRoot, fileName), options.AllowedConfigRoots, "配置文件路径");
                if (File.Exists(configPath) && !request.OverwriteExisting)
                {
                    throw new InvalidOperationException($"配置文件已存在: {configPath}");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                File.WriteAllText(
                    configPath,
                    BuildConfigJson(project, request, configName).ToJsonString(new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }) + Environment.NewLine);

                BuildConfigRecord? existingConfig = db.Configs.FirstOrDefault(config =>
                    config.ProjectId == project.Id &&
                    string.Equals(config.ConfigPath, configPath, StringComparison.OrdinalIgnoreCase));
                if (existingConfig is not null)
                {
                    existingConfig.Name = configName;
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
            return ClientInputError(ex);
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
            return ClientInputError(ex);
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

    private static IResult ClientInputError(Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
    }

    private static JsonObject BuildConfigJson(ProjectRecord project, BuildConfigFileRequest request, string configName)
    {
        string projectDirectoryName = string.IsNullOrWhiteSpace(request.ProjectDirectoryName)
            ? DeriveProjectDirectoryName(project)
            : SafePathComponent(request.ProjectDirectoryName.Trim(), "仓库目录名");
        string unityProjectRelativePath = string.IsNullOrWhiteSpace(request.UnityProjectRelativePath)
            ? "."
            : request.UnityProjectRelativePath.Trim();
        string unityBuildMethod = string.IsNullOrWhiteSpace(request.UnityBuildMethod)
            ? "BuildAutomation.IOSBuilder.Build"
            : request.UnityBuildMethod.Trim();
        string exportMethod = ChoiceOrDefault(request.ExportMethod, ["development", "ad-hoc", "app-store", "enterprise"], "development", "Export Method");
        string signingStyle = ChoiceOrDefault(request.SigningStyle, ["automatic", "manual"], "automatic", "Signing Style");
        string teamId = (request.TeamId ?? "").Trim();
        string iosDeploymentTarget = (request.IosDeploymentTarget ?? "").Trim();
        string bundleVersion = string.IsNullOrWhiteSpace(request.BundleVersion) ? "1.0.0" : request.BundleVersion.Trim();
        string buildNumber = string.IsNullOrWhiteSpace(request.BuildNumber) ? "1" : request.BuildNumber.Trim();

        if (!string.IsNullOrWhiteSpace(teamId) && (teamId.Length != 10 || !teamId.All(char.IsLetterOrDigit)))
        {
            throw new InvalidOperationException("Apple Team ID 必须是 10 位，例如 ABCDE12345，不能填公司名称。");
        }

        if (!string.IsNullOrWhiteSpace(iosDeploymentTarget) && !Version.TryParse(iosDeploymentTarget, out _))
        {
            throw new InvalidOperationException("iOS Deployment Target 必须是版本号格式，例如 13.0。");
        }

        if (!request.SyncBundleVersionFromUnity && string.IsNullOrWhiteSpace(bundleVersion))
        {
            throw new InvalidOperationException("不同步 Unity 版本号时，必须填写 Bundle Version。");
        }

        return new JsonObject
        {
            ["configName"] = configName,
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
            ["xcodeOutputDirectory"] = "",
            ["archivePath"] = "",
            ["exportPath"] = "",
            ["logsDirectory"] = "",
            ["scheme"] = "Unity-iPhone",
            ["configuration"] = "Release",
            ["exportMethod"] = exportMethod,
            ["teamId"] = teamId,
            ["signingStyle"] = signingStyle,
            ["exportOptionsPlistPath"] = "",
            ["bundleIdentifier"] = (request.BundleIdentifier ?? "").Trim(),
            ["productName"] = (request.ProductName ?? "").Trim(),
            ["bundleVersion"] = bundleVersion,
            ["syncBundleVersionFromUnity"] = request.SyncBundleVersionFromUnity,
            ["buildNumber"] = buildNumber,
            ["autoIncrementBuildNumber"] = request.AutoIncrementBuildNumber,
            ["iosDeploymentTarget"] = iosDeploymentTarget,
            ["allowProvisioningUpdates"] = request.AllowProvisioningUpdates,
            ["resetRepository"] = false,
            ["preserveUnityLibraryOnReset"] = true,
            ["cleanXcodeOutputBeforeBuild"] = true,
            ["useWorkspaceIfPresent"] = true,
            ["generateExportOptionsPlist"] = true,
            ["copyArchiveToOrganizer"] = request.CopyArchiveToOrganizer,
            ["saveConfigSnapshot"] = true,
            ["compileBitcode"] = null,
            ["uploadSymbols"] = true,
            ["xcodeBuildSettings"] = new JsonObject(),
            ["environment"] = new JsonObject(),
            ["provisioningProfiles"] = new JsonObject()
        };
    }

    private static string SafeConfigFileName(string? requestedFileName, string configName)
    {
        string fileName = string.IsNullOrWhiteSpace(requestedFileName)
            ? $"build-ios.{SafeFileNamePart(configName)}.json"
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

    private static string Required(string? value, string field)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{field} 不能为空。")
            : value.Trim();
    }

    private static string ValidateGitUrl(string value, BuildServerOptions options)
    {
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
        return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectory(string path)
    {
        string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath + Path.DirectorySeparatorChar;
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
