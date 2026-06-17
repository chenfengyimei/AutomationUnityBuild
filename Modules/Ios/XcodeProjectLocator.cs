namespace AutomationUnityBuildIOS;

internal sealed class XcodeProjectLocator(BuildRunContext context)
{
    private BuildConfig _config => context.Config;
    private BuildPaths _paths => context.Paths;

    public string? Find()
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
}
