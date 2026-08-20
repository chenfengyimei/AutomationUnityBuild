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

    public static bool IsSameOrChildPathWithoutReparsePoints(string path, string allowedRoot)
    {
        return IsSameOrChildPath(path, allowedRoot) &&
               !HasReparsePointBelowRoot(path, allowedRoot);
    }

    public static bool IsStrictChildPath(string path, string allowedRoot)
    {
        string normalizedPath = NormalizeDirectoryPath(path);
        string normalizedRoot = NormalizeDirectoryPath(allowedRoot);
        StringComparison comparison = PathComparison();

        return normalizedPath.Length > normalizedRoot.Length &&
               normalizedPath.StartsWith(normalizedRoot, comparison);
    }

    public static bool IsStrictChildPathWithoutReparsePoints(string path, string allowedRoot)
    {
        return IsStrictChildPath(path, allowedRoot) &&
               !HasReparsePointBelowRoot(path, allowedRoot);
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
        string? root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrWhiteSpace(root) &&
            trimmed.Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), PathComparison()))
        {
            return root;
        }

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

    private static bool HasReparsePointBelowRoot(string path, string allowedRoot)
    {
        string fullPath = NormalizePath(path);
        string fullRoot = NormalizePath(allowedRoot);
        string relativePath = Path.GetRelativePath(fullRoot, fullPath);
        if (relativePath == ".")
        {
            return false;
        }

        string current = fullRoot;
        foreach (string component in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch (IOException)
            {
                return true;
            }
        }

        return false;
    }

    private static StringComparison PathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }
}
