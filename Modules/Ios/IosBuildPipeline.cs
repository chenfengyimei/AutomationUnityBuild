namespace AutomationUnityBuildIOS;

internal sealed class IosBuildPipeline : IPlatformBuildPipeline
{
    private readonly BuildRunContext _context;
    private readonly WorkflowStepRunner _stepRunner;
    private readonly UnityProjectValidator _unityProjectValidator;
    private readonly IosUnityBuildService _iosUnityBuildService;
    private readonly XcodeBuildService _xcodeBuildService;
    private readonly AppStoreConnectUploader _appStoreConnectUploader;

    private BuildConfig _config => _context.Config;
    private CliOptions _options => _context.Options;
    private BuildPaths _paths => _context.Paths;
    private BuildLogger _logger => _context.Logger;

    public IosBuildPipeline(BuildRunContext context, WorkflowStepRunner stepRunner)
    {
        _context = context;
        _stepRunner = stepRunner;
        _unityProjectValidator = new UnityProjectValidator(context);
        var xcodeProjectLocator = new XcodeProjectLocator(context);
        _iosUnityBuildService = new IosUnityBuildService(context, xcodeProjectLocator);
        _xcodeBuildService = new XcodeBuildService(context, xcodeProjectLocator);
        _appStoreConnectUploader = new AppStoreConnectUploader(context);
    }

    public string ResultPathLabel => "导出目录";
    public string ResultPath => _paths.ExportPath;

    public void PrintSummary()
    {
        _logger.Info($"Xcode 输出: {_paths.XcodeOutputDirectory}");
        _logger.Info($"归档: {_paths.ArchivePath}");
        _logger.Info($"导出目录: {_paths.ExportPath}");
        _logger.Info($"复制 archive 到 Organizer: {(_config.CopyArchiveToOrganizer ? "启用" : "关闭")}");
        _logger.Info($"App Store Connect 自动上传: {(_config.AppStoreConnectUploadEnabled ? "启用" : "关闭")}");
    }

    public async Task RunAsync()
    {
        if (!_options.SkipUnity)
        {
            if (!_options.DryRun)
            {
                _stepRunner.Run("校验 Unity 工程目录", _unityProjectValidator.Validate);
            }

            await _stepRunner.RunAsync("Unity 导出 iOS Xcode 工程", _iosUnityBuildService.ExportIosAsync);
        }
        else
        {
            _logger.Warn("跳过 Unity 导出。");
        }

        if (!_options.SkipXcode)
        {
            await _stepRunner.RunAsync("Xcode archive/export", _xcodeBuildService.ArchiveAndExportAsync);
            if (_config.AppStoreConnectUploadEnabled)
            {
                await _stepRunner.RunAsync("App Store Connect 上传", _appStoreConnectUploader.UploadAsync);
            }
        }
        else
        {
            _logger.Warn("跳过 Xcode 编译导出。");
        }
    }
}
