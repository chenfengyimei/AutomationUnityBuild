namespace AutomationUnityBuildIOS;

internal sealed class TiktokBuildPipeline : IPlatformBuildPipeline
{
    private readonly BuildRunContext _context;
    private readonly WorkflowStepRunner _stepRunner;
    private readonly UnityProjectValidator _unityProjectValidator;
    private readonly TiktokBuildService _tiktokBuildService;
    private readonly TiktokUploadService _tiktokUploadService;

    private BuildConfig _config => _context.Config;
    private CliOptions _options => _context.Options;
    private BuildPaths _paths => _context.Paths;
    private BuildLogger _logger => _context.Logger;

    public TiktokBuildPipeline(BuildRunContext context, WorkflowStepRunner stepRunner)
    {
        _context = context;
        _stepRunner = stepRunner;
        _unityProjectValidator = new UnityProjectValidator(context);
        _tiktokBuildService = new TiktokBuildService(context);
        _tiktokUploadService = new TiktokUploadService(context);
    }

    public string ResultPathLabel => "TikTok WebGL 输出目录";
    public string ResultPath => _paths.TiktokWebglOutputDirectory;

    public void PrintSummary()
    {
        _logger.Info($"TikTok WebGL 输出目录: {_paths.TiktokWebglOutputDirectory}");
        _logger.Info($"TikTok App ID: {_config.TiktokAppId}");
        _logger.Info($"TikTok 上传: {(_config.TiktokUploadEnabled ? "启用" : "关闭")}");
    }

    public async Task RunAsync()
    {
        if (_options.SkipXcode)
        {
            _logger.Info("TikTok 打包不需要 Xcode，已忽略 --skip-xcode。");
        }

        if (!_options.SkipUnity)
        {
            if (!_options.DryRun)
            {
                _stepRunner.Run("校验 Unity 工程目录", _unityProjectValidator.Validate);
            }

            await _stepRunner.RunAsync("Unity 构建 TikTok WebGL", _tiktokBuildService.BuildAsync);
        }
        else
        {
            _logger.Warn("跳过 Unity TikTok 构建。");
        }

        if (_config.TiktokUploadEnabled)
        {
            await _stepRunner.RunAsync("TikTok 小游戏上传", _tiktokUploadService.UploadAsync);
        }
        else
        {
            _logger.Info("TikTok 上传: 关闭");
        }
    }
}