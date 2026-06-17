using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationUnityBuildIOS;

internal sealed class AndroidBuildService(BuildRunContext context)
{
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

        var args = new List<string>
        {
            "-batchmode",
            "-quit",
            "-nographics",
            "-accept-apiupdate",
            "-projectPath",
            _paths.UnityProjectRoot,
            "-buildTarget",
            "Android",
            "-executeMethod",
            _config.UnityBuildMethod,
            "-logFile",
            _paths.UnityLogPath,
            "-customAndroidBuildFormat",
            _config.AndroidBuildFormat,
            "-customApkPath",
            _paths.ApkOutputPath,
            "-customAabPath",
            _paths.AabOutputPath
        };

        AddUnityPair(args, "-customBuildNumber", _config.BuildNumber);
        if (_config.SyncBundleVersionFromUnity)
        {
            _logger.Info("Bundle Version 同步 Unity 项目设置，本次不会用配置文件强制覆盖。");
        }
        else
        {
            AddUnityPair(args, "-customBundleVersion", _config.BundleVersion);
            _logger.Info($"Bundle Version 使用配置文件固定值: {_config.BundleVersion}");
        }

        AddUnityPair(args, "-customBundleIdentifier", _config.BundleIdentifier);
        AddUnityPair(args, "-customProductName", _config.ProductName);
        AddUnityPair(args, "-customAndroidMinSdkVersion", _config.AndroidMinSdkVersion);
        AddUnityPair(args, "-customAndroidTargetSdkVersion", _config.AndroidTargetSdkVersion);
        AddUnityPair(args, "-customAndroidKeystoreName", ResolveOptionalPath(_config.AndroidKeystoreName));
        AddUnityPair(args, "-customAndroidKeystorePass", _config.AndroidKeystorePass);
        AddUnityPair(args, "-customAndroidKeyaliasName", _config.AndroidKeyaliasName);
        AddUnityPair(args, "-customAndroidKeyaliasPass", _config.AndroidKeyaliasPass);
        AddUnityPair(args, "-customBuildMetadataPath", _paths.UnityBuildMetadataPath);

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
            SyncBundleVersionFromUnityMetadata();
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
            LogDirectorySnapshot(_paths.AndroidOutputDirectory, "Android 输出目录");
            throw new FileNotFoundException($"Unity Android 构建完成，但没有找到 APK: {_paths.ApkOutputPath}");
        }

        if (_config.ShouldBuildAab && !File.Exists(_paths.AabOutputPath))
        {
            LogDirectorySnapshot(_paths.AndroidOutputDirectory, "Android 输出目录");
            throw new FileNotFoundException($"Unity Android 构建完成，但没有找到 AAB: {_paths.AabOutputPath}");
        }

        _logger.Info("Unity Android 产物校验通过。");
    }

    private void SyncBundleVersionFromUnityMetadata()
    {
        if (!_config.SyncBundleVersionFromUnity || _options.SkipUnity)
        {
            return;
        }

        if (!File.Exists(_paths.UnityBuildMetadataPath))
        {
            _logger.Warn($"已开启 Bundle Version 同步，但没有找到 Unity 构建元数据: {_paths.UnityBuildMetadataPath}");
            _logger.Warn("请确认 Unity 项目里的 Assets/Editor/BuildAndroid.cs 已更新到当前工具版本。");
            return;
        }

        AndroidBuildMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<AndroidBuildMetadata>(
                File.ReadAllText(_paths.UnityBuildMetadataPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.Warn($"读取 Unity 构建元数据失败，跳过 Bundle Version 同步: {ex.Message}");
            return;
        }

        string unityBundleVersion = metadata?.BundleVersion?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(unityBundleVersion))
        {
            _logger.Warn("Unity 构建元数据里没有 bundleVersion，跳过 Bundle Version 同步。");
            return;
        }

        if (string.Equals(_config.BundleVersion, unityBundleVersion, StringComparison.Ordinal))
        {
            _logger.Info($"Bundle Version 已与 Unity 项目一致: {unityBundleVersion}");
            return;
        }

        _logger.Info($"同步 Unity 项目 Bundle Version: {BuildDisplay.BundleVersion(_config.BundleVersion)} -> {unityBundleVersion}");
        _config.BundleVersion = unityBundleVersion;
        context.MarkRuntimeConfigChanged();
    }

    private void LogUnityFailureDetails()
    {
        _logger.Error("Unity Android 进程失败。下面是 Unity 日志里的关键错误线索。");
        LogMatchingLogLines(_paths.UnityLogPath, "Unity Editor", ["error", "exception", "executeMethod", "BuildAutomation", "AndroidBuilder", "Gradle", "SDK", "JDK", "license"]);
        LogTail(_paths.UnityLogPath, "Unity Editor", 80);
        LogTail(_paths.UnityProcessLogPath, "Unity Process", 80);
    }

    private void LogDirectorySnapshot(string directory, string title)
    {
        if (!Directory.Exists(directory))
        {
            _logger.Error($"{title}不存在: {directory}");
            return;
        }

        _logger.Error($"----- {title}: {directory} -----");
        foreach (string entry in Directory.EnumerateFileSystemEntries(directory).Take(80))
        {
            _logger.Error(entry);
        }
    }

    private void LogMatchingLogLines(string logPath, string title, IReadOnlyList<string> keywords)
    {
        if (!File.Exists(logPath))
        {
            _logger.Warn($"{title} 日志不存在: {logPath}");
            return;
        }

        string[] matches = File.ReadLines(logPath)
            .Where(line => keywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .TakeLast(60)
            .ToArray();

        if (matches.Length == 0)
        {
            _logger.Warn($"{title} 日志里没有匹配到常见错误关键字。");
            return;
        }

        _logger.Error($"----- {title} 关键错误行 -----");
        foreach (string line in matches)
        {
            _logger.Error(line);
        }
    }

    private void LogTail(string logPath, string title, int lineCount)
    {
        if (!File.Exists(logPath))
        {
            _logger.Warn($"{title} 日志不存在: {logPath}");
            return;
        }

        _logger.Error($"----- {title} 日志最后 {lineCount} 行 -----");
        foreach (string line in File.ReadLines(logPath).TakeLast(lineCount))
        {
            _logger.Error(line);
        }
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

    private static string ResolveOptionalPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(PathTools.ExpandHome(path));
    }

    private sealed class AndroidBuildMetadata
    {
        [JsonPropertyName("bundleVersion")]
        public string? BundleVersion { get; set; }

        [JsonPropertyName("buildNumber")]
        public string? BuildNumber { get; set; }

        [JsonPropertyName("bundleIdentifier")]
        public string? BundleIdentifier { get; set; }

        [JsonPropertyName("productName")]
        public string? ProductName { get; set; }

        [JsonPropertyName("apkPath")]
        public string? ApkPath { get; set; }

        [JsonPropertyName("aabPath")]
        public string? AabPath { get; set; }
    }
}
