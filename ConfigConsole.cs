using System.Text;
using System.Text.Json;

namespace AutomationUnityBuildIOS;

internal static class ConfigWizard
{
    public static string Run(string defaultConfigPath, bool configWasSpecified, bool force)
    {
        Console.WriteLine();
        Console.WriteLine("开始初始化 iOS 打包配置。直接回车会使用括号里的默认值。");
        Console.WriteLine();

        string profileName = ConsolePrompts.AskOptional("配置名称，例如 dev、testflight、release", "dev");
        string safeProfileName = SanitizeFileName(profileName);
        string suggestedPath = configWasSpecified && !string.IsNullOrWhiteSpace(defaultConfigPath)
            ? defaultConfigPath
            : Path.Combine("configs", $"build-ios.{safeProfileName}.json");

        string outputPath = ConsolePrompts.AskOptional("配置文件保存路径", suggestedPath);
        string repositoryUrl = ConsolePrompts.AskRequired(
            "Git 仓库地址",
            "例如 git@github.com:company/game.git");
        string branch = ConsolePrompts.AskOptional("Git 分支", "main");
        string inferredProjectName = InferProjectName(repositoryUrl);
        string projectDirectoryName = ConsolePrompts.AskOptional("Mac 工作区里的项目目录名", inferredProjectName);
        string unityProjectRelativePath = ConsolePrompts.AskOptional("Unity 工程相对仓库根目录路径", ".");

        string unityVersion = ConsolePrompts.AskOptional("Unity 版本，留空则使用 unityExecutablePath 或自动查找", "");
        string unityExecutablePath = ConsolePrompts.AskOptional("Unity 可执行文件完整路径，常规安装可留空", "");
        string unityBuildMethod = ConsolePrompts.AskOptional("Unity 构建入口方法", "BuildAutomation.IOSBuilder.Build");

        string productName = ConsolePrompts.AskOptional("游戏显示名称 productName", projectDirectoryName);
        string bundleIdentifier = ConsolePrompts.AskRequired(
            "iOS Bundle Identifier",
            "例如 com.company.game");
        string bundleVersion = ConsolePrompts.AskOptional("版本号 bundleVersion", "1.0.0");
        string buildNumber = ConsolePrompts.AskOptional("构建号 buildNumber", "1");

        string teamId = ConsolePrompts.AskRequired(
            "Apple Developer Team ID",
            "例如 ABCDE12345");
        string signingStyle = ConsolePrompts.AskChoice("签名方式", ["automatic", "manual"], "automatic");
        string exportMethod = ConsolePrompts.AskChoice(
            "Xcode 导出方式 exportMethod",
            ["development", "ad-hoc", "app-store", "enterprise"],
            "development");

        string workspaceRoot = ConsolePrompts.AskOptional("Mac 构建工作区目录", "~/UnityBuildWorkspace");
        string artifactsRoot = ConsolePrompts.AskOptional(
            "打包产物输出目录",
            $"~/UnityBuildArtifacts/{projectDirectoryName}");

        bool allowProvisioningUpdates = ConsolePrompts.AskBool("允许 xcodebuild 自动更新签名配置", true);
        bool resetRepository = ConsolePrompts.AskBool("每次打包前强制重置 Git 仓库到远端分支", false);
        bool cleanXcodeOutputBeforeBuild = ConsolePrompts.AskBool("每次打包前清理旧 Xcode 输出目录", true);

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

        return WriteConfig(config, outputPath, force);
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
        Console.WriteLine($"运行示例: AutomationUnityBuildIOS run --config \"{fullPath}\"");

        return fullPath;
    }

    private static string InferProjectName(string repositoryUrl)
    {
        string url = repositoryUrl.TrimEnd('/', '\\');
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
