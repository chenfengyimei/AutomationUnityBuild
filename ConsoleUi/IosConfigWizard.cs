using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutomationUnityBuildIOS;

internal static class IosConfigWizard
{
    public static string Run(string defaultConfigPath, bool configWasSpecified, bool force)
    {
        Console.WriteLine();
        Console.WriteLine("===== Unity iOS 打包配置初始化向导 =====");
        Console.WriteLine("这个向导只需要填写一次。生成配置后，后续打包直接选择配置文件即可。");
        Console.WriteLine("直接回车会使用方括号里的默认值。");

        PrintSection("1. 配置文件");
        string profileName = ConsolePrompts.AskOptional(
            "配置名称，用来区分 dev、testflight、release 等环境",
            "dev");
        string safeProfileName = SanitizeFileName(profileName);
        string suggestedPath = configWasSpecified && !string.IsNullOrWhiteSpace(defaultConfigPath)
            ? defaultConfigPath
            : Path.Combine("configs", $"build-ios.{safeProfileName}.json");
        string outputPath = ConsolePrompts.AskOptional("配置文件保存路径", suggestedPath);

        PrintSection("2. Git 仓库");
        Console.WriteLine("仓库地址可以填 SSH 或 HTTPS，例如：");
        Console.WriteLine("  git@github.com:company/game.git");
        Console.WriteLine("  https://github.com/company/game.git");
        Console.WriteLine("如果你从网页复制了 GitHub 链接，向导会尽量自动整理成 git clone 可用的地址。");
        string repositoryUrl = AskRepositoryUrl();
        string branch = ConsolePrompts.AskOptional("要打包的 Git 分支", "main");
        string inferredProjectName = InferProjectName(repositoryUrl);
        string projectDirectoryName = ConsolePrompts.AskOptional(
            "Mac 工作区里的仓库文件夹名，也就是 clone 后的目录名",
            inferredProjectName);

        PrintSection("3. Unity 工程位置");
        Console.WriteLine("这里问的是 Unity 工程根目录，不是打包输出目录。");
        Console.WriteLine("正确目录里面必须能看到 Assets 和 ProjectSettings。");
        Console.WriteLine("如果仓库根目录就是 Unity 工程，直接回车使用 .");
        Console.WriteLine("不要填 build、Builds、XcodeProject 这类输出目录。");
        string unityProjectRelativePath = AskUnityProjectRelativePath();

        PrintSection("4. Unity 编辑器");
        Console.WriteLine("如果 Unity 是 Hub 默认安装，一般只填版本号即可。");
        Console.WriteLine("Mac 可用这个命令查看版本：ls /Applications/Unity/Hub/Editor");
        string unityVersion = ConsolePrompts.AskOptional("Unity 版本，例如 2022.3.62f2c1", "");
        string unityExecutablePath = ConsolePrompts.AskOptional(
            "Unity 可执行文件完整路径。常规安装可留空",
            "");
        string unityBuildMethod = ConsolePrompts.AskOptional(
            "Unity 构建入口方法。保持默认即可，除非你改了 Assets/Editor/BuildIOS.cs",
            "BuildAutomation.IOSBuilder.Build");

        PrintSection("5. App 信息");
        string productName = ConsolePrompts.AskOptional("游戏显示名称 Product Name", projectDirectoryName);
        string bundleIdentifier = AskBundleIdentifier();
        bool syncBundleVersionFromUnity = ConsolePrompts.AskBool(
            "版本号 Bundle Version 是否同步 Unity 项目设置（推荐开启）",
            true);
        string bundleVersion = "";
        if (syncBundleVersionFromUnity)
        {
            Console.WriteLine("已选择同步 Unity 项目版本号：打包时不会用配置文件覆盖 PlayerSettings.bundleVersion。");
            Console.WriteLine("打包成功后，工具会把 Unity 实际版本号记录回配置文件。");
        }
        else
        {
            bundleVersion = AskBundleVersion();
        }

        string buildNumber = ConsolePrompts.AskOptional("构建号 Build Number，例如 1、2、100", "1");
        bool autoIncrementBuildNumber = ConsolePrompts.AskBool(
            "每次正式打包前自动将 Build Number +1",
            true);
        string iosDeploymentTarget = AskIosDeploymentTarget();

        PrintSection("6. Apple 签名和导出");
        Console.WriteLine("Team ID 是 10 位字母数字，不是公司名。");
        Console.WriteLine("可以在 Apple Developer 账号 Membership，或 Xcode -> Settings -> Accounts 里查看。");
        string teamId = AskAppleTeamId();
        string signingStyle = ConsolePrompts.AskChoice(
            "签名方式。新手建议 automatic",
            ["automatic", "manual"],
            "automatic");
        Console.WriteLine("导出方式说明：development 本机调试；ad-hoc 内部分发；app-store 上传 TestFlight/App Store；enterprise 企业分发。");
        string exportMethod = ConsolePrompts.AskChoice(
            "Xcode 导出方式 exportMethod",
            ["development", "ad-hoc", "app-store", "enterprise"],
            "development");
        bool appStoreConnectUploadEnabled = false;
        string appStoreConnectApiKeyPath = "";
        string appStoreConnectApiKeyId = "";
        string appStoreConnectApiIssuerId = "";
        if (exportMethod.Equals("app-store", StringComparison.OrdinalIgnoreCase))
        {
            appStoreConnectUploadEnabled = ConsolePrompts.AskBool(
                "打包完成后自动上传到 App Store Connect/TestFlight",
                false);
            if (appStoreConnectUploadEnabled)
            {
                Console.WriteLine("需要先在 App Store Connect 生成 API Key，并把 .p8 文件放到 Mac 打包机本地安全目录。");
                appStoreConnectApiKeyPath = ConsolePrompts.AskRequired(
                    "API Key .p8 文件路径",
                    "例如 ~/Secrets/AuthKey_XXXXXXXXXX.p8");
                appStoreConnectApiKeyId = ConsolePrompts.AskRequired(
                    "API Key ID",
                    "例如 ABCDE12345");
                appStoreConnectApiIssuerId = ConsolePrompts.AskRequired(
                    "Issuer ID",
                    "App Store Connect API 页面里的 Issuer ID");
            }
        }
        else
        {
            Console.WriteLine("App Store Connect/TestFlight 自动上传只适用于 exportMethod=app-store。");
        }

        PrintSection("7. Mac 路径和清理策略");
        Console.WriteLine("下面这些目录不用提前创建。打包时如果不存在，工具会自动创建。");
        string workspaceRoot = ConsolePrompts.AskOptional(
            "Mac 构建工作区目录。Git 仓库会 clone 到这里",
            "~/UnityBuildWorkspace");
        string artifactsRoot = ConsolePrompts.AskOptional(
            "打包产物输出目录。日志、Xcode 工程、ipa 都会放这里",
            $"~/UnityBuildArtifacts/{projectDirectoryName}");
        bool allowProvisioningUpdates = ConsolePrompts.AskBool(
            "允许 xcodebuild 自动更新签名配置",
            true);
        Console.WriteLine("强制重置 Git 仓库会删除 Mac 本地未提交修改和未跟踪文件。专用打包机推荐 y，只有把这个目录当开发目录用时才选 n。");
        bool resetRepository = ConsolePrompts.AskBool(
            "每次打包前强制重置 Git 仓库到远端分支",
            true);
        bool preserveUnityLibraryOnReset = !resetRepository || ConsolePrompts.AskBool(
            "强制重置时保留 Unity Library 缓存，避免每次重新导入资源",
            true);
        bool cleanXcodeOutputBeforeBuild = ConsolePrompts.AskBool(
            "每次打包前清理旧 Xcode 输出目录",
            true);
        bool copyArchiveToOrganizer = ConsolePrompts.AskBool(
            "Xcode archive 成功后复制 .xcarchive 到 Xcode Organizer",
            true);

        var config = new BuildConfig
        {
            ConfigName = profileName,
            RepositoryUrl = repositoryUrl,
            AllowedRepositoryUrls = [repositoryUrl],
            Branch = branch,
            WorkspaceRoot = workspaceRoot,
            AllowedWorkspaceRoots = [workspaceRoot],
            ProjectDirectoryName = projectDirectoryName,
            UnityProjectRelativePath = unityProjectRelativePath,

            UnityVersion = unityVersion,
            UnityExecutablePath = unityExecutablePath,
            UnityBuildMethod = unityBuildMethod,

            ArtifactsRoot = artifactsRoot,
            AllowedArtifactsRoots = [artifactsRoot],
            XcodeOutputDirectory = "",
            ArchivePath = "",
            ExportPath = "",
            LogsDirectory = "",

            Scheme = "Unity-iPhone",
            Configuration = "Release",
            ExportMethod = exportMethod,
            TeamId = teamId,
            SigningStyle = signingStyle,
            ExportOptionsPlistPath = "",

            BundleIdentifier = bundleIdentifier,
            ProductName = productName,
            BundleVersion = bundleVersion,
            SyncBundleVersionFromUnity = syncBundleVersionFromUnity,
            BuildNumber = buildNumber,
            AutoIncrementBuildNumber = autoIncrementBuildNumber,
            IosDeploymentTarget = iosDeploymentTarget,

            AllowProvisioningUpdates = allowProvisioningUpdates,
            ResetRepository = resetRepository,
            PreserveUnityLibraryOnReset = preserveUnityLibraryOnReset,
            CleanXcodeOutputBeforeBuild = cleanXcodeOutputBeforeBuild,
            UseWorkspaceIfPresent = true,
            GenerateExportOptionsPlist = true,
            CopyArchiveToOrganizer = copyArchiveToOrganizer,
            SaveConfigSnapshot = true,
            CompileBitcode = null,
            UploadSymbols = true,
            AppStoreConnectUploadEnabled = appStoreConnectUploadEnabled,
            AppStoreConnectApiKeyPath = appStoreConnectApiKeyPath,
            AppStoreConnectApiKeyId = appStoreConnectApiKeyId,
            AppStoreConnectApiIssuerId = appStoreConnectApiIssuerId,

            XcodeBuildSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ProvisioningProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        PrintSummary(config, outputPath);
        if (!ConsolePrompts.AskBool("确认生成这个配置文件", true))
        {
            throw new OperationCanceledException("已取消生成配置文件。可以重新运行 01 再填。");
        }

        return WriteConfig(config, outputPath, force);
    }

    private static string AskRepositoryUrl()
    {
        while (true)
        {
            string input = ConsolePrompts.AskRequired(
                "Git 仓库地址",
                "例如 https://github.com/company/game.git");
            string normalized = ConfigValueNormalizer.NormalizeRepositoryUrl(input);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                Console.WriteLine("仓库地址为空，请重新输入。");
                continue;
            }

            if (normalized != input.Trim())
            {
                Console.WriteLine($"已整理为: {normalized}");
            }

            if (!LooksLikeGitUrl(normalized))
            {
                Console.WriteLine("这个地址看起来不像 Git 仓库地址。请填 HTTPS/SSH 仓库地址，不要填网页标题。");
                if (!ConsolePrompts.AskBool("仍然使用这个地址", false))
                {
                    continue;
                }
            }

            return normalized;
        }
    }

