namespace BuildServer;

public sealed class BuildServerOptions
{
    public string DataRoot { get; set; } = "";
    public string AutomationExecutablePath { get; set; } = "";
    public string AutomationDllPath { get; set; } = "";
    public string AutomationWorkingDirectory { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public List<string> AllowedOrigins { get; set; } = [];
    public List<string> AllowedWorkspaceRoots { get; set; } = [];
    public List<string> AllowedArtifactsRoots { get; set; } = [];
    public List<string> AllowedConfigRoots { get; set; } = [];
    public List<string> AllowedRepositoryHosts { get; set; } = [];
    public string WorkerName { get; set; } = "";
    public int RetentionDays { get; set; } = 30;
    public long MaxArtifactBytes { get; set; } = 200L * 1024 * 1024 * 1024;
}

public static class BuildServerEnvironment
{
    public static BuildServerOptions Load(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var options = new BuildServerOptions();
        configuration.GetSection("BuildServer").Bind(options);

        options.DataRoot = Env("BUILD_SERVER_DATA_ROOT", options.DataRoot);
        options.AutomationExecutablePath = Env("BUILD_SERVER_AUTOMATION_EXE", options.AutomationExecutablePath);
        options.AutomationDllPath = Env("BUILD_SERVER_AUTOMATION_DLL", options.AutomationDllPath);
        options.AutomationWorkingDirectory = Env("BUILD_SERVER_AUTOMATION_CWD", options.AutomationWorkingDirectory);
        options.PublicBaseUrl = Env("BUILD_SERVER_PUBLIC_BASE_URL", options.PublicBaseUrl);
        OverrideListFromEnv(options.AllowedOrigins, "BUILD_SERVER_ALLOWED_ORIGINS");
        OverrideListFromEnv(options.AllowedWorkspaceRoots, "BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS");
        OverrideListFromEnv(options.AllowedArtifactsRoots, "BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS");
        OverrideListFromEnv(options.AllowedConfigRoots, "BUILD_SERVER_ALLOWED_CONFIG_ROOTS");
        OverrideListFromEnv(options.AllowedRepositoryHosts, "BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS");
        options.WorkerName = Env("BUILD_SERVER_WORKER_NAME", options.WorkerName);

        if (string.IsNullOrWhiteSpace(options.DataRoot))
        {
            options.DataRoot = Path.Combine(environment.ContentRootPath, "buildserver-data");
        }

        if (string.IsNullOrWhiteSpace(options.WorkerName))
        {
            options.WorkerName = Environment.MachineName;
        }

        options.DataRoot = Path.GetFullPath(ExpandHome(options.DataRoot));
        options.AutomationExecutablePath = ExpandOptionalPath(options.AutomationExecutablePath);
        options.AutomationDllPath = ExpandOptionalPath(options.AutomationDllPath);
        options.AutomationWorkingDirectory = ExpandOptionalPath(options.AutomationWorkingDirectory);

        if (options.AllowedWorkspaceRoots.Count == 0)
        {
            options.AllowedWorkspaceRoots.Add("~/UnityBuildWorkspace");
        }

        if (options.AllowedArtifactsRoots.Count == 0)
        {
            options.AllowedArtifactsRoots.Add("~/UnityBuildArtifacts");
        }

        if (options.AllowedConfigRoots.Count == 0)
        {
            options.AllowedConfigRoots.Add(Path.Combine(options.DataRoot, "configs"));
            options.AllowedConfigRoots.Add(Path.Combine(environment.ContentRootPath, "configs"));
        }

        options.AllowedWorkspaceRoots = NormalizeRootList(options.AllowedWorkspaceRoots);
        options.AllowedArtifactsRoots = NormalizeRootList(options.AllowedArtifactsRoots);
        options.AllowedConfigRoots = NormalizeRootList(options.AllowedConfigRoots);
        options.AllowedRepositoryHosts = options.AllowedRepositoryHosts
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return options;
    }

    public static string ExpandHome(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        }

        return path;
    }

    private static string ExpandOptionalPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(ExpandHome(path));
    }

    private static string Env(string name, string fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static void OverrideListFromEnv(List<string> target, string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        target.Clear();
        target.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static List<string> NormalizeRootList(IEnumerable<string> roots)
    {
        return roots
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetFullPath(ExpandHome(value.Trim())))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
