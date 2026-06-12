using System.Text.RegularExpressions;

namespace AutomationUnityBuildIOS;

internal static class ConfigEditor
{
    private static readonly IReadOnlyList<ConfigField> Fields =
    [
        new(1, "常用 App 信息", "Product Name", config => Display(config.ProductName), config =>
        {
            config.ProductName = AskString("Product Name", config.ProductName);
            return true;
        }),
        new(2, "常用 App 信息", "Bundle Identifier", config => Display(config.BundleIdentifier), config =>
        {
            config.BundleIdentifier = AskBundleIdentifier(config.BundleIdentifier);
            return true;
        }),
        new(3, "常用 App 信息", "Bundle Version", config => Display(config.BundleVersion), config =>
        {
            if (config.SyncBundleVersionFromUnity)
            {
                Console.WriteLine("当前已开启同步 Unity 项目版本号；这里的 Bundle Version 只是记录值，打包成功后会被 Unity 实际版本号更新。");
            }

            config.BundleVersion = AskVersion("Bundle Version", config.BundleVersion, allowEmpty: config.SyncBundleVersionFromUnity);
            return true;
        }),
        new(4, "常用 App 信息", "Sync Bundle Version From Unity", config => BoolText(config.SyncBundleVersionFromUnity), config =>
        {
            bool syncBundleVersionFromUnity = ConsolePrompts.AskBool("Sync Bundle Version From Unity", config.SyncBundleVersionFromUnity);
            config.SyncBundleVersionFromUnity = syncBundleVersionFromUnity;
            if (!syncBundleVersionFromUnity)
            {
                Console.WriteLine("关闭同步后，打包时会用下面这个 Bundle Version 强制覆盖 Unity 项目设置。");
                config.BundleVersion = AskVersion("Bundle Version", Default(config.BundleVersion, "1.0.0"), allowEmpty: false);
            }

            return true;
        }),
        new(5, "常用 App 信息", "Build Number", config => Display(config.BuildNumber), config =>
        {
            config.BuildNumber = AskBuildNumber(config.BuildNumber, config.AutoIncrementBuildNumber);
            return true;
        }),
        new(6, "常用 App 信息", "Auto Increment Build Number", config => BoolText(config.AutoIncrementBuildNumber), config =>
        {
            bool autoIncrementBuildNumber = ConsolePrompts.AskBool("Auto Increment Build Number", config.AutoIncrementBuildNumber);
            if (autoIncrementBuildNumber && !CanAutoIncrementBuildNumber(config.BuildNumber))
            {
                Console.WriteLine("当前 Build Number 不是纯数字，无法开启自动+1。请先把 Build Number 改成 1、2、100 这类数字。");
                return false;
            }

            config.AutoIncrementBuildNumber = autoIncrementBuildNumber;
            return true;
        }),
        new(7, "常用 App 信息", "iOS Deployment Target", config => Display(config.IosDeploymentTarget), config =>
        {
            config.IosDeploymentTarget = AskVersion("iOS Deployment Target", config.IosDeploymentTarget, allowEmpty: true);
            return true;
        }),
        new(8, "签名和导出", "Apple Team ID", config => Display(config.TeamId), config =>
        {
            config.TeamId = AskAppleTeamId(config.TeamId);
            return true;
        }),
        new(9, "签名和导出", "Signing Style", config => Display(config.SigningStyle), config =>
        {
            config.SigningStyle = ConsolePrompts.AskChoice("Signing Style", ["automatic", "manual"], Default(config.SigningStyle, "automatic"));
            return true;
        }),
        new(10, "签名和导出", "Export Method", config => Display(config.ExportMethod), config =>
        {
            config.ExportMethod = ConsolePrompts.AskChoice("Export Method", ["development", "ad-hoc", "app-store", "enterprise"], Default(config.ExportMethod, "development"));
            return true;
        }),
        new(11, "签名和导出", "Allow Provisioning Updates", config => BoolText(config.AllowProvisioningUpdates), config =>
        {
            config.AllowProvisioningUpdates = ConsolePrompts.AskBool("Allow Provisioning Updates", config.AllowProvisioningUpdates);
            return true;
        }),
        new(12, "签名和导出", "Copy Archive To Organizer", config => BoolText(config.CopyArchiveToOrganizer), config =>
        {
            config.CopyArchiveToOrganizer = ConsolePrompts.AskBool("Copy Archive To Organizer", config.CopyArchiveToOrganizer);
            return true;
        }),
        new(13, "Git 和 Unity", "Repository Url", config => Display(config.RepositoryUrl), config =>
        {
            config.RepositoryUrl = AskRepositoryUrl(config.RepositoryUrl);
            return true;
        }),
        new(14, "Git 和 Unity", "Branch", config => Display(config.Branch), config =>
        {
            config.Branch = AskRequiredWithDefault("Branch", config.Branch);
            return true;
        }),
        new(15, "Git 和 Unity", "Repository Folder Name", config => Display(config.ProjectDirectoryName), config =>
        {
            config.ProjectDirectoryName = AskString("Repository Folder Name", config.ProjectDirectoryName);
            return true;
        }),
        new(16, "Git 和 Unity", "Unity Project Relative Path", config => Display(config.UnityProjectRelativePath), config =>
        {
            config.UnityProjectRelativePath = AskUnityProjectRelativePath(config.UnityProjectRelativePath);
            return true;
        }),
        new(17, "Git 和 Unity", "Unity Version", config => Display(config.UnityVersion), config =>
        {
            config.UnityVersion = AskString("Unity Version", config.UnityVersion);
            return true;
        }),
        new(18, "Git 和 Unity", "Unity Executable Path", config => Display(config.UnityExecutablePath), config =>
        {
            config.UnityExecutablePath = AskString("Unity Executable Path", config.UnityExecutablePath);
            return true;
        }),
        new(19, "Git 和 Unity", "Unity Build Method", config => Display(config.UnityBuildMethod), config =>
        {
            config.UnityBuildMethod = AskRequiredWithDefault("Unity Build Method", config.UnityBuildMethod);
            return true;
        }),
        new(20, "路径和清理策略", "Workspace Root", config => Display(config.WorkspaceRoot), config =>
        {
            config.WorkspaceRoot = AskRequiredWithDefault("Workspace Root", config.WorkspaceRoot);
            return true;
        }),
        new(21, "路径和清理策略", "Artifacts Root", config => Display(config.ArtifactsRoot), config =>
        {
            config.ArtifactsRoot = AskRequiredWithDefault("Artifacts Root", config.ArtifactsRoot);
            return true;
        }),
        new(22, "路径和清理策略", "Reset Repository", config => BoolText(config.ResetRepository), config =>
        {
            config.ResetRepository = ConsolePrompts.AskBool("Reset Repository", config.ResetRepository);
            if (!config.ResetRepository)
            {
                config.PreserveUnityLibraryOnReset = true;
            }

            return true;
        }),
        new(23, "路径和清理策略", "Preserve Unity Library On Reset", config => BoolText(config.PreserveUnityLibraryOnReset), config =>
        {
            config.PreserveUnityLibraryOnReset = ConsolePrompts.AskBool("Preserve Unity Library On Reset", config.PreserveUnityLibraryOnReset);
            return true;
        }),
        new(24, "路径和清理策略", "Clean Xcode Output Before Build", config => BoolText(config.CleanXcodeOutputBeforeBuild), config =>
        {
            config.CleanXcodeOutputBeforeBuild = ConsolePrompts.AskBool("Clean Xcode Output Before Build", config.CleanXcodeOutputBeforeBuild);
            return true;
        }),
        new(25, "Xcode 高级项", "Scheme", config => Display(config.Scheme), config =>
        {
            config.Scheme = AskRequiredWithDefault("Scheme", config.Scheme);
            return true;
        }),
        new(26, "Xcode 高级项", "Configuration", config => Display(config.Configuration), config =>
        {
            config.Configuration = ConsolePrompts.AskChoice("Configuration", ["Release", "Debug"], Default(config.Configuration, "Release"));
            return true;
        }),
        new(27, "配置文件信息", "Config Name", config => Display(config.ConfigName), config =>
        {
            config.ConfigName = AskString("Config Name", config.ConfigName);
            return true;
        })
    ];

