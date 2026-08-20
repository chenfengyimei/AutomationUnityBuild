namespace BuildServer.Services;

public sealed record AutomationCommand(string FileName, List<string> PrefixArgs, string WorkingDirectory);

public sealed record CliCandidate(string Path, string Type, bool Exists);

public static class AutomationToolLocator
{
    public static AutomationCommand Locate(BuildServerOptions options, IWebHostEnvironment environment)
    {
        return TryLocate(options, environment)
            ?? throw new FileNotFoundException(
                "找不到 AutomationUnityBuildIOS 打包工具。请设置 BUILD_SERVER_AUTOMATION_EXE 或 BUILD_SERVER_AUTOMATION_DLL，或在系统设置中手动指定路径。");
    }

    public static AutomationCommand? TryLocate(BuildServerOptions options, IWebHostEnvironment environment)
    {
        string workingDirectory = string.IsNullOrWhiteSpace(options.AutomationWorkingDirectory)
            ? environment.ContentRootPath
            : options.AutomationWorkingDirectory;

        if (!string.IsNullOrWhiteSpace(options.AutomationExecutablePath))
        {
            return new AutomationCommand(options.AutomationExecutablePath, [], workingDirectory);
        }

        if (!string.IsNullOrWhiteSpace(options.AutomationDllPath))
        {
            return new AutomationCommand("dotnet", [options.AutomationDllPath], workingDirectory);
        }

        string contentRoot = environment.ContentRootPath;
        string[] candidates =
        [
            Path.Combine(contentRoot, "AutomationUnityBuildIOS.exe"),
            Path.Combine(contentRoot, "AutomationUnityBuildIOS"),
            Path.GetFullPath(Path.Combine(contentRoot, "..", "bin", "Verify", "AutomationUnityBuildIOS.dll")),
            Path.GetFullPath(Path.Combine(contentRoot, "..", "bin", "Debug", "net8.0", "AutomationUnityBuildIOS.dll")),
            Path.GetFullPath(Path.Combine(contentRoot, "..", "AutomationUnityBuildIOS.dll"))
        ];

        foreach (string candidate in candidates)
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            return candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? new AutomationCommand("dotnet", [candidate], Path.GetDirectoryName(candidate) ?? workingDirectory)
                : new AutomationCommand(candidate, [], Path.GetDirectoryName(candidate) ?? workingDirectory);
        }

        return null;
    }

    public static List<CliCandidate> DetectAllCandidates(BuildServerOptions options, IWebHostEnvironment environment)
    {
        var results = new List<CliCandidate>();

        if (!string.IsNullOrWhiteSpace(options.AutomationExecutablePath))
        {
            results.Add(new CliCandidate(options.AutomationExecutablePath, "env-exe", File.Exists(options.AutomationExecutablePath)));
        }

        if (!string.IsNullOrWhiteSpace(options.AutomationDllPath))
        {
            results.Add(new CliCandidate(options.AutomationDllPath, "env-dll", File.Exists(options.AutomationDllPath)));
        }

        string contentRoot = environment.ContentRootPath;
        string[] searchPaths =
        [
            Path.Combine(contentRoot, "AutomationUnityBuildIOS.exe"),
            Path.Combine(contentRoot, "AutomationUnityBuildIOS"),
            Path.GetFullPath(Path.Combine(contentRoot, "..", "bin", "Verify", "AutomationUnityBuildIOS.dll")),
            Path.GetFullPath(Path.Combine(contentRoot, "..", "bin", "Debug", "net8.0", "AutomationUnityBuildIOS.dll")),
            Path.GetFullPath(Path.Combine(contentRoot, "..", "AutomationUnityBuildIOS.dll"))
        ];

        string[] typeLabels =
        [
            "同目录 exe",
            "同目录 (无扩展名)",
            "上级 bin/Verify DLL",
            "上级 bin/Debug DLL",
            "上级目录 DLL"
        ];

        for (int i = 0; i < searchPaths.Length; i++)
        {
            string path = searchPaths[i];
            bool exists = File.Exists(path);
            string type = typeLabels.Length > i ? typeLabels[i] : "auto-detect";

            if (!results.Any(r => BuildServerPathSafety.PathsEqual(r.Path, path)))
            {
                results.Add(new CliCandidate(path, type, exists));
            }
        }

        return results;
    }

    public static AutomationCommand? TryLocateWithSettings(
        AutomationToolSettingsRecord? settings,
        BuildServerOptions options,
        IWebHostEnvironment environment)
    {
        if (settings is not null &&
            string.Equals(settings.Mode, "manual", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(settings.ManualPath))
        {
            string manualPath = settings.ManualPath.Trim();
            string workingDirectory = string.IsNullOrWhiteSpace(options.AutomationWorkingDirectory)
                ? environment.ContentRootPath
                : options.AutomationWorkingDirectory;

            if (manualPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(manualPath))
                {
                    return new AutomationCommand("dotnet", [manualPath], Path.GetDirectoryName(manualPath) ?? workingDirectory);
                }
            }
            else
            {
                if (File.Exists(manualPath))
                {
                    return new AutomationCommand(manualPath, [], Path.GetDirectoryName(manualPath) ?? workingDirectory);
                }
            }
        }

        return TryLocate(options, environment);
    }
}
