namespace AutomationUnityBuildIOS;

internal sealed class EnvironmentDoctor(BuildRunContext context)
{
    private BuildConfig _config => context.Config;
    private CliOptions _options => context.Options;
    private BuildPaths _paths => context.Paths;
    private ProcessRunner _processRunner => context.ProcessRunner;
    private BuildLogger _logger => context.Logger;

    public async Task CheckAsync()
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

        if (_config.IsIos && !_options.SkipXcode)
        {
            await _processRunner.RunAsync("xcodebuild", ["-version"]);
        }

        if (_config.IsAndroid)
        {
            _logger.Info("Android 打包不需要 Xcode。");
            if (_config.GooglePlayUploadEnabled)
            {
                string serviceAccountPath = Path.GetFullPath(PathTools.ExpandHome(_config.GooglePlayServiceAccountJsonPath));
                if (!_options.DryRun && !File.Exists(serviceAccountPath))
                {
                    throw new FileNotFoundException($"Google Play Service Account JSON 不存在: {serviceAccountPath}");
                }

                _logger.Info($"Google Play Service Account JSON: {serviceAccountPath}");
            }
        }
    }

    public void EnsureMacOrAllowed()
    {
        if (_config.IsIos && !OperatingSystem.IsMacOS() && !_options.AllowNonMac)
        {
            throw new PlatformNotSupportedException("iOS 自动打包必须在 macOS 上执行。Windows 可用于开发/发布这个工具；调试配置可加 --allow-non-mac --dry-run。");
        }
    }
}
