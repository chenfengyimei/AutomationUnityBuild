namespace BuildServer.Services;

public sealed record AutomationCommand(string FileName, List<string> PrefixArgs, string WorkingDirectory);

public static class AutomationToolLocator
{
    public static AutomationCommand Locate(BuildServerOptions options, IWebHostEnvironment environment)
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

        throw new FileNotFoundException(
            "找不到 AutomationUnityBuildIOS 打包工具。请设置 BUILD_SERVER_AUTOMATION_EXE 或 BUILD_SERVER_AUTOMATION_DLL。");
    }
}
