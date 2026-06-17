using System.Diagnostics;

namespace AutomationUnityBuildIOS;

internal sealed class AutomationWorkflow : IDisposable
{
    private readonly BuildRunContext _context;
    private readonly WorkflowStepRunner _stepRunner;
    private readonly EnvironmentDoctor _environmentDoctor;
    private readonly PathSafetyValidator _pathSafetyValidator;
    private readonly GitRepositoryPolicyValidator _gitRepositoryPolicyValidator;
    private readonly BuildDirectoryPreparer _directoryPreparer;
    private readonly GitRepositoryService _gitRepository;
    private readonly IPlatformBuildPipeline _platformPipeline;
    private readonly RuntimeConfigUpdater _runtimeConfigUpdater;
    private readonly BuildConfigSnapshotWriter _configSnapshotWriter;

    private BuildConfig _config => _context.Config;
    private CliOptions _options => _context.Options;
    private BuildPaths _paths => _context.Paths;
    private BuildLogger _logger => _context.Logger;

    public AutomationWorkflow(BuildConfig config, CliOptions options)
    {
        _context = BuildRunContext.Create(config, options);
        _stepRunner = new WorkflowStepRunner(_context.Logger);
        _environmentDoctor = new EnvironmentDoctor(_context);
        _pathSafetyValidator = new PathSafetyValidator(_context);
        _gitRepositoryPolicyValidator = new GitRepositoryPolicyValidator(_context);
        _directoryPreparer = new BuildDirectoryPreparer(_context);
        _gitRepository = new GitRepositoryService(_context);
        _platformPipeline = PlatformBuildPipelineFactory.Create(_context, _stepRunner);
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
            _stepRunner.Run("生成配置快照", _configSnapshotWriter.Write);

            if (_options.DryRun)
            {
                _logger.Info("[dry-run] 跳过目录创建、清理和文件生成。");
            }
            else
            {
                _stepRunner.Run("准备目录", _directoryPreparer.Prepare);
            }

            if (!_options.SkipGit)
            {
                await _stepRunner.RunAsync("同步 Unity 仓库", _gitRepository.SyncAsync);
            }
            else
            {
                _logger.Warn("跳过 Git 同步。");
            }

            await _platformPipeline.RunAsync();

            _runtimeConfigUpdater.SaveChangesIfNeeded();
            _logger.StepCompleted("自动化打包流程", workflowStopwatch.Elapsed);
            Console.ForegroundColor = ConsoleColor.Green;
            _logger.Info("自动化打包流程完成。");
            Console.ResetColor();
            _logger.Info($"产物目录: {_paths.ArtifactsRunRoot}");
            _logger.Info($"{_platformPipeline.ResultPathLabel}: {_platformPipeline.ResultPath}");
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
        _stepRunner.Run("校验配置安全边界", _pathSafetyValidator.Validate);
        _stepRunner.Run("校验 Git 仓库策略", _gitRepositoryPolicyValidator.Validate);
        await _stepRunner.RunAsync("检查环境", _environmentDoctor.CheckAsync);
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
        _platformPipeline.PrintSummary();
        _logger.Info($"日志目录: {_paths.LogsDirectory}");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
