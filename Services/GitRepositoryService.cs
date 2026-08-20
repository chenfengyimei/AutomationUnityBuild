namespace AutomationUnityBuildIOS;

internal sealed class GitRepositoryService(BuildRunContext context)
{
    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildPaths _paths => context.Paths;
    private ProcessRunner _processRunner => context.ProcessRunner;
    private BuildLogger _logger => context.Logger;

    public async Task SyncAsync()
    {
        GitRepositoryPolicy.Validate(_config, _logger);
        IReadOnlyDictionary<string, string> gitEnvironment = GitEnvironment();

        if (!Directory.Exists(Path.Combine(_paths.RepositoryRoot, ".git")))
        {
            _logger.Info($"仓库不存在，准备 clone 到: {_paths.RepositoryRoot}");
            Directory.CreateDirectory(_paths.WorkspaceRoot);

            if (Directory.Exists(_paths.RepositoryRoot) && Directory.EnumerateFileSystemEntries(_paths.RepositoryRoot).Any())
            {
                string gitDir = Path.Combine(_paths.RepositoryRoot, ".git");
                if (!Directory.Exists(gitDir) && !File.Exists(gitDir))
                {
                    _logger.Warn($"目标目录已存在且非空，但没有 .git，可能是上次 clone 中途失败: {_paths.RepositoryRoot}");
                    throw new InvalidOperationException(
                        $"Git 仓库目录已存在但不是有效的 Git 仓库（缺少 .git），可能是上次 clone 中途失败。{Environment.NewLine}" +
                        $"目录: {_paths.RepositoryRoot}{Environment.NewLine}" +
                        "处理方式：删除该目录后重新运行，或手动确认目录内容后删除。");
                }
            }

            await RunGitCommandAsync(
                ["clone", "--branch", _config.Branch, _config.RepositoryUrl, _paths.RepositoryRoot],
                _paths.WorkspaceRoot,
                gitEnvironment);
            return;
        }

        _logger.Info($"仓库已存在，准备更新: {_paths.RepositoryRoot}");
        await ValidateExistingRepositoryRemoteAsync(gitEnvironment);
        await RunGitCommandAsync(["fetch", "--prune", "origin"], _paths.RepositoryRoot, gitEnvironment);
        await RunGitCommandAsync(["checkout", _config.Branch], _paths.RepositoryRoot, gitEnvironment);

        if (_config.ResetRepository)
        {
            _logger.Warn($"resetRepository=true，将强制重置到 origin/{_config.Branch} 并清理未跟踪文件。");
            await RunGitCommandAsync(["reset", "--hard", $"origin/{_config.Branch}"], _paths.RepositoryRoot, gitEnvironment);
            await RunGitCommandAsync(GitCleanArguments(), _paths.RepositoryRoot, gitEnvironment);
        }
        else
        {
            await RunGitCommandAsync(["pull", "--ff-only", "origin", _config.Branch], _paths.RepositoryRoot, gitEnvironment);
        }
    }

    private async Task RunGitCommandAsync(IReadOnlyList<string> args, string workingDirectory, IReadOnlyDictionary<string, string> gitEnvironment)
    {
        try
        {
            await _processRunner.RunAsync("git", args, workingDirectory, environment: gitEnvironment);
        }
        catch (InvalidOperationException ex) when (GitAuthFailureDetector.IsAuthFailure(ex.Message))
        {
            string redactedRepoUrl = GitRepositoryUrl.Redact(_config.RepositoryUrl);
            string stderrSummary = GitAuthFailureDetector.ExtractStderrSummary(ex.Message);
            throw new InvalidOperationException(
                $"Git 认证失败：构建机器上的 Git 凭据可能已过期或无效。{Environment.NewLine}" +
                $"仓库: {redactedRepoUrl}{Environment.NewLine}" +
                $"分支: {_config.Branch}{Environment.NewLine}" +
                $"原始错误: {stderrSummary}{Environment.NewLine}" +
                $"解决方法：{Environment.NewLine}" +
                $"  1. SSH 方式：在构建机器上验证 SSH 密钥是否有效（ssh -T git@github.com）{Environment.NewLine}" +
                $"  2. HTTPS 方式：在构建机器上更新 Git 凭据（git credential reject / git credential approve）{Environment.NewLine}" +
                $"  3. GitHub PAT：生成新 Token 并更新 git credential helper 或 URL{Environment.NewLine}" +
                $"  4. 手动验证：在构建机器上以构建服务账号执行 git clone {redactedRepoUrl} 确认凭据可用",
                ex);
        }
    }

