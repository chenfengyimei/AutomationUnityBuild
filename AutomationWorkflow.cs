using System.Diagnostics;

namespace AutomationUnityBuildIOS;

internal sealed class AutomationWorkflow : IDisposable
{
    private readonly BuildConfig _config;
    private readonly CliOptions _options;
    private readonly BuildPaths _paths;
    private readonly ProcessRunner _processRunner;
    private readonly BuildLogger _logger;

    public AutomationWorkflow(BuildConfig config, CliOptions options)
    {
        _config = config;
        _options = options;
        _paths = BuildPaths.Create(config);
        _logger = BuildLogger.Create(_paths.AutomationLogPath, options.Verbose, options.DryRun);
        _processRunner = new ProcessRunner(options.DryRun, options.Verbose, _logger);
    }

    public async Task RunAsync()
    {
        var workflowStopwatch = Stopwatch.StartNew();
        try
        {
            _logger.StepStarted("自动化打包流程");
            PrintSummary();
            EnsureMacOrAllowed();
            await CheckPrerequisitesAsync();

            if (_options.DryRun)
            {
                _logger.Info("[dry-run] 跳过目录创建、清理和文件生成。");
            }
            else
            {
                RunStep("准备目录", PrepareDirectories);
            }

            if (!_options.SkipGit)
            {
                await RunStepAsync("同步 Unity 仓库", SyncRepositoryAsync);
            }
            else
            {
                _logger.Warn("跳过 Git 同步。");
            }

            if (!_options.SkipUnity)
            {
                await RunStepAsync("Unity 导出 iOS Xcode 工程", RunUnityBuildAsync);
            }
            else
            {
                _logger.Warn("跳过 Unity 导出。");
            }

            if (!_options.SkipXcode)
            {
                await RunStepAsync("Xcode archive/export", RunXcodeArchiveAndExportAsync);
            }
            else
            {
                _logger.Warn("跳过 Xcode 编译导出。");
            }

            _logger.StepCompleted("自动化打包流程", workflowStopwatch.Elapsed);
            Console.ForegroundColor = ConsoleColor.Green;
            _logger.Info("自动化打包流程完成。");
            Console.ResetColor();
            _logger.Info($"产物目录: {_paths.ArtifactsRunRoot}");
            _logger.Info($"导出目录: {_paths.ExportPath}");
            _logger.Info($"总日志: {_paths.AutomationLogPath}");
        }
        catch (Exception ex)
        {
            _logger.StepFailed("自动化打包流程", workflowStopwatch.Elapsed, ex);
            _logger.Error("自动化打包流程失败", ex);
            throw;
        }
    }

    public async Task CheckPrerequisitesAsync()
    {
        await RunStepAsync("检查环境", CheckPrerequisitesCoreAsync);
    }

    private async Task CheckPrerequisitesCoreAsync()
    {
        EnsureMacOrAllowed();

        await _processRunner.RunAsync("git", ["--version"]);

        if (!_options.SkipUnity)
        {
            if (!_options.DryRun && !File.Exists(_paths.UnityExecutable))
            {
                throw new FileNotFoundException($"找不到 Unity 可执行文件: {_paths.UnityExecutable}");
            }

            _logger.Info($"Unity: {_paths.UnityExecutable}");
        }

        if (!_options.SkipXcode)
        {
            await _processRunner.RunAsync("xcodebuild", ["-version"]);
        }
    }

    private void EnsureMacOrAllowed()
    {
        if (!OperatingSystem.IsMacOS() && !_options.AllowNonMac)
        {
            throw new PlatformNotSupportedException("iOS 自动打包必须在 macOS 上执行。Windows 可用于开发/发布这个工具；调试配置可加 --allow-non-mac --dry-run。");
        }
    }

