using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutomationUnityBuildIOS;

internal static class ConfigWizard
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
        string bundleVersion = ConsolePrompts.AskOptional("版本号 Bundle Version，例如 1.0.0", "1.0.0");
        string buildNumber = ConsolePrompts.AskOptional("构建号 Build Number，例如 1、2、100", "1");
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
        Console.WriteLine("强制重置 Git 仓库会删除 Mac 本地未提交修改和未跟踪文件。专用打包机可选 y，普通开发机建议 n。");
        bool resetRepository = ConsolePrompts.AskBool(
            "每次打包前强制重置 Git 仓库到远端分支",
            false);
        bool cleanXcodeOutputBeforeBuild = ConsolePrompts.AskBool(
            "每次打包前清理旧 Xcode 输出目录",
            true);

        var config = new BuildConfig
        {
            RepositoryUrl = repositoryUrl,
            Branch = branch,
            WorkspaceRoot = workspaceRoot,
            ProjectDirectoryName = projectDirectoryName,
            UnityProjectRelativePath = unityProjectRelativePath,

            UnityVersion = unityVersion,
            UnityExecutablePath = unityExecutablePath,
            UnityBuildMethod = unityBuildMethod,

            ArtifactsRoot = artifactsRoot,
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
            BuildNumber = buildNumber,
            IosDeploymentTarget = iosDeploymentTarget,

            AllowProvisioningUpdates = allowProvisioningUpdates,
            ResetRepository = resetRepository,
            CleanXcodeOutputBeforeBuild = cleanXcodeOutputBeforeBuild,
            UseWorkspaceIfPresent = true,
            GenerateExportOptionsPlist = true,
            CompileBitcode = null,
            UploadSymbols = true,

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

            Console.WriteLine("Team ID 必须是 10 位字母数字。不要填公司名，例如 FT Entertainment Limited。");
        }
    }

    private static void PrintSummary(BuildConfig config, string outputPath)
    {
        PrintSection("8. 配置确认");
        Console.WriteLine($"配置文件: {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"仓库: {config.RepositoryUrl}");
        Console.WriteLine($"分支: {config.Branch}");
        Console.WriteLine($"Mac 工作区: {config.WorkspaceRoot}");
        Console.WriteLine($"仓库目录名: {config.ProjectDirectoryName}");
        Console.WriteLine($"Unity 工程相对路径: {config.UnityProjectRelativePath}");
        Console.WriteLine($"Unity 版本: {(string.IsNullOrWhiteSpace(config.UnityVersion) ? "(自动或使用完整路径)" : config.UnityVersion)}");
        Console.WriteLine($"Unity 完整路径: {(string.IsNullOrWhiteSpace(config.UnityExecutablePath) ? "(未指定)" : config.UnityExecutablePath)}");
        Console.WriteLine($"Bundle ID: {config.BundleIdentifier}");
        Console.WriteLine($"iOS Deployment Target: {(string.IsNullOrWhiteSpace(config.IosDeploymentTarget) ? "(使用 Unity 项目原配置)" : config.IosDeploymentTarget)}");
        Console.WriteLine($"Team ID: {config.TeamId}");
        Console.WriteLine($"导出方式: {config.ExportMethod}");
        Console.WriteLine($"产物目录: {config.ArtifactsRoot}");
        Console.WriteLine($"强制重置 Git: {(config.ResetRepository ? "是" : "否")}");
    }

    private static string WriteConfig(BuildConfig config, string outputPath, bool force)
    {
        string fullPath = Path.GetFullPath(outputPath);
        if (File.Exists(fullPath) && !force)
        {
            throw new InvalidOperationException($"{fullPath} 已存在。需要覆盖时加 --force，或在向导里换一个文件名。");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string json = JsonSerializer.Serialize(config, JsonOptions.IndentedCamelCase);
        File.WriteAllText(fullPath, json + Environment.NewLine, TextEncodings.Utf8Bom);

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

internal static class ConfigValueNormalizer
{
    private static readonly Regex MarkdownLinkRegex = new(@"\((?<url>https?://[^)\s]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BareGitHubRegex = new(@"^https?://github\.com/[^/\s]+/[^/\s#?]+/?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string NormalizeRepositoryUrl(string value)
    {
        string normalized = (value ?? "").Trim();
        Match markdownMatch = MarkdownLinkRegex.Match(normalized);
        if (markdownMatch.Success)
        {
            normalized = markdownMatch.Groups["url"].Value;
        }

        int queryIndex = normalized.IndexOfAny(['?', '#']);
        if (queryIndex >= 0)
        {
            normalized = normalized[..queryIndex];
        }

        normalized = normalized.Trim().TrimEnd('/');

        if (BareGitHubRegex.IsMatch(normalized) && !normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalized += ".git";
        }

        if (normalized.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase) &&
            !normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalized += ".git";
        }

        return normalized;
    }
}

internal static class ConfigFileSelector
{
    public static IReadOnlyList<string> FindConfigFiles()
    {
        var files = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFiles(Environment.CurrentDirectory, "build-ios*.json", files);
        AddFiles(Environment.CurrentDirectory, "*.iosbuild.json", files);

        string configsDirectory = Path.Combine(Environment.CurrentDirectory, "configs");
        AddFiles(configsDirectory, "*.json", files);

        return files
            .Where(file => !Path.GetFileName(file).Equals("build-ios.sample.json", StringComparison.OrdinalIgnoreCase))
            .Select(ToDisplayPath)
            .ToArray();
    }

    public static string SelectConfigFile(string actionName)
    {
        while (true)
        {
            IReadOnlyList<string> configs = FindConfigFiles();
            if (configs.Count == 0)
            {
                Console.WriteLine("没有找到可用配置文件。");
                if (ConsolePrompts.AskBool("是否现在初始化一个新配置", true))
                {
                    string created = ConfigWizard.Run("build-ios.json", configWasSpecified: false, force: false);
                    return created;
                }

                throw new FileNotFoundException("没有选择配置文件。");
            }

            Console.WriteLine();
            Console.WriteLine($"请选择用于{actionName}的配置文件:");
            for (int i = 0; i < configs.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {configs[i]}");
            }

            Console.WriteLine("  0. 初始化新配置");
            Console.WriteLine("也可以直接输入配置文件路径。");
            Console.Write("> ");

            string? input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                return configs[0];
            }

            if (input == "0")
            {
                string created = ConfigWizard.Run("build-ios.json", configWasSpecified: false, force: false);
                return created;
            }

            if (int.TryParse(input, out int number) && number >= 1 && number <= configs.Count)
            {
                return configs[number - 1];
            }

            string candidate = Path.GetFullPath(input);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            Console.WriteLine($"找不到配置文件: {input}");
        }
    }

    private static void AddFiles(string directory, string pattern, SortedSet<string> files)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly))
        {
            files.Add(Path.GetFullPath(file));
        }
    }

    private static string ToDisplayPath(string fullPath)
    {
        string relative = Path.GetRelativePath(Environment.CurrentDirectory, fullPath);
        return relative.StartsWith("..", StringComparison.Ordinal) ? fullPath : relative;
    }
}