    private async Task ValidateExistingRepositoryRemoteAsync(IReadOnlyDictionary<string, string> gitEnvironment)
    {
        if (_options.DryRun)
        {
            _logger.Info("[dry-run] 检查已有仓库 remote origin 是否匹配配置。");
            return;
        }

        string originUrl = (await _processRunner.RunCaptureStdoutAsync(
            "git",
            ["remote", "get-url", "origin"],
            _paths.RepositoryRoot,
            gitEnvironment)).Trim();

        string configuredKey = GitRepositoryUrl.CanonicalKey(_config.RepositoryUrl);
        string originKey = GitRepositoryUrl.CanonicalKey(originUrl);
        if (string.Equals(configuredKey, originKey, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info("Git remote origin 与配置仓库匹配。");
            return;
        }

        throw new InvalidOperationException(
            $"已有仓库目录的 remote origin 与配置不一致，已停止打包，避免打错项目。{Environment.NewLine}" +
            $"仓库目录: {_paths.RepositoryRoot}{Environment.NewLine}" +
            $"配置仓库: {GitRepositoryUrl.Redact(_config.RepositoryUrl)}{Environment.NewLine}" +
            $"当前 origin: {GitRepositoryUrl.Redact(originUrl)}{Environment.NewLine}" +
            "处理方式：修改配置里的 projectDirectoryName 使用新的目录，或手动确认后删除/重命名旧仓库目录。");
    }

    private IReadOnlyList<string> GitCleanArguments()
    {
        var args = new List<string> { "clean", "-fdx" };
        if (!_config.PreserveUnityLibraryOnReset)
        {
            return args;
        }

        string? excludePattern = UnityLibraryGitCleanExcludePattern();
        if (excludePattern is null)
        {
            _logger.Warn("无法计算 Unity Library 相对仓库路径，将按原始 git clean -fdx 清理。");
            return args;
        }

        args.AddRange(["-e", excludePattern]);
        _logger.Info($"保留 Unity Library 缓存: {Path.Combine(_paths.UnityProjectRoot, "Library")}");
        return args;
    }

    private string? UnityLibraryGitCleanExcludePattern()
    {
        string repositoryRoot = Path.GetFullPath(_paths.RepositoryRoot);
        string libraryPath = Path.GetFullPath(Path.Combine(_paths.UnityProjectRoot, "Library"));
        string relativePath = Path.GetRelativePath(repositoryRoot, libraryPath);

        if (Path.IsPathRooted(relativePath) ||
            relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return null;
        }

        return relativePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .TrimEnd('/') + "/";
    }

    private IReadOnlyDictionary<string, string> GitEnvironment()
    {
        var environment = new Dictionary<string, string>(_config.Environment, StringComparer.OrdinalIgnoreCase);
        environment.TryAdd("GIT_TERMINAL_PROMPT", "0");
        return environment;
    }
}

internal static class GitAuthFailureDetector
{
    private static readonly string[] AuthFailurePatterns =
    [
        "Authentication failed",
        "could not read Username",
        "could not read Password",
        "Invalid username or token",
        "Invalid username or password",
        "Permission denied (publickey)",
        "Permission denied",
        "access denied",
        "fatal: unable to access",
        "403 Forbidden",
        "401 Unauthorized",
        "Support for password authentication was removed",
        "Personal access tokens with read:org",
    ];

    public static bool IsAuthFailure(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        foreach (string pattern in AuthFailurePatterns)
        {
            if (message.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string ExtractStderrSummary(string exceptionMessage)
    {
        if (string.IsNullOrWhiteSpace(exceptionMessage))
        {
            return "（无详细信息）";
        }

        int newlineIndex = exceptionMessage.IndexOf('\n');
        string detail = newlineIndex >= 0
            ? exceptionMessage[(newlineIndex + 1)..].Trim()
            : exceptionMessage;

        if (string.IsNullOrWhiteSpace(detail))
        {
            return "（无 stderr 输出）";
        }

        string[] lines = detail.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var relevant = lines
            .Where(line => !line.StartsWith("命令执行失败", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToArray();

        return relevant.Length > 0
            ? string.Join(Environment.NewLine, relevant)
            : detail;
    }
}
