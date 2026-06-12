using System.Diagnostics;

namespace AutomationUnityBuildIOS;

internal sealed class AutomationWorkflow : IDisposable
{
    private readonly BuildConfig _config;
    private readonly CliOptions _options;
    private readonly BuildPaths _paths;
    private readonly ProcessRunner _processRunner;
    private readonly BuildLogger _logger;

    public AutomationWorkflow(BuildConfig config, CliOptions options)
    {
        _config = config;
        _options = options;
        _paths = BuildPaths.Create(config);
        _logger = BuildLogger.Create(_paths.AutomationLogPath, options.Verbose, options.DryRun);
        _processRunner = new ProcessRunner(options.DryRun, options.Verbose, _logger);
    }

    public async Task RunAsync()
    {
        var workflowStopwatch = Stopwatch.StartNew();
        try
        {
            _logger.StepStarted("自动化打包流程");
            PrintSummary();
            EnsureMacOrAllowed();
            await CheckPrerequisitesAsync();

            if (_options.DryRun)
            {
                _logger.Info("[dry-run] 跳过目录创建、清理和文件生成。");
            }
            else
            {
                RunStep("准备目录", PrepareDirectories);
            }

            if (!_options.SkipGit)
            {
                await RunStepAsync("同步 Unity 仓库", SyncRepositoryAsync);
            }
            else
            {
                _logger.Warn("跳过 Git 同步。");
            }

            if (!_options.SkipUnity)
            {
                if (!_options.DryRun)
                {
                    RunStep("校验 Unity 工程目录", ValidateUnityProjectDirectory);
                }

                await RunStepAsync("Unity 导出 iOS Xcode 工程", RunUnityBuildAsync);
            }
            else
            {
                _logger.Warn("跳过 Unity 导出。");
            }

            if (!_options.SkipXcode)
            {
                await RunStepAsync("Xcode archive/export", RunXcodeArchiveAndExportAsync);
            }
            else
            {
                _logger.Warn("跳过 Xcode 编译导出。");
            }

            _logger.StepCompleted("自动化打包流程", workflowStopwatch.Elapsed);
            Console.ForegroundColor = ConsoleColor.Green;
            _logger.Info("自动化打包流程完成。");
            Console.ResetColor();
            _logger.Info($"产物目录: {_paths.ArtifactsRunRoot}");
            _logger.Info($"导出目录: {_paths.ExportPath}");
            _logger.Info($"总日志: {_paths.AutomationLogPath}");
        }
        catch (Exception ex)
        {
            _logger.StepFailed("自动化打包流程", workflowStopwatch.Elapsed, ex);
            _logger.Error("自动化打包流程失败", ex);
            throw;
        }
    }

    public async Task CheckPrerequisitesAsync()
    {
        await RunStepAsync("检查环境", CheckPrerequisitesCoreAsync);
    }

    private async Task CheckPrerequisitesCoreAsync()
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

