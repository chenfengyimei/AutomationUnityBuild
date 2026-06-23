namespace AutomationUnityBuildIOS;

internal sealed class AndroidBuildPipeline : IPlatformBuildPipeline
{
    private readonly BuildRunContext _context;
    private readonly WorkflowStepRunner _stepRunner;
    private readonly UnityProjectValidator _unityProjectValidator;
    private readonly AndroidBuildService _androidBuildService;
    private readonly GooglePlayPublisher _googlePlayPublisher;

    private BuildConfig _config => _context.Config;
    private CliOptions _options => _context.Options;
    private BuildPaths _paths => _context.Paths;
    private BuildLogger _logger => _context.Logger;

    public AndroidBuildPipeline(BuildRunContext context, WorkflowStepRunner stepRunner)
    {
        _context = context;
        _stepRunner = stepRunner;
        _unityProjectValidator = new UnityProjectValidator(context);
        _androidBuildService = new AndroidBuildService(context);
        _googlePlayPublisher = new GooglePlayPublisher(context);
    }

    public string ResultPathLabel => "Android 输出目录";
    public string ResultPath => _paths.AndroidOutputDirectory;

    public void PrintSummary()
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

    public async Task RunAsync()
    {
        if (_options.SkipXcode)
        {
            _logger.Info("Android 打包不需要 Xcode，已忽略 --skip-xcode。");
        }

        if (!_options.SkipUnity)
        {
            if (!_options.DryRun)
            {
                _stepRunner.Run("校验 Unity 工程目录", _unityProjectValidator.Validate);
            }

            await _stepRunner.RunAsync("Unity 构建 Android APK/AAB", _androidBuildService.BuildAsync);
        }
        else
        {
            _logger.Warn("跳过 Unity Android 构建。");
        }

        if (_config.GooglePlayUploadEnabled)
        {
            await _stepRunner.RunAsync("Google Play 上传", _googlePlayPublisher.PublishAsync);
        }
        else
        {
            _logger.Info("Google Play 上传: 关闭");
        }
    }
}
