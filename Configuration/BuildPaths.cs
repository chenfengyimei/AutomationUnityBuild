namespace AutomationUnityBuildIOS;

internal sealed record BuildPaths(
    string RunId,
    string WorkspaceRoot,
    string RepositoryRoot,
    string UnityProjectRoot,
    string UnityExecutable,
    string ArtifactsRoot,
    string ArtifactsRunRoot,
    string XcodeOutputDirectory,
    string ArchivePath,
    string ExportPath,
    string LogsDirectory,
    string AutomationLogPath,
    string UnityLogPath,
    string UnityProcessLogPath,
    string UnityBuildMetadataPath,
    string ConfigSnapshotPath,
    string XcodeArchiveLogPath,
    string XcodeExportLogPath,
    string ExportOptionsPlistPath,
    string AndroidOutputDirectory,
    string ApkOutputPath,
    string AabOutputPath)
{
    public static BuildPaths Create(BuildConfig config)
    {
        string runId = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
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
        string androidOutputDirectory = ResolvePath(config.AndroidOutputDirectory, artifactsRunRoot, "Android");
        string productFileName = SafeFileName(
            !string.IsNullOrWhiteSpace(config.ProductName)
                ? config.ProductName
                : ProjectDirectoryName(config));
        string apkOutputPath = ResolvePath(config.ApkOutputPath, androidOutputDirectory, $"{productFileName}.apk");
        string aabOutputPath = ResolvePath(config.AabOutputPath, androidOutputDirectory, $"{productFileName}.aab");

        return new BuildPaths(
            runId,
            workspaceRoot,
            repositoryRoot,
            unityProjectRoot,
            ResolveUnityExecutable(config),
            artifactsRoot,
            artifactsRunRoot,
            xcodeOutputDirectory,
            archivePath,
            exportPath,
            logsDirectory,
            Path.Combine(logsDirectory, "automation.log"),
            Path.Combine(logsDirectory, "unity-editor.log"),
            Path.Combine(logsDirectory, "unity-process.log"),
            Path.Combine(logsDirectory, "unity-build-metadata.json"),
            Path.Combine(logsDirectory, "build-config-snapshot.json"),
            Path.Combine(logsDirectory, "xcode-archive.log"),
            Path.Combine(logsDirectory, "xcode-export.log"),
            exportOptionsPlistPath,
            androidOutputDirectory,
            apkOutputPath,
            aabOutputPath);
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

    private static string SafeFileName(string value)
    {
        string sanitized = string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "UnityGame" : sanitized;
    }

    private static string ResolveUnityExecutable(BuildConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.UnityExecutablePath))
        {
            return Path.GetFullPath(PathTools.ExpandHome(config.UnityExecutablePath));
        }

        if (OperatingSystem.IsWindows())
        {
            return ResolveWindowsUnityExecutable(config.UnityVersion);
        }

        return ResolveMacUnityExecutable(config.UnityVersion);
    }

    private static string ResolveMacUnityExecutable(string? unityVersion)
    {
        if (!string.IsNullOrWhiteSpace(unityVersion))
        {
            return $"/Applications/Unity/Hub/Editor/{unityVersion}/Unity.app/Contents/MacOS/Unity";
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

        return "";
    }

    private static string ResolveWindowsUnityExecutable(string? unityVersion)
    {
        string[] searchRoots =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Unity", "Hub", "Editor"),
        ];

        if (!string.IsNullOrWhiteSpace(unityVersion))
        {
            foreach (string root in searchRoots)
            {
                string exePath = Path.Combine(root, unityVersion, "Editor", "Unity.exe");
                if (File.Exists(exePath))
                {
                    return exePath;
                }
            }

            return Path.Combine(searchRoots[0], unityVersion, "Editor", "Unity.exe");
        }

        foreach (string root in searchRoots)
        {
            if (Directory.Exists(root))
            {
                DirectoryInfo? latest = new DirectoryInfo(root)
                    .EnumerateDirectories()
                    .OrderByDescending(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (latest is not null)
                {
                    return Path.Combine(latest.FullName, "Editor", "Unity.exe");
                }
            }
        }

        return "";
    }
}
