using BuildServer;
using BuildServer.Persistence;
using BuildServer.Reverse;
using BuildServer.Security;
using BuildServer.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

var contentRoot = ResolveContentRoot();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot,
    WebRootPath = Path.Combine(contentRoot, "wwwroot")
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://127.0.0.1:5088");
}

BuildServerOptions options = BuildServerEnvironment.Load(builder.Configuration, builder.Environment);
string? configuredUrls = builder.Configuration["urls"];
string? aspNetCoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<JsonDatabase>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<LoginRateLimiter>();
builder.Services.AddSingleton<BuildQueueService>();
builder.Services.AddSingleton<ArtifactScanner>();
builder.Services.AddSingleton<BuildWorkerService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<BuildWorkerService>());
builder.Services.AddHostedService<MaintenanceService>();
builder.Services.AddSingleton<AgentCredentialStore>();
builder.Services.AddSingleton<EmailNotificationService>();
builder.Services.AddSingleton<GatewayAgentService>();
builder.Services.AddSingleton<IGatewayPushChannel>(provider => provider.GetRequiredService<GatewayAgentService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<GatewayAgentService>());

var app = builder.Build();

JsonDatabase database = app.Services.GetRequiredService<JsonDatabase>();
AuthService auth = app.Services.GetRequiredService<AuthService>();
await database.InitializeAsync();
await auth.SeedDefaultsAsync();
GatewayTokenInitializer.Ensure(options, app.Logger);
app.Logger.LogInformation("BuildServer data root: {DataRoot}", options.DataRoot);
app.Logger.LogInformation("Initial admin file: {InitialAdminPath}", Path.Combine(options.DataRoot, "initial-admin.txt"));
app.Logger.LogInformation("Initial gateway token file: {InitialGatewayTokenPath}", Path.Combine(options.DataRoot, "initial-gateway-token.txt"));
app.Logger.LogInformation(
    "BuildServer URL configuration: urls={ConfiguredUrls}; ASPNETCORE_URLS={AspNetCoreUrls}; appUrls={AppUrls}",
    string.IsNullOrWhiteSpace(configuredUrls) ? "(empty)" : configuredUrls,
    string.IsNullOrWhiteSpace(aspNetCoreUrls) ? "(empty)" : aspNetCoreUrls,
    string.Join(",", app.Urls));

app.UseApiDiagnostics();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(ApiDiagnostics.WriteExceptionAsync);
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
});
app.UseBuildServerSecurity();
app.Logger.LogInformation("BuildServer HTTP middleware configured.");

string? webRoot = ResolveWebRoot(app.Environment.ContentRootPath);
if (webRoot is not null)
{
    var webFileProvider = new PhysicalFileProvider(webRoot);
    app.MapGet("/", () => Results.File(Path.Combine(webRoot, "index.html"), "text/html; charset=utf-8"));
    app.UseStaticFiles(new StaticFileOptions { FileProvider = webFileProvider });
    app.Logger.LogInformation("BuildServer static web root: {WebRoot}", webRoot);
}
else
{
    app.MapGet("/", () => Results.Problem("找不到 wwwroot/index.html。请确认发布目录里包含 wwwroot 文件夹，并从完整发布目录启动 BuildServer。"));
    app.Logger.LogWarning("BuildServer static web root was not found.");
}

ApiRoutes.Map(app);
McpEndpoint.Map(app);
GatewayEndpoint.Map(app);
GatewayAgentEndpoint.Map(app);
app.Logger.LogInformation("BuildServer API routes mapped. Starting web host...");

app.Run();

static string ResolveContentRoot()
{
    string baseDirectory = AppContext.BaseDirectory;
    string currentDirectory = Directory.GetCurrentDirectory();

    string[] directCandidates =
    [
        baseDirectory,
        currentDirectory,
        Path.Combine(currentDirectory, "BuildServer")
    ];

    foreach (string candidate in directCandidates)
    {
        if (IsBuildServerContentRoot(candidate))
        {
            return Path.GetFullPath(candidate);
        }
    }

    DirectoryInfo? directory = new(baseDirectory);
    while (directory is not null)
    {
        if (IsBuildServerContentRoot(directory.FullName))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return Path.GetFullPath(baseDirectory);
}

static string? ResolveWebRoot(string contentRootPath)
{
    string[] candidates =
    [
        Path.Combine(contentRootPath, "wwwroot"),
        Path.Combine(AppContext.BaseDirectory, "wwwroot"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")
    ];

    return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "index.html")));
}

static bool IsBuildServerContentRoot(string path)
{
    return File.Exists(Path.Combine(path, "appsettings.json")) &&
           (File.Exists(Path.Combine(path, "wwwroot", "index.html")) ||
            File.Exists(Path.Combine(path, "BuildServer.csproj")));
}
