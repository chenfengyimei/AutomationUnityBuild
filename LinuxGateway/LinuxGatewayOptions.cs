namespace LinuxGateway;

public sealed class LinuxGatewayOptions
{
    public string DataRoot { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public List<string> AllowedOrigins { get; set; } = [];
    public int JobRefreshIntervalSeconds { get; set; } = 15;
    public int MaxSseConnectionsPerUser { get; set; } = 5;
    public string UpdateRepoOwner { get; set; } = "chenfengloveyuri";
    public string UpdateRepoName { get; set; } = "automation-unity-build-ios";
    public string UpdateSource { get; set; } = "gitee";
    public string UpdateGithubRepoOwner { get; set; } = "chenfengyimei";
    public string UpdateGithubRepoName { get; set; } = "AutomationUnityBuild";

    public static LinuxGatewayOptions Load(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var options = new LinuxGatewayOptions();
        configuration.GetSection("LinuxGateway").Bind(options);

        options.DataRoot = Env("LINUX_GATEWAY_DATA_ROOT", options.DataRoot);
        options.AdminPassword = Env("LINUX_GATEWAY_ADMIN_PASSWORD", options.AdminPassword);
        options.PublicBaseUrl = Env("LINUX_GATEWAY_PUBLIC_BASE_URL", options.PublicBaseUrl);
        options.JobRefreshIntervalSeconds = EnvInt("LINUX_GATEWAY_JOB_REFRESH_INTERVAL_SECONDS", options.JobRefreshIntervalSeconds);
        options.MaxSseConnectionsPerUser = EnvInt("LINUX_GATEWAY_MAX_SSE_CONNECTIONS_PER_USER", options.MaxSseConnectionsPerUser);
        OverrideListFromEnv(options.AllowedOrigins, "LINUX_GATEWAY_ALLOWED_ORIGINS");

        options.UpdateRepoOwner = Env("LINUX_GATEWAY_UPDATE_REPO_OWNER", options.UpdateRepoOwner);
        options.UpdateRepoName = Env("LINUX_GATEWAY_UPDATE_REPO_NAME", options.UpdateRepoName);
        options.UpdateSource = Env("LINUX_GATEWAY_UPDATE_SOURCE", options.UpdateSource).Trim().ToLowerInvariant();
        options.UpdateGithubRepoOwner = Env("LINUX_GATEWAY_UPDATE_GITHUB_REPO_OWNER", options.UpdateGithubRepoOwner);
        options.UpdateGithubRepoName = Env("LINUX_GATEWAY_UPDATE_GITHUB_REPO_NAME", options.UpdateGithubRepoName);

        if (string.IsNullOrWhiteSpace(options.DataRoot))
        {
            options.DataRoot = Path.Combine(environment.ContentRootPath, "linuxgateway-data");
        }

        options.DataRoot = ResolvePath(options.DataRoot, environment.ContentRootPath);
        if (IsFilesystemRoot(options.DataRoot))
        {
            throw new InvalidOperationException($"LINUX_GATEWAY_DATA_ROOT 不能指向文件系统根目录: {options.DataRoot}");
        }
        options.AllowedOrigins = options.AllowedOrigins
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        options.JobRefreshIntervalSeconds = Math.Max(1, options.JobRefreshIntervalSeconds);
        options.MaxSseConnectionsPerUser = Math.Max(1, options.MaxSseConnectionsPerUser);
        return options;
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

    private static int EnvInt(string name, int fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, out int parsed) ? parsed : fallback;
    }

    private static string ResolvePath(string path, string contentRootPath)
    {
        string expanded = ExpandHome(path);
        return Path.GetFullPath(Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(contentRootPath, expanded));
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

    private static bool IsFilesystemRoot(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return true;
        }

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), comparison);
    }
}
