namespace AutomationUnityBuildIOS;

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
            string relativePath = path[1..]
                .TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            if (string.IsNullOrEmpty(relativePath))
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), relativePath);
        }

        return path;
    }

    public static void EnsureParentDirectory(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            Directory.CreateDirectory(parent);
        }
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

        return value.Length >= 3 &&
               char.IsAsciiLetter(value[0]) &&
               value[1] == ':' &&
               value[2] is '/' or '\\';
    }
}