    private static string AskUnityProjectRelativePath()
    {
        while (true)
        {
            string value = ConsolePrompts.AskOptional(
                "Unity 工程相对仓库根目录路径",
                ".");
            value = value.Trim().Trim('/', '\\');
            if (string.IsNullOrWhiteSpace(value))
            {
                value = ".";
            }

            string lastPart = value == "." ? "." : Path.GetFileName(value);
            if (lastPart.Equals("build", StringComparison.OrdinalIgnoreCase) ||
                lastPart.Equals("builds", StringComparison.OrdinalIgnoreCase) ||
                lastPart.Equals("xcode", StringComparison.OrdinalIgnoreCase) ||
                lastPart.Equals("xcodeproject", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"你填的是 {value}，它看起来像打包输出目录，不像 Unity 工程根目录。");
                Console.WriteLine("Unity 工程目录必须包含 Assets 和 ProjectSettings。");
                if (!ConsolePrompts.AskBool("确认继续使用这个路径", false))
                {
                    continue;
                }
            }

            return value;
        }
    }

    private static string AskBundleIdentifier()
    {
        while (true)
        {
            string value = ConsolePrompts.AskRequired(
                "iOS Bundle Identifier",
                "例如 com.company.game");
            if (Regex.IsMatch(value, @"^[A-Za-z0-9][A-Za-z0-9.-]+\.[A-Za-z0-9.-]+$"))
            {
                return value;
            }

            Console.WriteLine("Bundle Identifier 通常像 com.company.game，至少包含一个点，且不要包含空格或中文。");
        }
    }

