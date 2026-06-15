using BuildServer;
using BuildServer.Persistence;
using BuildServer.Security;
using BuildServer.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://127.0.0.1:5088");
}

BuildServerOptions options = BuildServerEnvironment.Load(builder.Configuration, builder.Environment);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<JsonDatabase>();
builder.Services.AddSingleton<AuthService>();
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

app.UseDefaultFiles();
app.UseStaticFiles();

ApiRoutes.Map(app);
McpEndpoint.Map(app);

app.Run();
