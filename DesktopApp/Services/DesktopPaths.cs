using AutomationUnityBuildIOS;

namespace DesktopApp.Services;

public static class DesktopPaths
{
    public const string DataRootEnvironmentVariable = "AUTOMATION_UNITY_DESKTOP_DATA_ROOT";

    public static string DataRoot { get; } = ResolveDataRoot();
    public static string ProfilesDirectory => Path.Combine(DataRoot, "profiles");
    public static string ConfigsDirectory => Path.Combine(DataRoot, "configs");
    public static string ServerSettingsPath => Path.Combine(ProfilesDirectory, "server-settings.json");
    public static string BuildServerPathSettingsPath => Path.Combine(ProfilesDirectory, "buildserver-path.json");
    public static string EmailSettingsPath => Path.Combine(ProfilesDirectory, "email-settings.json");

    public static void Initialize()
    {
        if (PathSafety.IsFilesystemRoot(DataRoot))
        {
            throw new IOException($"{DataRootEnvironmentVariable} 不能指向文件系统根目录: {DataRoot}");
        }

        EnsureManagedDirectory(ProfilesDirectory);
        EnsureManagedDirectory(ConfigsDirectory);

        string[] legacyRoots = [Environment.CurrentDirectory, AppContext.BaseDirectory];
        foreach (string legacyRoot in legacyRoots.Distinct(PathComparer()))
        {
            MigrateDirectory(Path.Combine(legacyRoot, "profiles"), ProfilesDirectory);
            MigrateDirectory(Path.Combine(legacyRoot, "configs"), ConfigsDirectory);
        }

        string legacyEmailSettings = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopApp",
            "email-settings.json");
        MigrateFile(legacyEmailSettings, EmailSettingsPath);
    }

    public static bool IsPortableFileName(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value is not "." and not ".." &&
               value == Path.GetFileName(value) &&
               !value.EndsWith('.') &&
               !value.EndsWith(' ') &&
               value.IndexOfAny(['/', '\\', '<', '>', ':', '"', '|', '?', '*']) < 0 &&
               value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               !value.Any(char.IsControl) &&
               !IsReservedWindowsDeviceName(value);
    }

    public static string MakePortableFileName(string value, string fallback)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars()
            .Concat(['/', '\\', '<', '>', ':', '"', '|', '?', '*'])
            .Concat(Enumerable.Range(0, 32).Select(number => (char)number))
            .Distinct()
            .ToArray();
        string result = string.Join("_", value.Split(invalidCharacters, StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(result) || result is "." or "..")
        {
            return fallback;
        }

        return IsReservedWindowsDeviceName(result) ? $"_{result}" : result;
    }

    private static string ResolveDataRoot()
    {
        string? configured = Environment.GetEnvironmentVariable(DataRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string expanded = Environment.ExpandEnvironmentVariables(PathTools.ExpandHome(configured.Trim()));
            return Path.GetFullPath(expanded);
        }

        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
        {
            localData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share");
        }

        return Path.GetFullPath(Path.Combine(localData, "AutomationUnityBuildIOS", "DesktopApp"));
    }

    private static void MigrateDirectory(string sourceDirectory, string targetDirectory)
    {
        try
        {
            string source = Path.GetFullPath(sourceDirectory);
            string target = Path.GetFullPath(targetDirectory);
            if (source.Equals(target, PathComparison()) || !Directory.Exists(source))
            {
                return;
            }

            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            foreach (string sourceFile in Directory.EnumerateFiles(source, "*", enumerationOptions))
            {
                string relativePath = Path.GetRelativePath(source, sourceFile);
                string targetFile = Path.GetFullPath(Path.Combine(target, relativePath));
                string targetRoot = target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                    Path.DirectorySeparatorChar;
                if (!targetFile.StartsWith(targetRoot, PathComparison()) ||
                    !PathSafety.IsSameOrChildPathWithoutReparsePoints(targetFile, DataRoot) ||
                    File.Exists(targetFile))
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(sourceFile, targetFile, overwrite: false);
            }
        }
        catch
        {
            // 旧版目录只做尽力迁移；失败时保留原文件，不影响应用启动。
        }
    }

    private static void MigrateFile(string sourcePath, string targetPath)
    {
        try
        {
            string source = Path.GetFullPath(sourcePath);
            string target = Path.GetFullPath(targetPath);
            if (source.Equals(target, PathComparison()) ||
                !File.Exists(source) ||
                File.Exists(target) ||
                (File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: false);
        }
        catch
        {
            // 同上：迁移失败不能阻止用户打开应用。
        }
    }

    private static void EnsureManagedDirectory(string path)
    {
        if (!PathSafety.IsSameOrChildPathWithoutReparsePoints(path, DataRoot))
        {
            throw new IOException($"DesktopApp 数据目录包含符号链接或 Junction，已拒绝写入: {path}");
        }

        Directory.CreateDirectory(path);
        if (!PathSafety.IsSameOrChildPathWithoutReparsePoints(path, DataRoot))
        {
            throw new IOException($"DesktopApp 数据目录创建后安全校验失败: {path}");
        }
    }

    private static StringComparer PathComparer()
    {
        return OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    }

    private static StringComparison PathComparison()
    {
        return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    private static bool IsReservedWindowsDeviceName(string value)
    {
        string stem = value.Split('.')[0];
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
               stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
               (stem.Length == 4 &&
                (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                stem[3] is >= '1' and <= '9');
    }
}
