using LinuxGateway;
using LinuxGateway.Persistence;
using LinuxGateway.Reverse;
using LinuxGateway.Security;
using LinuxGateway.Services;
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
    builder.WebHost.UseUrls("http://127.0.0.1:5090");
}

LinuxGatewayOptions options = LinuxGatewayOptions.Load(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<JsonGatewayDatabase>();
builder.Services.AddSingleton<GatewayAuthService>();
builder.Services.AddSingleton<NodeRefreshService>();
builder.Services.AddSingleton<JobRefreshService>();
builder.Services.AddHttpClient<NodeGatewayClient>(client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddHostedService(provider => provider.GetRequiredService<NodeRefreshService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<JobRefreshService>());
builder.Services.AddSingleton<ReverseNodeConnectionManager>();
builder.Services.AddSingleton<GatewayCommandStore>();
builder.Services.AddSingleton<GatewayCommandDispatcher>();
builder.Services.AddSingleton<EnrollmentService>();
builder.Services.AddSingleton<DirectNodeTransport>();
builder.Services.AddSingleton<ReverseNodeTransport>();
builder.Services.AddSingleton<NodeTransportFactory>();
builder.Services.AddHttpClient<SelfUpdateService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});

var app = builder.Build();

JsonGatewayDatabase database = app.Services.GetRequiredService<JsonGatewayDatabase>();
GatewayAuthService auth = app.Services.GetRequiredService<GatewayAuthService>();
await database.InitializeAsync();
await auth.SeedAsync();
app.Logger.LogInformation("LinuxGateway data root: {DataRoot}", options.DataRoot);
app.Logger.LogInformation("Initial admin file: {InitialAdminPath}", Path.Combine(options.DataRoot, "initial-admin.txt"));

app.UseApiDiagnostics();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(ApiDiagnostics.WriteExceptionAsync);
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
});

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(15)
});

app.UseLinuxGatewaySecurity();

string? webRoot = ResolveWebRoot(app.Environment.ContentRootPath);
if (webRoot is not null)
{
    var webFileProvider = new PhysicalFileProvider(webRoot);
    app.MapGet("/", () => Results.File(Path.Combine(webRoot, "index.html"), "text/html; charset=utf-8"));
    app.UseStaticFiles(new StaticFileOptions { FileProvider = webFileProvider });
}
else
{
    app.MapGet("/", () => Results.Problem("找不到 wwwroot/index.html。请确认发布目录里包含 wwwroot 文件夹。"));
}

ApiRoutes.Map(app);
ReverseNodeEndpoint.Map(app);
app.Run();

static string ResolveContentRoot()
{
    string baseDirectory = AppContext.BaseDirectory;
    string currentDirectory = Directory.GetCurrentDirectory();

    string[] directCandidates =
    [
        baseDirectory,
        currentDirectory,
        Path.Combine(currentDirectory, "LinuxGateway")
    ];

    foreach (string candidate in directCandidates)
    {
        if (IsLinuxGatewayContentRoot(candidate))
        {
            return Path.GetFullPath(candidate);
        }
    }

    DirectoryInfo? directory = new(baseDirectory);
    while (directory is not null)
    {
        if (IsLinuxGatewayContentRoot(directory.FullName))
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

static bool IsLinuxGatewayContentRoot(string path)
{
    return File.Exists(Path.Combine(path, "appsettings.json")) &&
           (File.Exists(Path.Combine(path, "wwwroot", "index.html")) ||
            File.Exists(Path.Combine(path, "LinuxGateway.csproj")));
}
