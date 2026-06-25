using System.Text.Json;
using System.Text.Json.Nodes;
using BuildServer.Persistence;
using BuildServer.Security;

namespace BuildServer.Services;

public sealed class BuildQueueService(JsonDatabase database, BuildServerOptions options)
{
    private static readonly JsonSerializerOptions IndentedCamelizeOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<BuildJobRecord> EnqueueAsync(
        StartBuildRequest request,
        CurrentUser user,
        string source,
        McpClientRecord? mcpClient = null)
    {
        return await database.UpdateAsync(db =>
        {
            ProjectRecord project = db.Projects.FirstOrDefault(project => project.Id == request.ProjectId && project.Enabled)
                ?? throw new InvalidOperationException("项目不存在或已禁用。");
            if (!IsProjectAllowed(user, project.Id))
            {
                throw new UnauthorizedAccessException("当前用户没有操作这个项目的权限。");
            }

            BuildConfigRecord config = db.Configs.FirstOrDefault(config =>
                    config.Id == request.ConfigId &&
                    config.ProjectId == request.ProjectId &&
                    config.Enabled)
                ?? throw new InvalidOperationException("配置不存在或已禁用。");

            if (source == BuildSources.Mcp)
            {
                ValidateMcpAccess(request, project, config, mcpClient);
            }

            string clientRequestId = NormalizeClientRequestId(request.ClientRequestId);
            if (!string.IsNullOrWhiteSpace(clientRequestId))
            {
                BuildJobRecord? existingJob = db.Jobs
                    .OrderByDescending(job => job.CreatedAt)
                    .FirstOrDefault(job =>
                        string.Equals(job.ClientRequestId, clientRequestId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(job.RequestedByUserId, user.Id, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(job.Source, source, StringComparison.OrdinalIgnoreCase));
                if (existingJob is not null)
                {
                    return existingJob;
                }
            }

            string branch = string.IsNullOrWhiteSpace(request.Branch)
                ? project.DefaultBranch
                : request.Branch.Trim();
            EnsureBranchAllowed(project, branch);

            if (!File.Exists(ExpandPath(config.ConfigPath)))
            {
                throw new FileNotFoundException($"配置文件不存在: {config.ConfigPath}");
            }

            string buildNumber = string.IsNullOrWhiteSpace(request.BuildNumber)
                ? project.NextBuildNumber.ToString()
                : request.BuildNumber.Trim();
            string buildPlatform = string.IsNullOrWhiteSpace(config.BuildPlatform)
                ? BuildPlatforms.Ios
                : config.BuildPlatform;

            if (string.IsNullOrWhiteSpace(request.BuildNumber) && !request.DryRun)
            {
                project.NextBuildNumber++;
            }

            string jobId = Ids.New("job");
            string jobRoot = Path.Combine(options.DataRoot, "jobs", jobId);
            Directory.CreateDirectory(jobRoot);
            string materializedConfigPath = Path.Combine(jobRoot, "build-config.json");
            MaterializeConfig(project, config, materializedConfigPath, branch, buildNumber);

            var job = new BuildJobRecord
            {
                Id = jobId,
                ProjectId = project.Id,
                ConfigId = config.Id,
                RequestedByUserId = user.Id,
                Source = source,
                Status = BuildStatuses.Queued,
                BuildPlatform = buildPlatform,
                Branch = branch,
                BuildNumber = buildNumber,
                DryRun = request.DryRun,
                SkipGit = request.SkipGit,
                SkipUnity = request.SkipUnity,
                SkipXcode = request.SkipXcode,
                AllowNonMac = request.AllowNonMac,
                ClientRequestId = clientRequestId,
                Notes = request.Notes ?? "",
                MaterializedConfigPath = materializedConfigPath,
                WorkerLogPath = Path.Combine(jobRoot, "worker.log"),
                CreatedAt = DateTimeOffset.Now
            };

            db.Jobs.Add(job);
            AuthService.AddAudit(db, user.Id, user.UserName, "build.enqueue", "job", job.Id, $"创建打包任务 {project.Name}/{config.Name} platform={buildPlatform} branch={branch} build={buildNumber} source={source}");
            return job;
        });
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

    public async Task<bool> CancelQueuedAsync(string jobId, CurrentUser user)
    {
        return await database.UpdateAsync(db =>
        {
            BuildJobRecord? job = db.Jobs.FirstOrDefault(job => job.Id == jobId);
            if (job is null || job.Status != BuildStatuses.Queued)
            {
                return false;
            }

            job.Status = BuildStatuses.Canceled;
            job.FinishedAt = DateTimeOffset.Now;
            AuthService.AddAudit(db, user.Id, user.UserName, "build.cancel", "job", job.Id, "取消排队中的打包任务。");
            return true;
        });
    }

    private static void ValidateMcpAccess(
        StartBuildRequest request,
        ProjectRecord project,
        BuildConfigRecord config,
        McpClientRecord? client)
    {
        if (client is null || !client.CanStartBuild)
        {
            throw new UnauthorizedAccessException("MCP Client 没有发起打包权限。");
        }

        if (client.AllowedProjectIds.Count > 0 && !client.AllowedProjectIds.Contains(project.Id, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("MCP Client 不允许操作这个项目。");
        }

        if (!config.AllowMcpBuild)
        {
            throw new UnauthorizedAccessException("这个配置不允许 MCP 发起打包。");
        }

        if (!client.AllowFullBuild && !request.DryRun)
        {
            throw new UnauthorizedAccessException("当前 MCP Client 只允许 dry-run。");
        }
    }

    private static void EnsureBranchAllowed(ProjectRecord project, string branch)
    {
        if (project.AllowedBranches.Count == 0 ||
            project.AllowedBranches.Any(pattern => BranchMatches(pattern, branch)))
        {
            return;
        }

        throw new InvalidOperationException($"分支 {branch} 不在项目允许分支内。");
    }

    private static bool BranchMatches(string pattern, string branch)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        pattern = pattern.Trim();
        if (pattern == "*")
        {
            return true;
        }

        if (!pattern.Contains('*'))
        {
            return string.Equals(pattern, branch, StringComparison.OrdinalIgnoreCase);
        }

        string[] parts = pattern.Split('*', 2);
        return branch.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase) &&
               branch.EndsWith(parts[1], StringComparison.OrdinalIgnoreCase);
    }

    private static void MaterializeConfig(ProjectRecord project, BuildConfigRecord config, string targetPath, string branch, string buildNumber)
    {
        string fullSourcePath = ExpandPath(config.ConfigPath);
        JsonObject json = JsonNode.Parse(File.ReadAllText(fullSourcePath))?.AsObject()
            ?? throw new InvalidOperationException($"配置文件不是有效 JSON: {config.ConfigPath}");

        json["configName"] = config.Name;
        json["buildPlatform"] = string.IsNullOrWhiteSpace(config.BuildPlatform) ? BuildPlatforms.Ios : config.BuildPlatform;
        json["repositoryUrl"] = project.RepositoryUrl;
        json["allowedRepositoryUrls"] = new JsonArray(project.RepositoryUrl);
        json["branch"] = branch;
        json["workspaceRoot"] = project.WorkspaceRoot;
        json["allowedWorkspaceRoots"] = new JsonArray(project.WorkspaceRoot);
        json["artifactsRoot"] = project.ArtifactsRoot;
        json["allowedArtifactsRoots"] = new JsonArray(project.ArtifactsRoot);
        json["buildNumber"] = buildNumber;
        json["autoIncrementBuildNumber"] = false;
        json["saveConfigSnapshot"] = true;
        NormalizeUnityExecutableForCurrentHost(json);

        if (Path.GetDirectoryName(targetPath) is string targetDir && targetDir.Length > 0)
        {
            Directory.CreateDirectory(targetDir);
        }
        WriteTextAtomically(
            targetPath,
            json.ToJsonString(IndentedCamelizeOptions) + Environment.NewLine);
    }

    private static void WriteTextAtomically(string targetPath, string content)
    {
        string? targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            Directory.CreateDirectory(targetDir);
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

    private static bool IsProjectAllowed(CurrentUser user, string projectId)
    {
        return user.AllowedProjectIds is null ||
               user.AllowedProjectIds.Count == 0 ||
               user.AllowedProjectIds.Contains(projectId, StringComparer.OrdinalIgnoreCase);
    }

    private static void NormalizeUnityExecutableForCurrentHost(JsonObject json)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string configuredPath = JsonString(json, "unityExecutablePath");
        if (!string.IsNullOrWhiteSpace(configuredPath) && !LooksLikeMacUnityExecutablePath(configuredPath))
        {
            return;
        }

        string unityVersion = JsonString(json, "unityVersion");
        if (string.IsNullOrWhiteSpace(unityVersion) && !string.IsNullOrWhiteSpace(configuredPath))
        {
            unityVersion = ExtractUnityVersionFromMacPath(configuredPath);
        }

        string resolvedPath = ResolveWindowsUnityExecutable(unityVersion);
        if (!string.IsNullOrWhiteSpace(resolvedPath))
        {
            json["unityExecutablePath"] = resolvedPath;
        }
    }

    private static bool LooksLikeMacUnityExecutablePath(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.StartsWith("/Applications/Unity/Hub/Editor/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/Unity.app/Contents/MacOS/Unity", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractUnityVersionFromMacPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        const string marker = "/Editor/";
        int markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return "";
        }

        string afterMarker = normalized[(markerIndex + marker.Length)..];
        int separatorIndex = afterMarker.IndexOf('/');
        return separatorIndex <= 0 ? "" : afterMarker[..separatorIndex].Trim();
    }

    private static string ResolveWindowsUnityExecutable(string unityVersion)
    {
        string[] searchRoots = WindowsUnityEditorSearchRoots();
        if (!string.IsNullOrWhiteSpace(unityVersion))
        {
            foreach (string root in searchRoots)
            {
                string candidate = Path.Combine(root, unityVersion.Trim(), "Editor", "Unity.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return searchRoots.Length == 0
                ? ""
                : Path.Combine(searchRoots[0], unityVersion.Trim(), "Editor", "Unity.exe");
        }

        foreach (string root in searchRoots)
        {
            DirectoryInfo? latest = LatestUnityEditorDirectory(root);
            if (latest is not null)
            {
                return Path.Combine(latest.FullName, "Editor", "Unity.exe");
            }
        }

        return "";
    }

    private static string[] WindowsUnityEditorSearchRoots()
    {
        string[] programRoots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetEnvironmentVariable("ProgramW6432") ?? ""
        ];

        return programRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(root => Path.Combine(root, "Unity", "Hub", "Editor"))
            .ToArray();
    }

    private static DirectoryInfo? LatestUnityEditorDirectory(string editorRoot)
    {
        if (!Directory.Exists(editorRoot))
        {
            return null;
        }

        return new DirectoryInfo(editorRoot)
            .EnumerateDirectories()
            .OrderByDescending(directory => directory.Name, Comparer<string>.Create(CompareUnityVersionNames))
            .FirstOrDefault();
    }

    private static int CompareUnityVersionNames(string? left, string? right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        int[] leftNumbers = ExtractVersionNumbers(left);
        int[] rightNumbers = ExtractVersionNumbers(right);
        int length = Math.Max(leftNumbers.Length, rightNumbers.Length);
        for (int index = 0; index < length; index++)
        {
            int leftValue = index < leftNumbers.Length ? leftNumbers[index] : 0;
            int rightValue = index < rightNumbers.Length ? rightNumbers[index] : 0;
            int comparison = leftValue.CompareTo(rightValue);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static int[] ExtractVersionNumbers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        List<int> numbers = [];
        int current = 0;
        bool readingNumber = false;
        bool overflow = false;

        foreach (char character in value)
        {
            if (char.IsDigit(character))
            {
                int digit = character - '0';
                readingNumber = true;
                if (current > (int.MaxValue - digit) / 10)
                {
                    overflow = true;
                    current = int.MaxValue;
                }
                else if (!overflow)
                {
                    current = (current * 10) + digit;
                }
                continue;
            }

            if (readingNumber)
            {
                numbers.Add(current);
                current = 0;
                readingNumber = false;
                overflow = false;
            }
        }

        if (readingNumber)
        {
            numbers.Add(current);
        }

        return numbers.ToArray();
    }

    private static string JsonString(JsonObject json, string propertyName)
    {
        return json[propertyName]?.GetValue<string>()?.Trim() ?? "";
    }

    public static string ExpandPath(string path)
    {
        return Path.GetFullPath(BuildServerEnvironment.ExpandHome(path));
    }
}