        if (!_options.SkipXcode)
        {
            await _processRunner.RunAsync("xcodebuild", ["-version"]);
        }
    }

    private void EnsureMacOrAllowed()
    {
        if (!OperatingSystem.IsMacOS() && !_options.AllowNonMac)
        {
            throw new PlatformNotSupportedException("iOS 自动打包必须在 macOS 上执行。Windows 可用于开发/发布这个工具；调试配置可加 --allow-non-mac --dry-run。");
        }
    }

    private void PrepareDirectories()
    {
        _logger.Info("准备目录：不存在的目录会自动创建。");
        EnsureDirectoryExists(_paths.WorkspaceRoot, "工作区目录");
        EnsureDirectoryExists(_paths.ArtifactsRunRoot, "本次产物目录");
        EnsureDirectoryExists(_paths.LogsDirectory, "日志目录");
        EnsureParentDirectoryExists(_paths.ArchivePath, "Xcode archive 父目录");
        EnsureParentDirectoryExists(_paths.ExportOptionsPlistPath, "ExportOptions.plist 父目录");

        if (_config.CleanXcodeOutputBeforeBuild && Directory.Exists(_paths.XcodeOutputDirectory))
        {
            _logger.Warn($"清理旧 Xcode 输出目录: {_paths.XcodeOutputDirectory}");
            Directory.Delete(_paths.XcodeOutputDirectory, recursive: true);
        }

        EnsureDirectoryExists(_paths.XcodeOutputDirectory, "Xcode 输出目录");
        EnsureDirectoryExists(_paths.ExportPath, "导出目录");
    }

    private async Task SyncRepositoryAsync()
    {
        ValidateRepositoryUrlForGit();
        IReadOnlyDictionary<string, string> gitEnvironment = GitEnvironment();

        if (!Directory.Exists(Path.Combine(_paths.RepositoryRoot, ".git")))
        {
            _logger.Info($"仓库不存在，准备 clone 到: {_paths.RepositoryRoot}");
            Directory.CreateDirectory(_paths.WorkspaceRoot);
            await _processRunner.RunAsync(
                "git",
                ["clone", "--branch", _config.Branch, _config.RepositoryUrl, _paths.RepositoryRoot],
                _paths.WorkspaceRoot,
                environment: gitEnvironment);
            return;
        }

        _logger.Info($"仓库已存在，准备更新: {_paths.RepositoryRoot}");
        await _processRunner.RunAsync("git", ["fetch", "--prune", "origin"], _paths.RepositoryRoot, environment: gitEnvironment);
        await _processRunner.RunAsync("git", ["checkout", _config.Branch], _paths.RepositoryRoot, environment: gitEnvironment);

        if (_config.ResetRepository)
        {
            _logger.Warn($"resetRepository=true，将强制重置到 origin/{_config.Branch} 并清理未跟踪文件。");
            await _processRunner.RunAsync("git", ["reset", "--hard", $"origin/{_config.Branch}"], _paths.RepositoryRoot, environment: gitEnvironment);
            await _processRunner.RunAsync("git", ["clean", "-fdx"], _paths.RepositoryRoot, environment: gitEnvironment);
        }
        else
        {
            await _processRunner.RunAsync("git", ["pull", "--ff-only", "origin", _config.Branch], _paths.RepositoryRoot, environment: gitEnvironment);
        }
    }

    private void ValidateRepositoryUrlForGit()
    {
        if (_config.RepositoryUrl.Any(char.IsWhiteSpace) ||
            _config.RepositoryUrl.Contains('[') ||
            _config.RepositoryUrl.Contains(']'))
        {
            throw new InvalidOperationException(
                $"Git 仓库地址格式不正确: {_config.RepositoryUrl}{Environment.NewLine}" +
                "请填写 git clone 可直接使用的地址，例如 https://github.com/owner/repo.git 或 git@github.com:owner/repo.git。");
        }

        if (_config.RepositoryUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info("GitHub HTTPS 地址不会支持账号密码登录。公开仓库可直接 clone；私有仓库建议改用 SSH 地址 git@github.com:owner/repo.git。");
        }
    }

    private IReadOnlyDictionary<string, string> GitEnvironment()
    {
        var environment = new Dictionary<string, string>(_config.Environment, StringComparer.OrdinalIgnoreCase);
        environment.TryAdd("GIT_TERMINAL_PROMPT", "0");
        return environment;
    }

    private async Task RunUnityBuildAsync()
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
        AddUnityPair(args, "-customBundleVersion", _config.BundleVersion);
        AddUnityPair(args, "-customBundleIdentifier", _config.BundleIdentifier);
        AddUnityPair(args, "-customProductName", _config.ProductName);
        AddUnityPair(args, "-customAppleTeamId", _config.TeamId);
        AddUnityPair(args, "-customIosDeploymentTarget", _config.IosDeploymentTarget);

        try
        {
            await _processRunner.RunAsync(
                _paths.UnityExecutable,
                args,
                _paths.UnityProjectRoot,
                _paths.UnityProcessLogPath,
                _config.Environment);
            ValidateXcodeProjectExported();
            ValidateCocoaPodsInstallation();
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
        if (FindXcodeProjectOrWorkspace() is not null)
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

    private string? FindXcodeProjectOrWorkspace()
    {
        if (!Directory.Exists(_paths.XcodeOutputDirectory))
        {
            return null;
        }

        if (_config.UseWorkspaceIfPresent)
        {
            string? workspace = FindXcodeBundleDirectory("*.xcworkspace");
            if (workspace is not null)
            {
                return workspace;
            }
        }

        return FindXcodeBundleDirectory("*.xcodeproj");
    }

    private string? FindXcodeBundleDirectory(string pattern)
    {
        string? topLevelBundle = Directory
            .EnumerateDirectories(_paths.XcodeOutputDirectory, pattern, SearchOption.TopDirectoryOnly)
            .OrderBy(BundlePriority)
            .ThenBy(path => path.Length)
            .FirstOrDefault();

        if (topLevelBundle is not null)
        {
            return topLevelBundle;
        }

        return Directory
            .EnumerateDirectories(_paths.XcodeOutputDirectory, pattern, SearchOption.AllDirectories)
            .Where(path => !IsNestedInsideXcodeProject(path))
            .OrderBy(BundlePriority)
            .ThenBy(path => path.Length)
            .FirstOrDefault();
    }

    private static int BundlePriority(string path)
    {
        string name = Path.GetFileName(path);
        return name.StartsWith("Unity-iPhone", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private static bool IsNestedInsideXcodeProject(string path)
    {
        string directory = Path.GetDirectoryName(path) ?? "";
        char[] separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];
        return directory
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.EndsWith(".xcodeproj", StringComparison.OrdinalIgnoreCase));
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

    private void ValidateUnityProjectDirectory()
    {
        if (!Directory.Exists(_paths.UnityProjectRoot))
        {
            string candidates = FormatUnityProjectCandidates();
            throw new DirectoryNotFoundException(
                $"Unity 工程目录不存在: {_paths.UnityProjectRoot}{Environment.NewLine}" +
                $"请检查配置 unityProjectRelativePath。它必须指向包含 Assets 和 ProjectSettings 的 Unity 工程根目录，通常填 \".\"。{candidates}");
        }

        bool hasAssets = Directory.Exists(Path.Combine(_paths.UnityProjectRoot, "Assets"));
        bool hasProjectSettings = Directory.Exists(Path.Combine(_paths.UnityProjectRoot, "ProjectSettings"));
        if (!hasAssets || !hasProjectSettings)
        {
            string candidates = FormatUnityProjectCandidates();
            throw new InvalidOperationException(
                $"当前路径不是 Unity 工程根目录: {_paths.UnityProjectRoot}{Environment.NewLine}" +
                $"缺少目录: {(hasAssets ? "" : "Assets ")}{(hasProjectSettings ? "" : "ProjectSettings")}{Environment.NewLine}" +
                $"请把 unityProjectRelativePath 改成包含 Assets 和 ProjectSettings 的目录，通常填 \".\"。{candidates}");
        }

        _logger.Info($"Unity 工程目录校验通过: {_paths.UnityProjectRoot}");
        if (!Directory.Exists(Path.Combine(_paths.UnityProjectRoot, "Library")))
        {
            _logger.Warn("Unity 工程没有 Library 目录，说明这是 Git 新拉下来的干净工程。Unity 命令行会自动导入资源，不需要手动打开；第一次会比较慢。");
        }
    }

    private string FormatUnityProjectCandidates()
    {
        if (!Directory.Exists(_paths.RepositoryRoot))
        {
            return "";
        }

        string[] candidates = FindUnityProjectCandidates(_paths.RepositoryRoot, maxDepth: 4).Take(5).ToArray();
        if (candidates.Length == 0)
        {
            return "";
        }

        string lines = string.Join(
            Environment.NewLine,
            candidates.Select(path => $"  - {Path.GetRelativePath(_paths.RepositoryRoot, path)}"));

        return $"{Environment.NewLine}仓库里检测到可能的 Unity 工程目录:{Environment.NewLine}{lines}";
    }

    private static IEnumerable<string> FindUnityProjectCandidates(string root, int maxDepth)
    {
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));

        while (queue.Count > 0)
        {
            (string path, int depth) = queue.Dequeue();
            if (Directory.Exists(Path.Combine(path, "Assets")) &&
                Directory.Exists(Path.Combine(path, "ProjectSettings")))
            {
                yield return path;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (string child in EnumerateDirectoriesSafe(path))
            {
                string name = Path.GetFileName(child);
                if (name is ".git" or "Library" or "Temp" or "Obj" or "Build" or "Builds")
                {
                    continue;
                }

                queue.Enqueue((child, depth + 1));
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafe(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private void EnsureDirectoryExists(string path, string description)
    {
        Directory.CreateDirectory(path);
        _logger.Info($"{description}: {path}");
    }

    private void EnsureParentDirectoryExists(string filePath, string description)
    {
        string? parent = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        Directory.CreateDirectory(parent);
        _logger.Info($"{description}: {parent}");
    }

    private async Task RunXcodeArchiveAndExportAsync()
    {
        _logger.Info($"Xcode archive 日志: {_paths.XcodeArchiveLogPath}");
        _logger.Info($"Xcode export 日志: {_paths.XcodeExportLogPath}");

        string? selectedProjectOrWorkspace;

        if (_options.DryRun)
        {
            selectedProjectOrWorkspace = Path.Combine(_paths.XcodeOutputDirectory, "Unity-iPhone.xcodeproj");
        }
        else
        {
            selectedProjectOrWorkspace = FindXcodeProjectOrWorkspace();
        }

        if (selectedProjectOrWorkspace is null)
        {
            throw new FileNotFoundException($"Unity 导出的 Xcode 工程不存在: {_paths.XcodeOutputDirectory}");
        }

        var archiveArgs = new List<string>();
        if (selectedProjectOrWorkspace.EndsWith(".xcworkspace", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Info($"使用 Xcode workspace: {selectedProjectOrWorkspace}");
            archiveArgs.AddRange(["-workspace", selectedProjectOrWorkspace]);
        }
        else
        {
            _logger.Info($"使用 Xcode project: {selectedProjectOrWorkspace}");
            archiveArgs.AddRange(["-project", selectedProjectOrWorkspace]);
        }

        archiveArgs.AddRange([
            "-scheme", _config.Scheme,
            "-configuration", _config.Configuration,
            "-archivePath", _paths.ArchivePath
        ]);

        if (_config.AllowProvisioningUpdates)
        {
            archiveArgs.Add("-allowProvisioningUpdates");
        }

        AddXcodeSetting(archiveArgs, "DEVELOPMENT_TEAM", _config.TeamId);
        AddXcodeSetting(archiveArgs, "PRODUCT_BUNDLE_IDENTIFIER", _config.BundleIdentifier);
        AddXcodeSetting(archiveArgs, "CODE_SIGN_STYLE", ToXcodeSigningStyle(_config.SigningStyle));

        foreach ((string key, string value) in _config.XcodeBuildSettings)
        {
            AddXcodeSetting(archiveArgs, key, value);
        }

        archiveArgs.Add("archive");

        if (_config.GenerateExportOptionsPlist)
        {
            if (_options.DryRun)
            {
                _logger.Info($"[dry-run] 生成 ExportOptions.plist: {_paths.ExportOptionsPlistPath}");
            }
            else
            {
                ExportOptionsPlist.Write(_config, _paths.ExportOptionsPlistPath);
                _logger.Info($"生成 ExportOptions.plist: {_paths.ExportOptionsPlistPath}");
            }
        }

        await _processRunner.RunAsync(
            "xcodebuild",
            archiveArgs,
            _paths.XcodeOutputDirectory,
            _paths.XcodeArchiveLogPath,
            _config.Environment);

        await _processRunner.RunAsync(
            "xcodebuild",
            [
                "-exportArchive",
                "-archivePath", _paths.ArchivePath,
                "-exportPath", _paths.ExportPath,
                "-exportOptionsPlist", _paths.ExportOptionsPlistPath
            ],
            _paths.XcodeOutputDirectory,
            _paths.XcodeExportLogPath,
            _config.Environment);
    }

    private void PrintSummary()
    {
        _logger.Info($"RunId: {_paths.RunId}");
        _logger.Info($"仓库: {_config.RepositoryUrl} [{_config.Branch}]");
        _logger.Info($"工作区: {_paths.WorkspaceRoot}");
        _logger.Info($"Unity 工程: {_paths.UnityProjectRoot}");
        _logger.Info($"Xcode 输出: {_paths.XcodeOutputDirectory}");
        _logger.Info($"归档: {_paths.ArchivePath}");
        _logger.Info($"导出目录: {_paths.ExportPath}");
        _logger.Info($"日志目录: {_paths.LogsDirectory}");
    }

    private StepTimer StartStep(string name)
    {
        _logger.StepStarted(name);
        return new StepTimer(_logger, name);
    }

    private void RunStep(string name, Action action)
    {
        using StepTimer step = StartStep(name);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            step.Fail(ex);
            throw;
        }
    }

    private async Task RunStepAsync(string name, Func<Task> action)
    {
        using StepTimer step = StartStep(name);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            step.Fail(ex);
            throw;
        }
    }

    public void Dispose()
    {
        _logger.Dispose();
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

    private static void AddXcodeSetting(List<string> args, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        args.Add($"{key}={value}");
    }

    private static string ToXcodeSigningStyle(string signingStyle)
    {
        return signingStyle.Equals("manual", StringComparison.OrdinalIgnoreCase)
            ? "Manual"
            : "Automatic";
    }
}
