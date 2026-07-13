using System.Reflection;
using BuildServer;
using BuildServer.Persistence;
using BuildServer.Security;
using BuildServer.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.BuildServer;

public sealed class EmailNotificationTests
{
    [Fact]
    public async Task UpdateEmailSettingsAsync_SavesSettingsAndPreservesPasswordWhenEmpty()
    {
        string root = TestRoot();
        try
        {
            BuildServerOptions options = Options(root);
            JsonDatabase database = await DatabaseAsync(options);
            UserRecord admin = await SeedAdminAsync(database);
            var auth = new AuthService(database, options);
            string token = await auth.CreateSessionAsync(admin);

            var firstRequest = new EmailSettingsRequest(
                SmtpHost: "smtp.gmail.com",
                SmtpPort: 587,
                SmtpUserName: "bot@example.com",
                SmtpPassword: "secret123",
                FromEmail: "bot@example.com",
                FromName: "BuildServer",
                UseSsl: true,
                Enabled: true);

            IResult firstResult = await InvokeUpdateEmailSettingsAsync(firstRequest, ContextWithBearer(token), auth, database);
            Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(firstResult).StatusCode);

            EmailSettingsRecord stored = await database.ReadAsync(db => db.EmailSettings!)
                ?? throw new InvalidOperationException("EmailSettings was not saved.");
            Assert.Equal("smtp.gmail.com", stored.SmtpHost);
            Assert.Equal(587, stored.SmtpPort);
            Assert.Equal("secret123", stored.SmtpPassword);
            Assert.True(stored.Enabled);

            var secondRequest = new EmailSettingsRequest(
                SmtpHost: "smtp.qq.com",
                SmtpPort: 465,
                SmtpUserName: "bot@example.com",
                SmtpPassword: null,
                FromEmail: "bot@example.com",
                FromName: "BuildServer",
                UseSsl: true,
                Enabled: true);

            IResult secondResult = await InvokeUpdateEmailSettingsAsync(secondRequest, ContextWithBearer(token), auth, database);
            Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(secondResult).StatusCode);