    private void PrepareDirectories()
    {
        _logger.Info($"工作区目录: {_paths.WorkspaceRoot}");
        _logger.Info($"本次产物目录: {_paths.ArtifactsRunRoot}");
        _logger.Info($"日志目录: {_paths.LogsDirectory}");
        Directory.CreateDirectory(_paths.WorkspaceRoot);
        Directory.CreateDirectory(_paths.ArtifactsRunRoot);
        Directory.CreateDirectory(_paths.LogsDirectory);

        if (_config.CleanXcodeOutputBeforeBuild && Directory.Exists(_paths.XcodeOutputDirectory))
        {
            _logger.Warn($"清理旧 Xcode 输出目录: {_paths.XcodeOutputDirectory}");
            Directory.Delete(_paths.XcodeOutputDirectory, recursive: true);
        }

        Directory.CreateDirectory(_paths.XcodeOutputDirectory);
        Directory.CreateDirectory(_paths.ExportPath);
    }

    private async Task SyncRepositoryAsync()
    {
        if (!Directory.Exists(Path.Combine(_paths.RepositoryRoot, ".git")))
        {
            _logger.Info($"仓库不存在，准备 clone 到: {_paths.RepositoryRoot}");
            Directory.CreateDirectory(_paths.WorkspaceRoot);
            await _processRunner.RunAsync(
                "git",
                ["clone", "--branch", _config.Branch, _config.RepositoryUrl, _paths.RepositoryRoot],
                _paths.WorkspaceRoot,
                environment: _config.Environment);
            return;
        }

        _logger.Info($"仓库已存在，准备更新: {_paths.RepositoryRoot}");
        await _processRunner.RunAsync("git", ["fetch", "--prune", "origin"], _paths.RepositoryRoot, environment: _config.Environment);
        await _processRunner.RunAsync("git", ["checkout", _config.Branch], _paths.RepositoryRoot, environment: _config.Environment);

        if (_config.ResetRepository)
        {
            _logger.Warn($"resetRepository=true，将强制重置到 origin/{_config.Branch} 并清理未跟踪文件。");
            await _processRunner.RunAsync("git", ["reset", "--hard", $"origin/{_config.Branch}"], _paths.RepositoryRoot, environment: _config.Environment);
            await _processRunner.RunAsync("git", ["clean", "-fdx"], _paths.RepositoryRoot, environment: _config.Environment);
        }
        else
        {
            await _processRunner.RunAsync("git", ["pull", "--ff-only", "origin", _config.Branch], _paths.RepositoryRoot, environment: _config.Environment);
        }
    }

    private async Task RunUnityBuildAsync()
    {
        _logger.Info($"Unity 编辑器日志: {_paths.UnityLogPath}");
        _logger.Info($"Unity 进程输出日志: {_paths.UnityProcessLogPath}");

        var args = new List<string>
        {
            "-batchmode",
            "-quit",
            "-nographics",
            "-accept-apiupdate",
            "-projectPath",
            _paths.UnityProjectRoot,
            "-buildTarget",
            "iOS",
            "-executeMethod",
            _config.UnityBuildMethod,
            "-logFile",
            _paths.UnityLogPath,
            "-customBuildPath",
            _paths.XcodeOutputDirectory
        };

        AddUnityPair(args, "-customBuildNumber", _config.BuildNumber);
        AddUnityPair(args, "-customBundleVersion", _config.BundleVersion);
        AddUnityPair(args, "-customBundleIdentifier", _config.BundleIdentifier);
        AddUnityPair(args, "-customProductName", _config.ProductName);
        AddUnityPair(args, "-customAppleTeamId", _config.TeamId);

        await _processRunner.RunAsync(
            _paths.UnityExecutable,
            args,
            _paths.UnityProjectRoot,
            _paths.UnityProcessLogPath,
            _config.Environment);
    }