    public static void Run(string configPath)
    {
        string fullPath = Path.GetFullPath(configPath);
        BuildConfig config = BuildConfig.Load(fullPath);

        while (true)
        {
            PrintMenu(config, fullPath);
            Console.Write("> ");
            string input = Console.ReadLine()?.Trim() ?? "";

            if (input is "0" or "")
            {
                Console.WriteLine("已退出配置修改。");
                return;
            }

            if (input.Equals("s", StringComparison.OrdinalIgnoreCase))
            {
                PrintConfigSummary(config, fullPath);
                continue;
            }

            if (!int.TryParse(input, out int number))
            {
                Console.WriteLine("请输入要修改的编号，输入 s 查看摘要，输入 0 退出。");
                continue;
            }

            bool changed = EditField(config, number);
            if (!changed)
            {
                continue;
            }

            Save(config, fullPath);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"已保存: {fullPath}");
            Console.ResetColor();
        }
    }

    private static void PrintMenu(BuildConfig config, string fullPath)
    {
        Console.WriteLine();
        Console.WriteLine("===== 修改 Unity iOS 打包配置 =====");
        Console.WriteLine($"配置文件: {fullPath}");
        Console.WriteLine("输入编号修改对应内容；直接回车或输入 0 退出；输入 s 查看当前摘要。");
        string? currentGroup = null;
        foreach (ConfigField field in Fields)
        {
            if (!string.Equals(currentGroup, field.Group, StringComparison.Ordinal))
            {
                currentGroup = field.Group;
                Console.WriteLine();
                Console.WriteLine(currentGroup);
            }

            Console.WriteLine($"  {field.Number:00}. {field.Label}: {field.Read(config)}");
        }
    }

    private static bool EditField(BuildConfig config, int number)
    {
        ConfigField? field = Fields.FirstOrDefault(field => field.Number == number);
        if (field is null)
        {
            Console.WriteLine("没有这个编号。");
            return false;
        }

        return field.Edit(config);
    }

    private static void PrintConfigSummary(BuildConfig config, string fullPath)
    {
        Console.WriteLine();
        Console.WriteLine("----- 当前配置摘要 -----");
        Console.WriteLine($"配置名称: {Display(config.ConfigName)}");
        Console.WriteLine($"配置文件: {fullPath}");
        Console.WriteLine($"仓库: {config.RepositoryUrl} [{config.Branch}]");
        Console.WriteLine($"Unity 工程: {config.ProjectDirectoryName}/{config.UnityProjectRelativePath}");
        Console.WriteLine($"Unity: {(string.IsNullOrWhiteSpace(config.UnityExecutablePath) ? config.UnityVersion : config.UnityExecutablePath)}");
        Console.WriteLine($"App: {config.ProductName}, {config.BundleIdentifier}, version={Display(config.BundleVersion)}, syncUnityVersion={config.SyncBundleVersionFromUnity}, build={config.BuildNumber}, autoIncrementBuild={config.AutoIncrementBuildNumber}");
        Console.WriteLine($"iOS Deployment Target: {Display(config.IosDeploymentTarget)}");
        Console.WriteLine($"签名: team={config.TeamId}, style={config.SigningStyle}, export={config.ExportMethod}");
        Console.WriteLine($"工作区: {config.WorkspaceRoot}");
        Console.WriteLine($"产物: {config.ArtifactsRoot}");
        Console.WriteLine($"resetRepository={config.ResetRepository}, preserveLibrary={config.PreserveUnityLibraryOnReset}");
    }

    private static void Save(BuildConfig config, string fullPath)
    {
        ConfigFileWriter.Save(fullPath, config);
    }

    private static string AskString(string label, string currentValue)
    {
        Console.WriteLine($"{label} 当前值: {Display(currentValue)}");
        Console.Write($"{label} 新值，直接回车保持不变，输入 CLEAR 清空: ");
        string value = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(value))
        {
            return currentValue;
        }

        return value.Equals("CLEAR", StringComparison.OrdinalIgnoreCase) ? "" : value;
    }

    private static string AskBuildNumber(string currentValue, bool autoIncrementBuildNumber)
    {
        while (true)
        {
            string value = AskString("Build Number", currentValue);
            if (!autoIncrementBuildNumber || CanAutoIncrementBuildNumber(value))
            {
                return value;
            }

            Console.WriteLine("Auto Increment Build Number 已开启，Build Number 必须是纯数字，例如 1、2、100。");
        }
    }

    private static string AskRequiredWithDefault(string label, string currentValue)
    {
        while (true)
        {
            string value = ConsolePrompts.AskOptional(label, currentValue);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            Console.WriteLine("这个值不能为空。");
        }
    }

    private static string AskRepositoryUrl(string currentValue)
    {
        while (true)
        {
            string value = AskRequiredWithDefault("Git 仓库地址", currentValue);
            string normalized = ConfigValueNormalizer.NormalizeRepositoryUrl(value);
            if (normalized != value.Trim())
            {
                Console.WriteLine($"已整理为: {normalized}");
            }

            if (normalized.Any(char.IsWhiteSpace) || normalized.Contains('[') || normalized.Contains(']'))
            {
                Console.WriteLine("仓库地址格式不正确，请填写 git clone 可直接使用的地址。");
                continue;
            }

            return normalized;
        }
    }

    private static string AskBundleIdentifier(string currentValue)
    {
        while (true)
        {
            string value = AskRequiredWithDefault("Bundle Identifier", currentValue);
            if (Regex.IsMatch(value, @"^[A-Za-z0-9][A-Za-z0-9.-]+\.[A-Za-z0-9.-]+$"))
            {
                return value;
            }

            Console.WriteLine("Bundle Identifier 通常像 com.company.game，至少包含一个点，且不要包含空格或中文。");
        }
    }

    private static string AskAppleTeamId(string currentValue)
    {
        while (true)
        {
            string value = AskRequiredWithDefault("Apple Developer Team ID", currentValue).ToUpperInvariant();
            if (Regex.IsMatch(value, @"^[A-Z0-9]{10}$"))
            {
                return value;
            }

            Console.WriteLine("Team ID 必须是 10 位字母数字，不是公司名。");
        }
    }

    private static string AskVersion(string label, string currentValue, bool allowEmpty)
    {
        while (true)
        {
            Console.Write(string.IsNullOrEmpty(currentValue)
                ? $"{label}: "
                : $"{label} [{currentValue}]，直接回车保持不变，输入 CLEAR 清空: ");
            string value = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(value))
            {
                return currentValue;
            }

            if (allowEmpty && value.Equals("CLEAR", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            if (Version.TryParse(value, out _))
            {
                return value.Trim();
            }

            Console.WriteLine("请输入版本号格式，例如 13.0、14.0、1.2.3。");
        }
    }

    private static string AskUnityProjectRelativePath(string currentValue)
    {
        string value = ConsolePrompts.AskOptional("Unity 工程相对仓库根目录路径", Default(currentValue, "."));
        value = value.Trim().Trim('/', '\\');
        return string.IsNullOrWhiteSpace(value) ? "." : value;
    }

    private static string Display(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(空)" : value;
    }

    private static string BoolText(bool value)
    {
        return value ? "true" : "false";
    }

    private static string Default(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static bool CanAutoIncrementBuildNumber(string buildNumber)
    {
        string value = buildNumber.Trim();
        return string.IsNullOrWhiteSpace(value) || value.All(char.IsDigit);
    }

    private sealed record ConfigField(
        int Number,
        string Group,
        string Label,
        Func<BuildConfig, string> Read,
        Func<BuildConfig, bool> Edit);
}

