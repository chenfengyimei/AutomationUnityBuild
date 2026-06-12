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
        ValidateRepositoryUrlForGit();
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

        string configuredKey = CanonicalRepositoryKey(_config.RepositoryUrl);
        string originKey = CanonicalRepositoryKey(originUrl);
        if (string.Equals(configuredKey, originKey, StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info("Git remote origin 与配置仓库匹配。");
            return;
        }

        throw new InvalidOperationException(
            $"已有仓库目录的 remote origin 与配置不一致，已停止打包，避免打错项目。{Environment.NewLine}" +
            $"仓库目录: {_paths.RepositoryRoot}{Environment.NewLine}" +
            $"配置仓库: {RedactRepositoryUrl(_config.RepositoryUrl)}{Environment.NewLine}" +
            $"当前 origin: {RedactRepositoryUrl(originUrl)}{Environment.NewLine}" +
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

    private static string CanonicalRepositoryKey(string url)
    {
        string normalized = ConfigValueNormalizer.NormalizeRepositoryUrl(url).Trim().TrimEnd('/', '\\');
        if (normalized.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            int atIndex = normalized.IndexOf('@');
            int colonIndex = normalized.IndexOf(':', atIndex + 1);
            if (atIndex >= 0 && colonIndex > atIndex)
            {
                string host = normalized[(atIndex + 1)..colonIndex];
                string path = normalized[(colonIndex + 1)..];
                return TrimGitSuffix($"{host}/{path}").ToLowerInvariant();
            }
        }

        if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
        {
            return TrimGitSuffix($"{uri.Host}{uri.AbsolutePath}").TrimEnd('/').ToLowerInvariant();
        }

        return TrimGitSuffix(normalized).ToLowerInvariant();
    }

    private static string TrimGitSuffix(string value)
    {
        return value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? value[..^4]
            : value;
    }

    private static string RedactRepositoryUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || string.IsNullOrEmpty(uri.UserInfo))
        {
            return url;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = "***",
            Password = ""
        };
        return builder.Uri.ToString();
    }

    private void ValidateRepositoryUrlForGit()
    {
        if (_config.RepositoryUrl.Any(char.IsWhiteSpace) ||
            _config.RepositoryUrl.Contains('[') ||
            _config.RepositoryUrl.Contains(']'))
        {
            throw new InvalidOperationException(
                $"Git 仓库地址格式不正确: {_config.RepositoryUrl}{Environment.NewLine}" +
                "请填写 git clone 可直接使用的地址，例如 https://github.com/owner/repo.git 或 git@github.com:owner/repo.git。");
        }

        if (_config.RepositoryUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info("GitHub HTTPS 地址不会支持账号密码登录。公开仓库可直接 clone；私有仓库建议改用 SSH 地址 git@github.com:owner/repo.git。");
        }
    }

    private IReadOnlyDictionary<string, string> GitEnvironment()
    {
        var environment = new Dictionary<string, string>(_config.Environment, StringComparer.OrdinalIgnoreCase);
        environment.TryAdd("GIT_TERMINAL_PROMPT", "0");
        return environment;
    }
}