internal static class ConsolePrompts
{
    public static string AskRequired(string label, string hint)
    {
        while (true)
        {
            Console.WriteLine($"{label} ({hint})");
            Console.Write("> ");
            string value = Console.ReadLine()?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            Console.WriteLine("这个值必填。");
        }
    }

    public static string AskOptional(string label, string defaultValue)
    {
        Console.Write(string.IsNullOrEmpty(defaultValue) ? $"{label}: " : $"{label} [{defaultValue}]: ");
        string value = Console.ReadLine()?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    public static string AskChoice(string label, IReadOnlyList<string> choices, string defaultValue)
    {
        while (true)
        {
            Console.WriteLine($"{label} [{defaultValue}]");
            for (int i = 0; i < choices.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {choices[i]}");
            }

            Console.Write("> ");
            string value = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (int.TryParse(value, out int number) && number >= 1 && number <= choices.Count)
            {
                return choices[number - 1];
            }

            string? match = choices.FirstOrDefault(choice => choice.Equals(value, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }

            Console.WriteLine("请输入列表编号或列表中的值。");
        }
    }

    public static bool AskBool(string label, bool defaultValue)
    {
        string suffix = defaultValue ? "[Y/n]" : "[y/N]";
        while (true)
        {
            Console.Write($"{label} {suffix}: ");
            string value = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (value is "y" or "yes" or "1" or "true" or "是")
            {
                return true;
            }

            if (value is "n" or "no" or "0" or "false" or "否")
            {
                return false;
            }

            Console.WriteLine("请输入 y 或 n。");
        }
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions IndentedCamelCase = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

internal static class TextEncodings
{
    public static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
}
