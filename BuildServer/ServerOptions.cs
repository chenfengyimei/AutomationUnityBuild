namespace BuildServer;

public sealed class BuildServerOptions
{
    public string DataRoot { get; set; } = "";
    public string AutomationExecutablePath { get; set; } = "";
    public string AutomationDllPath { get; set; } = "";
    public string AutomationWorkingDirectory { get; set; } = "";
    public string GatewayToken { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public List<string> AllowedOrigins { get; set; } = [];
    public List<string> AllowedWorkspaceRoots { get; set; } = [];
    public List<string> AllowedArtifactsRoots { get; set; } = [];
    public List<string> AllowedConfigRoots { get; set; } = [];
    public List<string> AllowedRepositoryHosts { get; set; } = [];
    public List<string> NodePlatforms { get; set; } = [];
    public string WorkerName { get; set; } = "";
    public int RetentionDays { get; set; } = 30;
    public long MaxArtifactBytes { get; set; } = 200L * 1024 * 1024 * 1024;
    public int BuildTimeoutMinutes { get; set; } = 240;
    public int MaxSseConnectionsPerUser { get; set; } = 5;
    public int SessionCleanupIntervalMinutes { get; set; } = 10;

    public bool ReverseGatewayEnabled { get; set; }
    public string ReverseGatewayUrl { get; set; } = "";
    public string ReverseEnrollmentToken { get; set; } = "";
    public string ReverseNodeName { get; set; } = "";
    public string ReverseCredentialPath { get; set; } = "";
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
        options.GatewayToken = Env("BUILD_SERVER_GATEWAY_TOKEN", options.GatewayToken);
        options.PublicBaseUrl = Env("BUILD_SERVER_PUBLIC_BASE_URL", options.PublicBaseUrl);
        OverrideListFromEnv(options.AllowedOrigins, "BUILD_SERVER_ALLOWED_ORIGINS");
        OverrideListFromEnv(options.AllowedWorkspaceRoots, "BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS");
        OverrideListFromEnv(options.AllowedArtifactsRoots, "BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS");
        OverrideListFromEnv(options.AllowedConfigRoots, "BUILD_SERVER_ALLOWED_CONFIG_ROOTS");
        OverrideListFromEnv(options.AllowedRepositoryHosts, "BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS");
        OverrideListFromEnv(options.NodePlatforms, "BUILD_SERVER_NODE_PLATFORMS");
        options.WorkerName = Env("BUILD_SERVER_WORKER_NAME", options.WorkerName);
        options.BuildTimeoutMinutes = EnvInt("BUILD_SERVER_BUILD_TIMEOUT_MINUTES", options.BuildTimeoutMinutes);
        options.MaxSseConnectionsPerUser = EnvInt("BUILD_SERVER_MAX_SSE_CONNECTIONS_PER_USER", options.MaxSseConnectionsPerUser);
        options.SessionCleanupIntervalMinutes = EnvInt("BUILD_SERVER_SESSION_CLEANUP_INTERVAL_MINUTES", options.SessionCleanupIntervalMinutes);

        options.ReverseGatewayEnabled = EnvBool("BUILD_SERVER_REVERSE_GATEWAY_ENABLED", options.ReverseGatewayEnabled);
        options.ReverseGatewayUrl = Env("BUILD_SERVER_REVERSE_GATEWAY_URL", options.ReverseGatewayUrl);
        options.ReverseEnrollmentToken = Env("BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN", options.ReverseEnrollmentToken);
        options.ReverseNodeName = Env("BUILD_SERVER_REVERSE_NODE_NAME", options.ReverseNodeName);
        options.ReverseCredentialPath = Env("BUILD_SERVER_REVERSE_CREDENTIAL_PATH", options.ReverseCredentialPath);

        if (string.IsNullOrWhiteSpace(options.DataRoot))
        {
            options.DataRoot = Path.Combine(environment.ContentRootPath, "buildserver-data");
        }

        if (string.IsNullOrWhiteSpace(options.WorkerName))
        {
            options.WorkerName = Environment.MachineName;
        }

        options.DataRoot = ResolvePath(options.DataRoot, environment.ContentRootPath);
        if (BuildServerPathSafety.IsFilesystemRoot(options.DataRoot))
        {
            throw new InvalidOperationException($"BUILD_SERVER_DATA_ROOT 不能指向磁盘根目录: {options.DataRoot}");
        }
        options.AutomationExecutablePath = ExpandOptionalPath(options.AutomationExecutablePath, environment.ContentRootPath);
        options.AutomationDllPath = ExpandOptionalPath(options.AutomationDllPath, environment.ContentRootPath);
        options.AutomationWorkingDirectory = ExpandOptionalPath(options.AutomationWorkingDirectory, environment.ContentRootPath);

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

        options.AllowedWorkspaceRoots = NormalizeRootList(options.AllowedWorkspaceRoots, environment.ContentRootPath);
        options.AllowedArtifactsRoots = NormalizeRootList(options.AllowedArtifactsRoots, environment.ContentRootPath);
        options.AllowedConfigRoots = NormalizeRootList(options.AllowedConfigRoots, environment.ContentRootPath);
        RejectFilesystemRoots(options.AllowedWorkspaceRoots, "BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS");
        RejectFilesystemRoots(options.AllowedArtifactsRoots, "BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS");
        RejectFilesystemRoots(options.AllowedConfigRoots, "BUILD_SERVER_ALLOWED_CONFIG_ROOTS");
        options.AllowedRepositoryHosts = options.AllowedRepositoryHosts
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        options.NodePlatforms = NormalizeNodePlatforms(options.NodePlatforms);
        options.BuildTimeoutMinutes = Math.Max(1, options.BuildTimeoutMinutes);
        options.MaxSseConnectionsPerUser = Math.Max(1, options.MaxSseConnectionsPerUser);
        options.SessionCleanupIntervalMinutes = Math.Max(1, options.SessionCleanupIntervalMinutes);
        return options;
    }

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

    private static string ExpandOptionalPath(string path, string contentRootPath)
    {
        return string.IsNullOrWhiteSpace(path) ? "" : ResolvePath(path, contentRootPath);
    }

    private static string Env(string name, string fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static bool EnvBool(string name, bool fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int EnvInt(string name, int fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out int parsed) ? parsed : fallback;
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

    private static List<string> NormalizeRootList(IEnumerable<string> roots, string contentRootPath)
    {
        return roots
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => ResolvePath(value.Trim(), contentRootPath))
            .Distinct(BuildServerPathSafety.Comparer)
            .ToList();
    }

    private static void RejectFilesystemRoots(IEnumerable<string> roots, string settingName)
    {
        foreach (string root in roots)
        {
            if (BuildServerPathSafety.IsFilesystemRoot(root))
            {
                throw new InvalidOperationException($"{settingName} 不能包含磁盘根目录: {root}");
            }
        }
    }

    private static List<string> NormalizeNodePlatforms(IEnumerable<string> platforms)
    {
        List<string> result = platforms
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => BuildPlatforms.Normalize(value))
            .Where(BuildPlatforms.IsKnown)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (result.Count > 0)
        {
            return result;
        }

        if (OperatingSystem.IsMacOS())
        {
            return [BuildPlatforms.Ios, BuildPlatforms.Android];
        }

        if (OperatingSystem.IsWindows())
        {
            return [BuildPlatforms.Android];
        }

        return [];
    }

    private static string ResolvePath(string path, string contentRootPath)
    {
        string expanded = ExpandHome(path);
        return Path.GetFullPath(Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(contentRootPath, expanded));
    }
}
