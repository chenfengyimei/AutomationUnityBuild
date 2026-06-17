using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationUnityBuildIOS;

internal sealed class IosUnityBuildService(BuildRunContext context, XcodeProjectLocator xcodeProjectLocator)
{
    private readonly BuildRunContext _context = context;
    private BuildConfig _config => _context.Config;
    private CliOptions _options => _context.Options;
    private BuildPaths _paths => _context.Paths;
    private ProcessRunner _processRunner => _context.ProcessRunner;
    private BuildLogger _logger => _context.Logger;
    private XcodeProjectLocator _xcodeProjectLocator => xcodeProjectLocator;

    public async Task ExportIosAsync()
    {
        _logger.Info($"Unity 编辑器日志: {_paths.UnityLogPath}");
        _logger.Info($"Unity 进程输出日志: {_paths.UnityProcessLogPath}");

        var args = new List<string>
        {
            "-batchmode",
            "-quit",
            "-nographics",
            "-accept-apiupdate",
            "-projectPath",
            _paths.UnityProjectRoot,
            "-buildTarget",
            "iOS",
            "-executeMethod",
            _config.UnityBuildMethod,
            "-logFile",
            _paths.UnityLogPath,
            "-customBuildPath",
            _paths.XcodeOutputDirectory
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
        AddUnityPair(args, "-customAppleTeamId", _config.TeamId);
        AddUnityPair(args, "-customIosDeploymentTarget", _config.IosDeploymentTarget);
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

            ValidateXcodeProjectExported();
            ValidateCocoaPodsInstallation();
            SyncBundleVersionFromUnityMetadata();
        }
        catch
        {
            LogUnityFailureDetails();
            throw;
        }
    }

    private void LogUnityFailureDetails()
    {
        _logger.Error("Unity 进程失败。下面是 Unity 日志里的关键错误线索。");
        LogMatchingLogLines(_paths.UnityLogPath, "Unity Editor", ["error", "exception", "executeMethod", "BuildAutomation", "IOSBuilder", "compilation", "license"]);
        LogTail(_paths.UnityLogPath, "Unity Editor", 80);
        LogTail(_paths.UnityProcessLogPath, "Unity Process", 80);
    }

    private void ValidateXcodeProjectExported()
    {
        if (_xcodeProjectLocator.Find() is not null)
        {
            _logger.Info($"Unity Xcode 工程导出校验通过: {_paths.XcodeOutputDirectory}");
            return;
        }

        _logger.Error($"Unity 进程返回成功，但没有在目标目录找到 .xcodeproj/.xcworkspace: {_paths.XcodeOutputDirectory}");
        LogDirectorySnapshot(_paths.XcodeOutputDirectory, "Unity 指定的 Xcode 输出目录");
        LogDirectorySnapshot(_paths.ArtifactsRunRoot, "本次产物目录");
        LogMatchingLogLines(_paths.UnityLogPath, "Unity Editor", ["Unity iOS", "BuildPipeline", "BuildReport", "BuildPlayer", "locationPathName", "customBuildPath", "error", "exception"]);

        throw new FileNotFoundException(
            $"Unity 没有导出 Xcode 工程到指定目录: {_paths.XcodeOutputDirectory}{Environment.NewLine}" +
            "请确认 Unity 项目中的 Assets/Editor/BuildIOS.cs 使用了 -customBuildPath 参数，并且 BuildPipeline.BuildPlayer 的 locationPathName 指向该路径。");
    }

    private void ValidateCocoaPodsInstallation()
    {
        if (!File.Exists(_paths.UnityLogPath))
        {
            return;
        }

        string[] failureMarkers =
        [
            "CocoaPods installation failure",
            "pod install output:",
            "CocoaPods could not find compatible versions",
            "required a higher minimum deployment target"
        ];

        string[] matches = File.ReadLines(_paths.UnityLogPath)
            .Where(line => failureMarkers.Any(marker => line.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .TakeLast(80)
            .ToArray();

        if (matches.Length == 0)
        {
            return;
        }

        _logger.Error("Unity 导出的 Xcode 工程存在，但 CocoaPods 依赖安装失败，Xcode 编译会缺少 iOS SDK/framework。");
        _logger.Error("----- Unity CocoaPods 关键错误 -----");
        foreach (string line in matches)
        {
            _logger.Error(line);
        }

        throw new InvalidOperationException(
            "CocoaPods 依赖安装失败。请在 unity-editor.log 中搜索 \"pod install output\"，先修复 Podfile/Deployment Target/CocoaPods repo 后再打包。");
    }

    private void SyncBundleVersionFromUnityMetadata()
    {
        if (!_config.SyncBundleVersionFromUnity)
        {
            return;
        }

        if (_options.SkipUnity)
        {
            return;
        }

        if (!File.Exists(_paths.UnityBuildMetadataPath))
        {
            _logger.Warn($"已开启 Bundle Version 同步，但没有找到 Unity 构建元数据: {_paths.UnityBuildMetadataPath}");
            _logger.Warn("请确认 Unity 项目里的 Assets/Editor/BuildIOS.cs 已更新到当前工具版本。");
            return;
        }

        UnityBuildMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<UnityBuildMetadata>(
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
        _context.MarkRuntimeConfigChanged();
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

    private sealed class UnityBuildMetadata
    {
        [JsonPropertyName("bundleVersion")]
        public string? BundleVersion { get; set; }

        [JsonPropertyName("buildNumber")]
        public string? BuildNumber { get; set; }

        [JsonPropertyName("bundleIdentifier")]
        public string? BundleIdentifier { get; set; }

        [JsonPropertyName("productName")]
        public string? ProductName { get; set; }

        [JsonPropertyName("iosDeploymentTarget")]
        public string? IosDeploymentTarget { get; set; }
    }
}
