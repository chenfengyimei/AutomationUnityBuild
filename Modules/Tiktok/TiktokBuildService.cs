namespace AutomationUnityBuildIOS;

internal sealed class TiktokBuildService(BuildRunContext context)
{
    private readonly UnityLogDiagnostics _logDiagnostics = new(context.Logger);
    private readonly UnityBuildMetadataReader _metadataReader = new(context, "BuildTiktok.cs");
    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildPaths _paths => context.Paths;
    private ProcessRunner _processRunner => context.ProcessRunner;
    private BuildLogger _logger => context.Logger;

    public async Task BuildAsync()
    {
        _logger.Info($"Unity 编辑器日志: {_paths.UnityLogPath}");
        _logger.Info($"Unity 进程输出日志: {_paths.UnityProcessLogPath}");
        _logger.Info($"TikTok WebGL 输出目录: {_paths.TiktokWebglOutputDirectory}");

        List<string> args = UnityCommandBuilder.CreateBatchModeArgs(_config, _paths, "WebGL");
        UnityCommandBuilder.AddPair(args, "-customWebglOutputPath", _paths.TiktokWebglOutputDirectory);
        UnityCommandBuilder.AddBundleVersionArgs(args, _config, _logger);
        UnityCommandBuilder.AddCommonPlayerArgs(args, _config);
        UnityCommandBuilder.AddMetadataPath(args, _paths);

        try
        {
            await _processRunner.RunAsync(
                _paths.UnityExecutable,
                args,
                _paths.UnityProjectRoot,
                _paths.UnityProcessLogPath,
                _config.Environment);

            if (_options.DryRun)
            {
                return;
            }

            ValidateWebglArtifacts();
            _metadataReader.SyncBundleVersionFromUnityMetadata();
        }
        catch (Exception ex)
        {
            LogUnityFailureDetails();
            if (_logDiagnostics.TryGetKnownFailureMessage(_paths) is { } knownFailureMessage)
            {
                throw new InvalidOperationException(knownFailureMessage, ex);
            }

            throw;
        }
    }

    private void ValidateWebglArtifacts()
    {
        if (!Directory.Exists(_paths.TiktokWebglOutputDirectory))
        {
            _logDiagnostics.LogDirectorySnapshot(_paths.ArtifactsRunRoot, "产物根目录");
            throw new DirectoryNotFoundException(
                $"Unity WebGL 构建完成，但没有找到输出目录: {_paths.TiktokWebglOutputDirectory}");
        }

        string[] expectedFiles = Directory.GetFiles(
            _paths.TiktokWebglOutputDirectory, "*", SearchOption.AllDirectories);
        if (expectedFiles.Length == 0)
        {
            throw new FileNotFoundException(
                $"Unity WebGL 输出目录存在但为空: {_paths.TiktokWebglOutputDirectory}");
        }

        _logger.Info($"Unity WebGL 产物验证通过，共 {expectedFiles.Length} 个文件。");
    }

    private void LogUnityFailureDetails()
    {
        _logDiagnostics.LogFailureDetails(
            _paths,
            "Unity WebGL 进程失败。下面是 Unity 日志里的关键错误线索。",
            ["error", "exception", "executeMethod", "BuildAutomation", "TiktokBuilder", "WebGL", "license"]);
    }
}