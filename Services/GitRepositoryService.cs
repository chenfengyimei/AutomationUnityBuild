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
            await _processRunner.RunAsync(
                "git",
                ["clone", "--branch", _config.Branch, _config.RepositoryUrl, _paths.RepositoryRoot],
                _paths.WorkspaceRoot,
                environment: gitEnvironment);
            return;
        }

        _logger.Info($"仓库已存在，准备更新: {_paths.RepositoryRoot}");
        await ValidateExistingRepositoryRemoteAsync(gitEnvironment);
        await _processRunner.RunAsync("git", ["fetch", "--prune", "origin"], _paths.RepositoryRoot, environment: gitEnvironment);
        await _processRunner.RunAsync("git", ["checkout", _config.Branch], _paths.RepositoryRoot, environment: gitEnvironment);

        if (_config.ResetRepository)
        {
            _logger.Warn($"resetRepository=true，将强制重置到 origin/{_config.Branch} 并清理未跟踪文件。");
            await _processRunner.RunAsync("git", ["reset", "--hard", $"origin/{_config.Branch}"], _paths.RepositoryRoot, environment: gitEnvironment);
            await _processRunner.RunAsync("git", GitCleanArguments(), _paths.RepositoryRoot, environment: gitEnvironment);
        }
        else
        {
            await _processRunner.RunAsync("git", ["pull", "--ff-only", "origin", _config.Branch], _paths.RepositoryRoot, environment: gitEnvironment);
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

        if (Path.IsPathRooted(relativePath) || relativePath.StartsWith("..", StringComparison.Ordinal))
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
