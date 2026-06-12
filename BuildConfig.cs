using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationUnityBuildIOS;

internal sealed class BuildConfig
{
    public string RepositoryUrl { get; set; } = "";
    public string Branch { get; set; } = "main";
    public string WorkspaceRoot { get; set; } = "~/UnityBuildWorkspace";
    public string ProjectDirectoryName { get; set; } = "";
    public string UnityProjectRelativePath { get; set; } = ".";

    public string UnityVersion { get; set; } = "";
    public string UnityExecutablePath { get; set; } = "";
    public string UnityBuildMethod { get; set; } = "BuildAutomation.IOSBuilder.Build";

    public string ArtifactsRoot { get; set; } = "~/UnityBuildArtifacts/UnityGame";
    public string XcodeOutputDirectory { get; set; } = "";
    public string ArchivePath { get; set; } = "";
    public string ExportPath { get; set; } = "";
    public string LogsDirectory { get; set; } = "";

    public string Scheme { get; set; } = "Unity-iPhone";
    public string Configuration { get; set; } = "Release";
    public string ExportMethod { get; set; } = "development";
    public string TeamId { get; set; } = "";
    public string SigningStyle { get; set; } = "automatic";
    public string ExportOptionsPlistPath { get; set; } = "";

    public string BundleIdentifier { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string BundleVersion { get; set; } = "";
    public bool SyncBundleVersionFromUnity { get; set; } = true;
    public string BuildNumber { get; set; } = "";
    public string IosDeploymentTarget { get; set; } = "";
    public bool AutoIncrementBuildNumber { get; set; } = true;

    public bool AllowProvisioningUpdates { get; set; } = true;
    public bool ResetRepository { get; set; }
    public bool PreserveUnityLibraryOnReset { get; set; } = true;
    public bool CleanXcodeOutputBeforeBuild { get; set; } = true;
    public bool UseWorkspaceIfPresent { get; set; } = true;
    public bool GenerateExportOptionsPlist { get; set; } = true;
    public bool CopyArchiveToOrganizer { get; set; } = true;

    public bool? CompileBitcode { get; set; }
    public bool? UploadSymbols { get; set; } = true;

    public Dictionary<string, string> XcodeBuildSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Environment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ProvisioningProfiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static BuildConfig Load(string configPath)
    {
        string fullPath = Path.GetFullPath(configPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"找不到配置文件 {fullPath}。可以先运行 init-config 生成模板。");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        BuildConfig? config = JsonSerializer.Deserialize<BuildConfig>(File.ReadAllText(fullPath), options);
        if (config is null)
        {
            throw new InvalidOperationException($"配置文件为空或格式不正确: {fullPath}");
        }

        config.XcodeBuildSettings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        config.Environment ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        config.ProvisioningProfiles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        config.RepositoryUrl = ConfigValueNormalizer.NormalizeRepositoryUrl(config.RepositoryUrl);
        config.IosDeploymentTarget = config.IosDeploymentTarget.Trim();
        config.Validate();
        return config;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(RepositoryUrl))
        {
            throw new InvalidOperationException("配置 repositoryUrl 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(Branch))
        {
            throw new InvalidOperationException("配置 branch 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(UnityBuildMethod))
        {
            throw new InvalidOperationException("配置 unityBuildMethod 不能为空。");
        }

        if (!string.IsNullOrWhiteSpace(TeamId) &&
            (TeamId.Length != 10 || !TeamId.All(char.IsLetterOrDigit)))
        {
            throw new InvalidOperationException("配置 teamId 必须是 10 位 Apple Developer Team ID，例如 ABCDE12345，不能填公司名。");
        }

        if (!GenerateExportOptionsPlist && string.IsNullOrWhiteSpace(ExportOptionsPlistPath))
        {
            throw new InvalidOperationException("generateExportOptionsPlist=false 时必须配置 exportOptionsPlistPath。");
        }

        if (!string.IsNullOrWhiteSpace(IosDeploymentTarget) && !Version.TryParse(IosDeploymentTarget, out _))
        {
            throw new InvalidOperationException("配置 iosDeploymentTarget 必须是版本号格式，例如 13.0 或 14.0。");
        }

        if (!SyncBundleVersionFromUnity && string.IsNullOrWhiteSpace(BundleVersion))
        {
            throw new InvalidOperationException("syncBundleVersionFromUnity=false 时必须配置 bundleVersion。");
        }
    }
}

internal sealed record BuildPaths(
    string RunId,
    string WorkspaceRoot,
    string RepositoryRoot,
    string UnityProjectRoot,
    string UnityExecutable,
    string ArtifactsRunRoot,
    string XcodeOutputDirectory,
    string ArchivePath,
    string ExportPath,
    string LogsDirectory,
    string AutomationLogPath,
    string UnityLogPath,
    string UnityProcessLogPath,
    string UnityBuildMetadataPath,
    string XcodeArchiveLogPath,
    string XcodeExportLogPath,
    string ExportOptionsPlistPath)
{
    public static BuildPaths Create(BuildConfig config)
    {
        string runId = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        string workspaceRoot = PathTools.ExpandHome(config.WorkspaceRoot);
        string repositoryRoot = Path.Combine(workspaceRoot, ProjectDirectoryName(config));
        string unityProjectRoot = Path.GetFullPath(Path.Combine(repositoryRoot, config.UnityProjectRelativePath));

        string artifactsRoot = PathTools.ExpandHome(config.ArtifactsRoot);
        string artifactsRunRoot = Path.Combine(artifactsRoot, runId);
        string xcodeOutputDirectory = ResolvePath(config.XcodeOutputDirectory, artifactsRunRoot, "XcodeProject");
        string archivePath = ResolvePath(config.ArchivePath, artifactsRunRoot, "Archive.xcarchive");
        string exportPath = ResolvePath(config.ExportPath, artifactsRunRoot, "Export");
        string logsDirectory = ResolvePath(config.LogsDirectory, artifactsRunRoot, "Logs");
        string exportOptionsPlistPath = ResolvePath(config.ExportOptionsPlistPath, artifactsRunRoot, "ExportOptions.plist");

        return new BuildPaths(
            runId,
            workspaceRoot,
            repositoryRoot,
            unityProjectRoot,
            ResolveUnityExecutable(config),
            artifactsRunRoot,
            xcodeOutputDirectory,
            archivePath,
            exportPath,
            logsDirectory,
            Path.Combine(logsDirectory, "automation.log"),
            Path.Combine(logsDirectory, "unity-editor.log"),
            Path.Combine(logsDirectory, "unity-process.log"),
            Path.Combine(logsDirectory, "unity-build-metadata.json"),
            Path.Combine(logsDirectory, "xcode-archive.log"),
            Path.Combine(logsDirectory, "xcode-export.log"),
            exportOptionsPlistPath);
    }

    private static string ProjectDirectoryName(BuildConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.ProjectDirectoryName))
        {
            return config.ProjectDirectoryName.Trim();
        }

        string url = config.RepositoryUrl.TrimEnd('/', '\\');
        string lastPart = url.Split('/', '\\', ':').Last();
        return lastPart.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? lastPart[..^4]
            : lastPart;
    }

    private static string ResolvePath(string configuredPath, string root, string fallbackName)
    {
        string path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(root, fallbackName)
            : PathTools.ExpandHome(configuredPath);

        return Path.GetFullPath(path);
    }

    private static string ResolveUnityExecutable(BuildConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.UnityExecutablePath))
        {
            return Path.GetFullPath(PathTools.ExpandHome(config.UnityExecutablePath));
        }

        if (!string.IsNullOrWhiteSpace(config.UnityVersion))
        {
            return $"/Applications/Unity/Hub/Editor/{config.UnityVersion}/Unity.app/Contents/MacOS/Unity";
        }

        string editorRoot = "/Applications/Unity/Hub/Editor";
        if (Directory.Exists(editorRoot))
        {
            DirectoryInfo? latest = new DirectoryInfo(editorRoot)
                .EnumerateDirectories()
                .OrderByDescending(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (latest is not null)
            {
                return Path.Combine(latest.FullName, "Unity.app", "Contents", "MacOS", "Unity");
            }
        }

        return "/Applications/Unity/Hub/Editor/<UnityVersion>/Unity.app/Contents/MacOS/Unity";
    }
}

internal static class PathTools
{
    public static string ExpandHome(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            string relativePath = path[2..]
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), relativePath);
        }

        return path;
    }
}
