namespace BuildServer;

public static class BuildServerPathSafety
{
    public static StringComparer Comparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static bool PathsEqual(string left, string right)
    {
        return string.Equals(NormalizePath(left), NormalizePath(right), Comparison);
    }

    public static bool IsSameOrChild(string path, string root)
    {
        string normalizedPath = NormalizeDirectory(path);
        string normalizedRoot = NormalizeDirectory(root);
        return normalizedPath.Equals(normalizedRoot, Comparison) ||
               normalizedPath.StartsWith(normalizedRoot, Comparison);
    }

    public static bool IsSafeSameOrChild(string path, string root)
    {
        return IsSameOrChild(path, root) &&
               !HasReparsePointBelowRoot(path, root);
    }

    public static bool IsFilesystemRoot(string path)
    {
        string fullPath = Path.GetFullPath(BuildServerEnvironment.ExpandHome(path));
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return true;
        }

        string normalized = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalized.Length == 0 || normalized.Equals(normalizedRoot, Comparison);
    }

    public static bool IsAbsolutePathFromAnyPlatform(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string value = path.Trim();
        if (value[0] is '/' or '\\')
        {
            return true;
        }

        return IsWindowsAbsolutePath(value);
    }

    public static bool IsWindowsAbsolutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string value = path.Trim();
        if (value[0] == '\\')
        {
            return true;
        }

        return value.Length >= 3 &&
               char.IsAsciiLetter(value[0]) &&
               value[1] == ':' &&
               value[2] is '/' or '\\';
    }

    public static bool IsPortableFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value is "." or ".." ||
            value != Path.GetFileName(value) ||
            value.EndsWith('.') ||
            value.EndsWith(' ') ||
            value.IndexOfAny(['/', '\\', '<', '>', ':', '"', '|', '?', '*']) >= 0 ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Any(char.IsControl))
        {
            return false;
        }

        string stem = value.Split('.')[0];
        return !stem.Equals("CON", StringComparison.OrdinalIgnoreCase) &&
               !stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) &&
               !stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) &&
               !stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) &&
               !(stem.Length == 4 &&
                 (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                  stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                 stem[3] is >= '1' and <= '9');
    }

    public static string NormalizePath(string path)
    {
        string fullPath = Path.GetFullPath(BuildServerEnvironment.ExpandHome(path));
        string trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrWhiteSpace(root) &&
            trimmed.Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Comparison))
        {
            return root;
        }

        return string.IsNullOrEmpty(trimmed) ? fullPath : trimmed;
    }

    private static string NormalizeDirectory(string path)
    {
        string fullPath = Path.GetFullPath(BuildServerEnvironment.ExpandHome(path))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath + Path.DirectorySeparatorChar;
    }

    private static bool HasReparsePointBelowRoot(string path, string root)
    {
        string fullPath = NormalizePath(path);
        string fullRoot = NormalizePath(root);
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
}
