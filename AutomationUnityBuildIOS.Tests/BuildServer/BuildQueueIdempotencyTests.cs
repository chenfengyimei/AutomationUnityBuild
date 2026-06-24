using System.Text.Json.Nodes;
using BuildServer;
using BuildServer.Persistence;
using BuildServer.Security;
using BuildServer.Services;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.BuildServer;

public sealed class BuildQueueIdempotencyTests
{
    [Fact]
    public async Task EnqueueAsync_ReturnsExistingJob_ForDuplicateClientRequestId()
    {
        string root = Path.Combine(Path.GetTempPath(), $"build-queue-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string configPath = Path.Combine(root, "build-ios.json");
        await File.WriteAllTextAsync(configPath, "{}");

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
                NextBuildNumber = 7
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
            ClientRequestId: "req-123",
            Notes: null);

        BuildJobRecord first = await queue.EnqueueAsync(request, user, BuildSources.Web);
        BuildJobRecord second = await queue.EnqueueAsync(request, user, BuildSources.Web);

        BuildServerDatabase snapshot = await database.ReadAsync(db => db);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(snapshot.Jobs);
        Assert.Equal("req-123", snapshot.Jobs[0].ClientRequestId);
        Assert.Equal(8, snapshot.Projects.Single().NextBuildNumber);
    }

    [Fact]
    public async Task EnqueueAsync_OnWindows_ReplacesMacUnityPathInMaterializedConfig()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(programFiles))
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), $"build-queue-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string unityVersion = "2022.3.62f3c1";
        string configPath = Path.Combine(root, "build-android.json");
        await File.WriteAllTextAsync(configPath, $$"""
        {
          "buildPlatform": "android",
          "unityExecutablePath": "/Applications/Unity/Hub/Editor/{{unityVersion}}/Unity.app/Contents/MacOS/Unity"
        }
        """);

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
                    NextBuildNumber = 7
                });
                db.Configs.Add(new BuildConfigRecord
                {
                    Id = "config-1",
                    ProjectId = "project-1",
                    Name = "Android",
                    BuildPlatform = BuildPlatforms.Android,
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
                SkipXcode: true,
                AllowNonMac: false,
                ClientRequestId: null,
                Notes: null);

            BuildJobRecord job = await queue.EnqueueAsync(request, user, BuildSources.Web);

            JsonObject materialized = JsonNode.Parse(await File.ReadAllTextAsync(job.MaterializedConfigPath))!.AsObject();
            string unityExecutablePath = materialized["unityExecutablePath"]!.GetValue<string>();
            string expected = Path.Combine(programFiles, "Unity", "Hub", "Editor", unityVersion, "Editor", "Unity.exe");
            Assert.Equal(expected, unityExecutablePath);
            Assert.DoesNotContain("/Applications/Unity", unityExecutablePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