    private static string AskBundleVersion()
    {
        while (true)
        {
            string value = ConsolePrompts.AskOptional("强制使用的版本号 Bundle Version，例如 1.0.0", "1.0.0");
            if (Version.TryParse(value, out _))
            {
                return value.Trim();
            }

            Console.WriteLine("Bundle Version 请输入版本号格式，例如 1.0.0 或 1.2.3。");
        }
    }

    private static string AskIosDeploymentTarget()
    {
        while (true)
        {
            string value = ConsolePrompts.AskOptional(
                "iOS 最低系统版本 Deployment Target。LevelPlay/IronSource 新版本通常需要 13.0 或更高",
                "13.0");

            if (Version.TryParse(value, out _))
            {
                return value;
            }

            Console.WriteLine("iOS Deployment Target 必须是版本号格式，例如 13.0 或 14.0。");
        }
    }

    private static string AskAppleTeamId()
    {
        while (true)
        {
            string value = ConsolePrompts.AskRequired(
                "Apple Developer Team ID",
                "10 位字母数字，例如 ABCDE12345，不是公司名");
            value = value.Trim().ToUpperInvariant();
            if (Regex.IsMatch(value, @"^[A-Z0-9]{10}$"))
            {
                return value;
            }

            Console.WriteLine("Team ID 必须是 10 位字母数字。不要填公司名，例如 Your Company Ltd.。");
        }
    }

