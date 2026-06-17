using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationUnityBuildIOS;

internal sealed class AutomationWorkflow : IDisposable
{
    private readonly BuildRunContext _context;
    private readonly EnvironmentDoctor _environmentDoctor;
    private readonly PathSafetyValidator _pathSafetyValidator;
    private readonly GitRepositoryPolicyValidator _gitRepositoryPolicyValidator;
    private readonly BuildDirectoryPreparer _directoryPreparer;
    private readonly GitRepositoryService _gitRepository;
    private readonly UnityProjectValidator _unityProjectValidator;
    private readonly XcodeProjectLocator _xcodeProjectLocator;
    private readonly UnityBuildService _unityBuildService;
    private readonly XcodeBuildService _xcodeBuildService;
    private readonly AndroidBuildService _androidBuildService;
    private readonly GooglePlayPublisher _googlePlayPublisher;
    private readonly RuntimeConfigUpdater _runtimeConfigUpdater;
    private readonly BuildConfigSnapshotWriter _configSnapshotWriter;
    private BuildConfig _config => _context.Config;
    private CliOptions _options => _context.Options;
    private BuildPaths _paths => _context.Paths;
    private BuildLogger _logger => _context.Logger;

    public AutomationWorkflow(BuildConfig config, CliOptions options)
    {
        _context = BuildRunContext.Create(config, options);
        _environmentDoctor = new EnvironmentDoctor(_context);
        _pathSafetyValidator = new PathSafetyValidator(_context);
        _gitRepositoryPolicyValidator = new GitRepositoryPolicyValidator(_context);
        _directoryPreparer = new BuildDirectoryPreparer(_context);
        _gitRepository = new GitRepositoryService(_context);
        _unityProjectValidator = new UnityProjectValidator(_context);
        _xcodeProjectLocator = new XcodeProjectLocator(_context);
        _unityBuildService = new UnityBuildService(_context, _xcodeProjectLocator);
        _xcodeBuildService = new XcodeBuildService(_context, _xcodeProjectLocator);
        _androidBuildService = new AndroidBuildService(_context);
        _googlePlayPublisher = new GooglePlayPublisher(_context);
        _runtimeConfigUpdater = new RuntimeConfigUpdater(_context);
        _configSnapshotWriter = new BuildConfigSnapshotWriter(_context);
    }

    public async Task RunAsync()
    {
        var workflowStopwatch = Stopwatch.StartNew();
        try
        {
            _logger.StepStarted("自动化打包流程");
            _runtimeConfigUpdater.PrepareBuildNumberForRun();
            PrintSummary();
            _environmentDoctor.EnsureMacOrAllowed();
            await CheckPrerequisitesAsync();
            RunStep("生成配置快照", _configSnapshotWriter.Write);

            if (_options.DryRun)
            {
                _logger.Info("[dry-run] 跳过目录创建、清理和文件生成。");
            }
            else
            {
                RunStep("准备目录", _directoryPreparer.Prepare);
            }

            if (!_options.SkipGit)
            {
                await RunStepAsync("同步 Unity 仓库", _gitRepository.SyncAsync);
            }
            else
            {
                _logger.Warn("跳过 Git 同步。");
            }

            if (_config.IsAndroid)
            {
                await RunAndroidBuildAsync();
            }
            else
            {
                await RunIosBuildAsync();
            }

            _runtimeConfigUpdater.SaveChangesIfNeeded();
            _logger.StepCompleted("自动化打包流程", workflowStopwatch.Elapsed);
            Console.ForegroundColor = ConsoleColor.Green;
            _logger.Info("自动化打包流程完成。");
            Console.ResetColor();
            _logger.Info($"产物目录: {_paths.ArtifactsRunRoot}");
            _logger.Info(_config.IsAndroid ? $"Android 输出目录: {_paths.AndroidOutputDirectory}" : $"导出目录: {_paths.ExportPath}");
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
        RunStep("校验配置安全边界", _pathSafetyValidator.Validate);
        RunStep("校验 Git 仓库策略", _gitRepositoryPolicyValidator.Validate);
        await RunStepAsync("检查环境", _environmentDoctor.CheckAsync);
    }

    private void PrintSummary()
    {
        _logger.Info($"RunId: {_paths.RunId}");
        _logger.Info($"平台: {_config.BuildPlatform}");
        _logger.Info($"仓库: {_config.RepositoryUrl} [{_config.Branch}]");
        _logger.Info($"工作区: {_paths.WorkspaceRoot}");
        _logger.Info($"Git 仓库目录: {_paths.RepositoryRoot}");
        _logger.Info($"Unity 工程: {_paths.UnityProjectRoot}");
        _logger.Info(_config.SyncBundleVersionFromUnity
            ? $"Bundle Version: 同步 Unity 项目设置（配置记录值: {BuildDisplay.BundleVersion(_config.BundleVersion)}）"
            : $"Bundle Version: 使用配置固定值 {_config.BundleVersion}");
        _logger.Info($"Build Number: {BuildDisplay.BuildNumber(_config.BuildNumber)}，自动+1: {(_config.AutoIncrementBuildNumber ? "启用" : "关闭")}");
        if (_config.IsAndroid)
        {
            _logger.Info($"Android Build Format: {_config.AndroidBuildFormat}");
            _logger.Info($"Android 输出目录: {_paths.AndroidOutputDirectory}");
            if (_config.ShouldBuildApk)
            {
                _logger.Info($"APK: {_paths.ApkOutputPath}");
            }

            if (_config.ShouldBuildAab)
            {
                _logger.Info($"AAB: {_paths.AabOutputPath}");
            }

            _logger.Info($"Google Play 上传: {(_config.GooglePlayUploadEnabled ? $"启用 track={_config.GooglePlayTrack}, artifact={_config.GooglePlayUploadArtifact}" : "关闭")}");
        }
        else
        {
            _logger.Info($"Xcode 输出: {_paths.XcodeOutputDirectory}");
            _logger.Info($"归档: {_paths.ArchivePath}");
            _logger.Info($"导出目录: {_paths.ExportPath}");
            _logger.Info($"复制 archive 到 Organizer: {(_config.CopyArchiveToOrganizer ? "启用" : "关闭")}");
        }

        _logger.Info($"日志目录: {_paths.LogsDirectory}");
    }

    private async Task RunIosBuildAsync()
    {
        if (!_options.SkipUnity)
        {
            if (!_options.DryRun)
            {
                RunStep("校验 Unity 工程目录", _unityProjectValidator.Validate);
            }

            await RunStepAsync("Unity 导出 iOS Xcode 工程", _unityBuildService.ExportIosAsync);
        }
        else
        {
            _logger.Warn("跳过 Unity 导出。");
        }

        if (!_options.SkipXcode)
        {
            await RunStepAsync("Xcode archive/export", _xcodeBuildService.ArchiveAndExportAsync);
        }
        else
        {
            _logger.Warn("跳过 Xcode 编译导出。");
        }
    }

    private async Task RunAndroidBuildAsync()
    {
        if (_options.SkipXcode)
        {
            _logger.Info("Android 打包不需要 Xcode，已忽略 --skip-xcode。");
        }

        if (!_options.SkipUnity)
        {
            if (!_options.DryRun)
            {
                RunStep("校验 Unity 工程目录", _unityProjectValidator.Validate);
            }

            await RunStepAsync("Unity 构建 Android APK/AAB", _androidBuildService.BuildAsync);
        }
        else
        {
            _logger.Warn("跳过 Unity Android 构建。");
        }

        await RunStepAsync("Google Play 上传", _googlePlayPublisher.PublishAsync);
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
        _context.Dispose();
    }
}

