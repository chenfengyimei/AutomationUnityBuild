using BuildServer;
using BuildServer.Persistence;
using BuildServer.Security;
using BuildServer.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.WebHost.UseContentRoot(AppContext.BaseDirectory);
builder.WebHost.UseWebRoot(Path.Combine(AppContext.BaseDirectory, "wwwroot"));

if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://127.0.0.1:5088");
}

BuildServerOptions options = BuildServerEnvironment.Load(builder.Configuration, builder.Environment);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<JsonDatabase>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<LoginRateLimiter>();
builder.Services.AddSingleton<BuildQueueService>();
builder.Services.AddSingleton<ArtifactScanner>();
builder.Services.AddSingleton<BuildWorkerService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<BuildWorkerService>());
builder.Services.AddHostedService<MaintenanceService>();

var app = builder.Build();

JsonDatabase database = app.Services.GetRequiredService<JsonDatabase>();
AuthService auth = app.Services.GetRequiredService<AuthService>();
await database.InitializeAsync();
await auth.SeedDefaultsAsync();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        Exception? exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        int statusCode = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            FileNotFoundException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            error = statusCode == StatusCodes.Status500InternalServerError ? "服务器内部错误。" : exception?.Message
        });
    });
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto
});
app.UseBuildServerSecurity();

string? webRoot = ResolveWebRoot(app.Environment.ContentRootPath);
if (webRoot is not null)
{
    var webFileProvider = new PhysicalFileProvider(webRoot);
    app.MapGet("/", () => Results.File(Path.Combine(webRoot, "index.html"), "text/html; charset=utf-8"));
    app.UseStaticFiles(new StaticFileOptions { FileProvider = webFileProvider });
}
else
{
    app.MapGet("/", () => Results.Problem("找不到 wwwroot/index.html。请确认发布目录里包含 wwwroot 文件夹，并从完整发布目录启动 BuildServer。"));
}

ApiRoutes.Map(app);
McpEndpoint.Map(app);

app.Run();

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
