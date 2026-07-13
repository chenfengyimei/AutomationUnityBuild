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
            if (string.IsNullOrWhiteSpace(_paths.UnityExecutable))
            {
                if (!_options.DryRun)
                {
                    throw new FileNotFoundException(
                        "找不到 Unity 可执行文件。请在配置中设置 unityExecutablePath 或 unityVersion。" + Environment.NewLine +
                        "macOS 默认路径示例: /Applications/Unity/Hub/Editor/2022.3.62f2c1/Unity.app/Contents/MacOS/Unity" + Environment.NewLine +
                        "Windows 默认路径示例: C:\\Program Files\\Unity\\Hub\\Editor\\2022.3.62f2c1\\Unity.exe");
                }

                _logger.Warn("[dry-run] 未配置 Unity 可执行文件，正式打包时会失败。请设置 unityExecutablePath 或 unityVersion。");
            }
            else if (!_options.DryRun && !File.Exists(_paths.UnityExecutable))
            {
                throw new FileNotFoundException($"找不到 Unity 可执行文件: {_paths.UnityExecutable}");
            }

            if (!string.IsNullOrWhiteSpace(_paths.UnityExecutable))
            {
                _logger.Info($"Unity: {_paths.UnityExecutable}");
            }
        }

        if (_config.IsIos && !_options.SkipXcode)
        {
            if (!OperatingSystem.IsMacOS() && _options.AllowNonMac)
            {
                _logger.Warn("--allow-non-mac：非 macOS 环境，跳过 xcodebuild 版本检查。Xcode 归档/导出步骤将不可用。");
            }
            else
            {
                await _processRunner.RunAsync("xcodebuild", ["-version"]);
            }

            if (_config.AppStoreConnectUploadEnabled)
            {
                string apiKeyPath = Path.GetFullPath(PathTools.ExpandHome(_config.AppStoreConnectApiKeyPath));
                if (!_options.DryRun && !File.Exists(apiKeyPath))
                {
                    throw new FileNotFoundException($"App Store Connect API Key .p8 文件不存在: {apiKeyPath}");
                }

                _logger.Info($"App Store Connect API Key: {apiKeyPath}");
            }
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
