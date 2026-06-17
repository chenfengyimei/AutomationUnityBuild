namespace AutomationUnityBuildIOS;

internal sealed class AndroidBuildService(BuildRunContext context)
{
    private readonly UnityLogDiagnostics _logDiagnostics = new(context.Logger);
    private readonly UnityBuildMetadataReader _metadataReader = new(context, "BuildAndroid.cs");
    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildPaths _paths => context.Paths;
    private ProcessRunner _processRunner => context.ProcessRunner;
    private BuildLogger _logger => context.Logger;

    public async Task BuildAsync()
    {
        _logger.Info($"Unity 编辑器日志: {_paths.UnityLogPath}");
        _logger.Info($"Unity 进程输出日志: {_paths.UnityProcessLogPath}");
        _logger.Info($"Android 输出目录: {_paths.AndroidOutputDirectory}");
        if (_config.ShouldBuildApk)
        {
            _logger.Info($"APK 输出: {_paths.ApkOutputPath}");
        }

        if (_config.ShouldBuildAab)
        {
            _logger.Info($"AAB 输出: {_paths.AabOutputPath}");
        }

        List<string> args = UnityCommandBuilder.CreateBatchModeArgs(_config, _paths, "Android");
        UnityCommandBuilder.AddPair(args, "-customAndroidBuildFormat", _config.AndroidBuildFormat);
        UnityCommandBuilder.AddPair(args, "-customApkPath", _paths.ApkOutputPath);
        UnityCommandBuilder.AddPair(args, "-customAabPath", _paths.AabOutputPath);
        UnityCommandBuilder.AddBundleVersionArgs(args, _config, _logger);
        UnityCommandBuilder.AddCommonPlayerArgs(args, _config);
        UnityCommandBuilder.AddPair(args, "-customAndroidMinSdkVersion", _config.AndroidMinSdkVersion);
        UnityCommandBuilder.AddPair(args, "-customAndroidTargetSdkVersion", _config.AndroidTargetSdkVersion);
        UnityCommandBuilder.AddPair(args, "-customAndroidKeystoreName", ResolveOptionalPath(_config.AndroidKeystoreName));
        UnityCommandBuilder.AddPair(args, "-customAndroidKeystorePass", _config.AndroidKeystorePass);
        UnityCommandBuilder.AddPair(args, "-customAndroidKeyaliasName", _config.AndroidKeyaliasName);
        UnityCommandBuilder.AddPair(args, "-customAndroidKeyaliasPass", _config.AndroidKeyaliasPass);
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

            ValidateAndroidArtifacts();
            _metadataReader.SyncBundleVersionFromUnityMetadata();
        }
        catch
        {
            LogUnityFailureDetails();
            throw;
        }
    }

    private void ValidateAndroidArtifacts()
    {
        if (_config.ShouldBuildApk && !File.Exists(_paths.ApkOutputPath))
        {
            _logDiagnostics.LogDirectorySnapshot(_paths.AndroidOutputDirectory, "Android 输出目录");
            throw new FileNotFoundException($"Unity Android 构建完成，但没有找到 APK: {_paths.ApkOutputPath}");
        }

        if (_config.ShouldBuildAab && !File.Exists(_paths.AabOutputPath))
        {
            _logDiagnostics.LogDirectorySnapshot(_paths.AndroidOutputDirectory, "Android 输出目录");
            throw new FileNotFoundException($"Unity Android 构建完成，但没有找到 AAB: {_paths.AabOutputPath}");
        }

        _logger.Info("Unity Android 产物校验通过。");
    }

    private void LogUnityFailureDetails()
    {
        _logDiagnostics.LogFailureDetails(
            _paths,
            "Unity Android 进程失败。下面是 Unity 日志里的关键错误线索。",
            ["error", "exception", "executeMethod", "BuildAutomation", "AndroidBuilder", "Gradle", "SDK", "JDK", "license"]);
    }

    private static string ResolveOptionalPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(PathTools.ExpandHome(path));
    }
}