    private static void PrintSummary(BuildConfig config, string outputPath)
    {
        PrintSection("8. 配置确认");
        Console.WriteLine($"配置名称: {DisplayOptional(config.ConfigName)}");
        Console.WriteLine($"配置文件: {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"仓库: {config.RepositoryUrl}");
        Console.WriteLine($"分支: {config.Branch}");
        Console.WriteLine($"Mac 工作区: {config.WorkspaceRoot}");
        Console.WriteLine($"仓库目录名: {config.ProjectDirectoryName}");
        Console.WriteLine($"Unity 工程相对路径: {config.UnityProjectRelativePath}");
        Console.WriteLine($"Unity 版本: {(string.IsNullOrWhiteSpace(config.UnityVersion) ? "(自动或使用完整路径)" : config.UnityVersion)}");
        Console.WriteLine($"Unity 完整路径: {(string.IsNullOrWhiteSpace(config.UnityExecutablePath) ? "(未指定)" : config.UnityExecutablePath)}");
        Console.WriteLine($"Bundle ID: {config.BundleIdentifier}");
        Console.WriteLine(config.SyncBundleVersionFromUnity
            ? $"Bundle Version: 同步 Unity 项目设置（当前记录值: {DisplayOptional(config.BundleVersion)}）"
            : $"Bundle Version: 使用固定值 {config.BundleVersion}");
        Console.WriteLine($"Build Number: {config.BuildNumber}");
        Console.WriteLine($"Build Number 自动+1: {(config.AutoIncrementBuildNumber ? "是" : "否")}");
        Console.WriteLine($"iOS Deployment Target: {(string.IsNullOrWhiteSpace(config.IosDeploymentTarget) ? "(使用 Unity 项目原配置)" : config.IosDeploymentTarget)}");
        Console.WriteLine($"Team ID: {config.TeamId}");
        Console.WriteLine($"导出方式: {config.ExportMethod}");
        Console.WriteLine($"App Store Connect 自动上传: {(config.AppStoreConnectUploadEnabled ? "是" : "否")}");
        Console.WriteLine($"产物目录: {config.ArtifactsRoot}");
        Console.WriteLine($"复制 archive 到 Organizer: {(config.CopyArchiveToOrganizer ? "是" : "否")}");
        Console.WriteLine($"强制重置 Git: {(config.ResetRepository ? "是" : "否")}");
        Console.WriteLine($"强制重置时保留 Unity Library: {(config.PreserveUnityLibraryOnReset ? "是" : "否")}");
    }

    private static string DisplayOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(空)" : value;
    }

    private static string WriteConfig(BuildConfig config, string outputPath, bool force)
    {
        string fullPath = Path.GetFullPath(outputPath);
        if (File.Exists(fullPath) && !force)
        {
            throw new InvalidOperationException($"{fullPath} 已存在。需要覆盖时加 --force，或在向导里换一个文件名。");
        }

        PathTools.EnsureParentDirectory(fullPath);
        ConfigFileWriter.Save(fullPath, config);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine();
        Console.WriteLine($"配置已生成: {fullPath}");
        Console.ResetColor();
        Console.WriteLine("下次打包可以直接选择这个配置，不需要重复填写。");
        Console.WriteLine($"预览命令: AutomationUnityBuildIOS 05 --config \"{fullPath}\"");
        Console.WriteLine($"正式打包: AutomationUnityBuildIOS 06 --config \"{fullPath}\"");

        return fullPath;
    }

    private static string InferProjectName(string repositoryUrl)
    {
        string url = ConfigValueNormalizer.NormalizeRepositoryUrl(repositoryUrl).TrimEnd('/', '\\');
        string name = url.Split('/', '\\', ':').LastOrDefault() ?? "UnityGame";
        return name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    private static string SanitizeFileName(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value) ? "dev" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '-');
        }

        return safe.Replace(' ', '-');
    }

    private static bool LooksLikeGitUrl(string value)
    {
        return value.StartsWith("git@", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               value.EndsWith(".git", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintSection(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        Console.WriteLine(new string('-', Math.Min(title.Length, 40)));
    }
}