    private async Task RunXcodeArchiveAndExportAsync()
    {
        _logger.Info($"Xcode archive 日志: {_paths.XcodeArchiveLogPath}");
        _logger.Info($"Xcode export 日志: {_paths.XcodeExportLogPath}");

        string? workspacePath;
        string? projectPath;

        if (_options.DryRun)
        {
            workspacePath = null;
            projectPath = Path.Combine(_paths.XcodeOutputDirectory, "Unity-iPhone.xcodeproj");
        }
        else
        {
            workspacePath = _config.UseWorkspaceIfPresent
                ? Directory.EnumerateFiles(_paths.XcodeOutputDirectory, "*.xcworkspace", SearchOption.TopDirectoryOnly).FirstOrDefault()
                : null;

            projectPath = Directory.EnumerateFiles(_paths.XcodeOutputDirectory, "*.xcodeproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        }

        if (workspacePath is null && projectPath is null)
        {
            throw new FileNotFoundException($"Unity 导出的 Xcode 工程不存在: {_paths.XcodeOutputDirectory}");
        }

        var archiveArgs = new List<string>();
        if (workspacePath is not null)
        {
            _logger.Info($"使用 Xcode workspace: {workspacePath}");
            archiveArgs.AddRange(["-workspace", workspacePath]);
        }
        else
        {
            _logger.Info($"使用 Xcode project: {projectPath}");
            archiveArgs.AddRange(["-project", projectPath!]);
        }

        archiveArgs.AddRange([
            "-scheme", _config.Scheme,
            "-configuration", _config.Configuration,
            "-archivePath", _paths.ArchivePath
        ]);

        if (_config.AllowProvisioningUpdates)
        {
            archiveArgs.Add("-allowProvisioningUpdates");
        }

        AddXcodeSetting(archiveArgs, "DEVELOPMENT_TEAM", _config.TeamId);
        AddXcodeSetting(archiveArgs, "PRODUCT_BUNDLE_IDENTIFIER", _config.BundleIdentifier);
        AddXcodeSetting(archiveArgs, "CODE_SIGN_STYLE", ToXcodeSigningStyle(_config.SigningStyle));

        foreach ((string key, string value) in _config.XcodeBuildSettings)
        {
            AddXcodeSetting(archiveArgs, key, value);
        }

        archiveArgs.Add("archive");

        if (_config.GenerateExportOptionsPlist)
        {
            if (_options.DryRun)
            {
                _logger.Info($"[dry-run] 生成 ExportOptions.plist: {_paths.ExportOptionsPlistPath}");
            }
            else
            {
                ExportOptionsPlist.Write(_config, _paths.ExportOptionsPlistPath);
                _logger.Info($"生成 ExportOptions.plist: {_paths.ExportOptionsPlistPath}");
            }
        }

        await _processRunner.RunAsync(
            "xcodebuild",
            archiveArgs,
            _paths.XcodeOutputDirectory,
            _paths.XcodeArchiveLogPath,
            _config.Environment);

        await _processRunner.RunAsync(
            "xcodebuild",
            [
                "-exportArchive",
                "-archivePath", _paths.ArchivePath,
                "-exportPath", _paths.ExportPath,
                "-exportOptionsPlist", _paths.ExportOptionsPlistPath
            ],
            _paths.XcodeOutputDirectory,
            _paths.XcodeExportLogPath,
            _config.Environment);
    }

    private void PrintSummary()
    {
        _logger.Info($"RunId: {_paths.RunId}");
        _logger.Info($"仓库: {_config.RepositoryUrl} [{_config.Branch}]");
        _logger.Info($"工作区: {_paths.WorkspaceRoot}");
        _logger.Info($"Unity 工程: {_paths.UnityProjectRoot}");
        _logger.Info($"Xcode 输出: {_paths.XcodeOutputDirectory}");
        _logger.Info($"归档: {_paths.ArchivePath}");
        _logger.Info($"导出目录: {_paths.ExportPath}");
        _logger.Info($"日志目录: {_paths.LogsDirectory}");
    }

    private StepTimer StartStep(string name)
    {
        _logger.StepStarted(name);
        return new StepTimer(_logger, name);
    }

    private void RunStep(string name, Action action)
    {
        using StepTimer step = StartStep(name);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            step.Fail(ex);
            throw;
        }
    }

    private async Task RunStepAsync(string name, Func<Task> action)
    {
        using StepTimer step = StartStep(name);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            step.Fail(ex);
            throw;
        }
    }

    public void Dispose()
    {
        _logger.Dispose();
    }

    private static void AddUnityPair(List<string> args, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        args.Add(key);
        args.Add(value);
    }

    private static void AddXcodeSetting(List<string> args, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        args.Add($"{key}={value}");
    }

    private static string ToXcodeSigningStyle(string signingStyle)
    {
        return signingStyle.Equals("manual", StringComparison.OrdinalIgnoreCase)
            ? "Manual"
            : "Automatic";
    }
}