            EmailSettingsRecord updated = await database.ReadAsync(db => db.EmailSettings!);
            Assert.Equal("smtp.qq.com", updated.SmtpHost);
            Assert.Equal(465, updated.SmtpPort);
            Assert.Equal("secret123", updated.SmtpPassword);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task GetEmailSettingsAsync_DoesNotReturnPassword()
    {
        string root = TestRoot();
        try
        {
            BuildServerOptions options = Options(root);
            JsonDatabase database = await DatabaseAsync(options);
            UserRecord admin = await SeedAdminAsync(database);
            var auth = new AuthService(database, options);
            string token = await auth.CreateSessionAsync(admin);

            await database.UpdateAsync(db =>
            {
                db.EmailSettings = new EmailSettingsRecord
                {
                    SmtpHost = "smtp.gmail.com",
                    SmtpPort = 587,
                    SmtpUserName = "bot@example.com",
                    SmtpPassword = "super-secret",
                    FromEmail = "bot@example.com",
                    Enabled = true
                };
            });

            IResult result = await InvokeGetEmailSettingsAsync(ContextWithBearer(token), auth, database);
            Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task EnqueueAsync_StoresNotifyEmailsInJobRecord()
    {
        string root = Path.Combine(Path.GetTempPath(), $"build-queue-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string configPath = Path.Combine(root, "build-ios.json");
        await File.WriteAllTextAsync(configPath, "{}");

        try
        {
            var options = new BuildServerOptions
            {
                DataRoot = root,
                AllowedConfigRoots = [root],
                AllowedWorkspaceRoots = [root],
                AllowedArtifactsRoots = [root],
                WorkerName = "test-worker"
            };
            var database = new JsonDatabase(options);
            await database.InitializeAsync();
            await database.UpdateAsync(db =>
            {
                db.Projects.Add(new ProjectRecord
                {
                    Id = "project-1",
                    Name = "Game",
                    RepositoryUrl = "https://github.com/org/game.git",
                    DefaultBranch = "main",
                    AllowedBranches = ["main"],
                    WorkspaceRoot = root,
                    ArtifactsRoot = root,
                    NextBuildNumber = 1
                });
                db.Configs.Add(new BuildConfigRecord
                {
                    Id = "config-1",
                    ProjectId = "project-1",
                    Name = "Release",
                    BuildPlatform = BuildPlatforms.Ios,
                    ConfigPath = configPath
                });
            });

            var queue = new BuildQueueService(database, options);
            var user = new CurrentUser("user-1", "admin", "Admin", Roles.Admin);
            var request = new StartBuildRequest(
                "project-1",
                "config-1",
                Branch: null,
                BuildNumber: null,
                DryRun: false,
                SkipGit: false,
                SkipUnity: false,
                SkipXcode: false,
                AllowNonMac: false,
                ClientRequestId: null,
                Notes: null,
                NotifyEmails: ["alice@example.com", "bob@example.com"]);

            BuildJobRecord job = await queue.EnqueueAsync(request, user, BuildSources.Web);

            Assert.Equal(2, job.NotifyEmails.Count);
            Assert.Contains("alice@example.com", job.NotifyEmails);
            Assert.Contains("bob@example.com", job.NotifyEmails);

            BuildJobRecord stored = await database.ReadAsync(db => db.Jobs.Single(j => j.Id == job.Id));
            Assert.Equal(2, stored.NotifyEmails.Count);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public async Task EnqueueAsync_RejectsInvalidNotifyEmails()
    {
        string root = Path.Combine(Path.GetTempPath(), $"build-queue-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string configPath = Path.Combine(root, "build-ios.json");
        await File.WriteAllTextAsync(configPath, "{}");

        try
        {
            var options = new BuildServerOptions
            {
                DataRoot = root,
                AllowedConfigRoots = [root],
                AllowedWorkspaceRoots = [root],
                AllowedArtifactsRoots = [root],
                WorkerName = "test-worker"
            };
            var database = new JsonDatabase(options);
            await database.InitializeAsync();
            await database.UpdateAsync(db =>
            {
                db.Projects.Add(new ProjectRecord
                {
                    Id = "project-1",
                    Name = "Game",
                    RepositoryUrl = "https://github.com/org/game.git",
                    DefaultBranch = "main",
                    AllowedBranches = ["main"],
                    WorkspaceRoot = root,
                    ArtifactsRoot = root,
                    NextBuildNumber = 1
                });
                db.Configs.Add(new BuildConfigRecord
                {
                    Id = "config-1",
                    ProjectId = "project-1",
                    Name = "Release",
                    BuildPlatform = BuildPlatforms.Ios,
                    ConfigPath = configPath
                });
            });

            var queue = new BuildQueueService(database, options);
            var user = new CurrentUser("user-1", "admin", "Admin", Roles.Admin);
            var request = new StartBuildRequest(
                "project-1",
                "config-1",
                Branch: null,
                BuildNumber: null,
                DryRun: false,
                SkipGit: false,
                SkipUnity: false,
                SkipXcode: false,
                AllowNonMac: false,
                ClientRequestId: null,
                Notes: null,
                NotifyEmails: ["not-an-email"]);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                queue.EnqueueAsync(request, user, BuildSources.Web));
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static async Task<IResult> InvokeUpdateEmailSettingsAsync(
        EmailSettingsRequest request,
        HttpContext context,
        AuthService auth,
        JsonDatabase database)
    {
        MethodInfo method = typeof(ApiRoutes).GetMethod("UpdateEmailSettingsAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("UpdateEmailSettingsAsync route handler was not found.");
        var task = (Task<IResult>?)method.Invoke(null, [request, context, auth, database])
            ?? throw new InvalidOperationException("UpdateEmailSettingsAsync route handler did not return a task.");
        return await task;
    }

    private static async Task<IResult> InvokeGetEmailSettingsAsync(
        HttpContext context,
        AuthService auth,
        JsonDatabase database)
    {
        MethodInfo method = typeof(ApiRoutes).GetMethod("GetEmailSettingsAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetEmailSettingsAsync route handler was not found.");
        var task = (Task<IResult>?)method.Invoke(null, [context, auth, database])
            ?? throw new InvalidOperationException("GetEmailSettingsAsync route handler did not return a task.");
        return await task;
    }

    private static async Task<UserRecord> SeedAdminAsync(JsonDatabase database)
    {
        var admin = new UserRecord
        {
            Id = "usr-admin",
            UserName = "admin",
            DisplayName = "Admin",
            Role = Roles.Admin,
            PasswordHash = PasswordHasher.Hash("Passw0rd!one"),
            Enabled = true,
            CreatedAt = DateTimeOffset.Now
        };

        await database.UpdateAsync(db => db.Users.Add(admin));
        return admin;
    }

    private static async Task<JsonDatabase> DatabaseAsync(BuildServerOptions options)
    {
        var database = new JsonDatabase(options);
        await database.InitializeAsync();
        return database;
    }

    private static BuildServerOptions Options(string root)
    {
        return new BuildServerOptions { DataRoot = root };
    }

    private static DefaultHttpContext ContextWithBearer(string token)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context;
    }

    private static string TestRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"build-server-email-{Guid.NewGuid():N}");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
