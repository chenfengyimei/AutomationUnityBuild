namespace AutomationUnityBuildIOS;

internal sealed class IosUnityBuildService(BuildRunContext context, XcodeProjectLocator xcodeProjectLocator)
{
    private readonly BuildRunContext _context = context;
    private readonly UnityLogDiagnostics _logDiagnostics = new(context.Logger);
    private readonly UnityBuildMetadataReader _metadataReader = new(context, "BuildIOS.cs");
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

        List<string> args = UnityCommandBuilder.CreateBatchModeArgs(_config, _paths, "iOS");
        UnityCommandBuilder.AddPair(args, "-customBuildPath", _paths.XcodeOutputDirectory);
        UnityCommandBuilder.AddBundleVersionArgs(args, _config, _logger);
        UnityCommandBuilder.AddCommonPlayerArgs(args, _config);
        UnityCommandBuilder.AddPair(args, "-customAppleTeamId", _config.TeamId);
        UnityCommandBuilder.AddPair(args, "-customIosDeploymentTarget", _config.IosDeploymentTarget);
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

            ValidateXcodeProjectExported();
            ValidateCocoaPodsInstallation();
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

    private void LogUnityFailureDetails()
    {
        _logDiagnostics.LogFailureDetails(
            _paths,
            "Unity 进程失败。下面是 Unity 日志里的关键错误线索。",
            ["error", "exception", "executeMethod", "BuildAutomation", "IOSBuilder", "compilation", "license"]);
    }

    private void ValidateXcodeProjectExported()
    {
        if (_xcodeProjectLocator.Find() is not null)
        {
            _logger.Info($"Unity Xcode 工程导出校验通过: {_paths.XcodeOutputDirectory}");
            return;
        }

        _logger.Error($"Unity 进程返回成功，但没有在目标目录找到 .xcodeproj/.xcworkspace: {_paths.XcodeOutputDirectory}");
        _logDiagnostics.LogDirectorySnapshot(_paths.XcodeOutputDirectory, "Unity 指定的 Xcode 输出目录");
        _logDiagnostics.LogDirectorySnapshot(_paths.ArtifactsRunRoot, "本次产物目录");
        _logDiagnostics.LogMatchingLogLines(_paths.UnityLogPath, "Unity Editor", ["Unity iOS", "BuildPipeline", "BuildReport", "BuildPlayer", "locationPathName", "customBuildPath", "error", "exception"]);

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
}
