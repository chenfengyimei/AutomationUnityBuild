namespace AutomationUnityBuildIOS;

internal static class PathSafety
{
    public static bool IsSameOrChildPath(string path, string allowedRoot)
    {
        string normalizedPath = NormalizeDirectoryPath(path);
        string normalizedRoot = NormalizeDirectoryPath(allowedRoot);
        StringComparison comparison = PathComparison();

        return normalizedPath.Equals(normalizedRoot, comparison) ||
               normalizedPath.StartsWith(normalizedRoot, comparison);
    }

    public static bool IsStrictChildPath(string path, string allowedRoot)
    {
        string normalizedPath = NormalizeDirectoryPath(path);
        string normalizedRoot = NormalizeDirectoryPath(allowedRoot);
        StringComparison comparison = PathComparison();

        return normalizedPath.Length > normalizedRoot.Length &&
               normalizedPath.StartsWith(normalizedRoot, comparison);
    }

    public static string NormalizeDirectoryPath(string path)
    {
        string fullPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath + Path.DirectorySeparatorChar;
    }

    public static string NormalizePath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.IsNullOrEmpty(trimmed) ? fullPath : trimmed;
    }

    public static bool IsFilesystemRoot(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        string normalized = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalized.Equals(normalizedRoot, PathComparison()) ||
               (normalized.Length == 0 && normalizedRoot.Length == 0);
    }

    private static StringComparison PathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
