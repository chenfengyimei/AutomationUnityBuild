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
    string AabOutputPath,
    string TiktokWebglOutputDirectory)
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
        string tiktokWebglOutputDirectory = ResolvePath(config.TiktokWebglOutputDirectory, artifactsRunRoot, "TiktokWebGL");

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
            aabOutputPath,
            tiktokWebglOutputDirectory);
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

        DirectoryInfo? latest = FindLatestUnityEditorDirectory("/Applications/Unity/Hub/Editor");
        if (latest is not null)
        {
            return Path.Combine(latest.FullName, "Unity.app", "Contents", "MacOS", "Unity");
        }

        return "";
    }

    private static string ResolveWindowsUnityExecutable(string? unityVersion)
    {
        string[] searchRoots = GetWindowsUnityEditorSearchRoots();

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

            return searchRoots.Length == 0
                ? ""
                : Path.Combine(searchRoots[0], unityVersion, "Editor", "Unity.exe");
        }

        foreach (string root in searchRoots)
        {
            DirectoryInfo? latest = FindLatestUnityEditorDirectory(root);
            if (latest is not null)
            {
                return Path.Combine(latest.FullName, "Editor", "Unity.exe");
            }
        }

        return "";
    }

    internal static DirectoryInfo? FindLatestUnityEditorDirectory(string editorRoot)
    {
        if (!Directory.Exists(editorRoot))
        {
            return null;
        }

        return new DirectoryInfo(editorRoot)
            .EnumerateDirectories()
            .OrderByDescending(directory => directory.Name, Comparer<string>.Create(CompareUnityVersionNames))
            .FirstOrDefault();
    }

    internal static int CompareUnityVersionNames(string? left, string? right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        int[] leftNumbers = ExtractVersionNumbers(left);
        int[] rightNumbers = ExtractVersionNumbers(right);
        int length = Math.Max(leftNumbers.Length, rightNumbers.Length);
        for (int index = 0; index < length; index++)
        {
            int leftValue = index < leftNumbers.Length ? leftNumbers[index] : 0;
            int rightValue = index < rightNumbers.Length ? rightNumbers[index] : 0;
            int comparison = leftValue.CompareTo(rightValue);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetWindowsUnityEditorSearchRoots()
    {
        string[] programRoots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetEnvironmentVariable("ProgramW6432") ?? ""
        ];

        return programRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(root => Path.Combine(root, "Unity", "Hub", "Editor"))
            .ToArray();
    }

    private static int[] ExtractVersionNumbers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        List<int> numbers = [];
        int current = 0;
        bool readingNumber = false;
        bool overflow = false;

        foreach (char character in value)
        {
            if (char.IsDigit(character))
            {
                int digit = character - '0';
                readingNumber = true;
                if (current > (int.MaxValue - digit) / 10)
                {
                    overflow = true;
                    current = int.MaxValue;
                }
                else if (!overflow)
                {
                    current = (current * 10) + digit;
                }
                continue;
            }

            if (readingNumber)
            {
                numbers.Add(current);
                current = 0;
                readingNumber = false;
                overflow = false;
            }
        }

        if (readingNumber)
        {
            numbers.Add(current);
        }

        return numbers.ToArray();
    }
}
