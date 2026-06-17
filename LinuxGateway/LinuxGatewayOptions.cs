namespace LinuxGateway;

public sealed class LinuxGatewayOptions
{
    public string DataRoot { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public List<string> AllowedOrigins { get; set; } = [];

    public static LinuxGatewayOptions Load(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var options = new LinuxGatewayOptions();
        configuration.GetSection("LinuxGateway").Bind(options);

        options.DataRoot = Env("LINUX_GATEWAY_DATA_ROOT", options.DataRoot);
        options.AdminPassword = Env("LINUX_GATEWAY_ADMIN_PASSWORD", options.AdminPassword);
        options.PublicBaseUrl = Env("LINUX_GATEWAY_PUBLIC_BASE_URL", options.PublicBaseUrl);
        OverrideListFromEnv(options.AllowedOrigins, "LINUX_GATEWAY_ALLOWED_ORIGINS");

        if (string.IsNullOrWhiteSpace(options.DataRoot))
        {
            options.DataRoot = Path.Combine(environment.ContentRootPath, "linuxgateway-data");
        }

        options.DataRoot = ResolvePath(options.DataRoot, environment.ContentRootPath);
        options.AllowedOrigins = options.AllowedOrigins
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static string ResolvePath(string path, string contentRootPath)
    {
        string expanded = ExpandHome(path);
        return Path.GetFullPath(Path.IsPathRooted(expanded)
            ? expanded
            : Path.Combine(contentRootPath, expanded));
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
}
