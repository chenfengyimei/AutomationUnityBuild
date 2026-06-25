using Xunit;

namespace AutomationUnityBuildIOS.Tests;

public class AutomationWorkflowDryRunTests
{
    [Fact]
    public async Task RunAsync_DryRun_CreatesLogsAndCommandLogFiles()
    {
        string workspaceRoot = TestHelpers.CreateTempDir();
        string artifactsRoot = TestHelpers.CreateTempDir();
        try
        {
            BuildConfig config = new()
            {
                BuildPlatform = BuildPlatforms.Android,
                RepositoryUrl = "https://github.com/company/game.git",
                Branch = "main",
                WorkspaceRoot = workspaceRoot,
                ProjectDirectoryName = "game",
                UnityProjectRelativePath = ".",
                UnityExecutablePath = Path.Combine(workspaceRoot, "Unity.exe"),
                UnityBuildMethod = DefaultUnityBuildMethods.Android,
                ArtifactsRoot = artifactsRoot,
                AndroidBuildFormat = AndroidBuildFormats.Aab,
                ProductName = "MyGame",
                BundleIdentifier = "com.company.game",
                BuildNumber = "1"
            };
            CliOptions options = CliOptions.Parse(["--dry-run", "--skip-git", "--allow-non-mac"]);

            using var workflow = new AutomationWorkflow(config, options);
            await workflow.RunAsync();

            string runRoot = Assert.Single(Directory.EnumerateDirectories(artifactsRoot));
            string logsDirectory = Path.Combine(runRoot, "Logs");

            Assert.True(Directory.Exists(logsDirectory));
            Assert.True(Directory.Exists(Path.Combine(runRoot, "Android")));
            Assert.True(File.Exists(Path.Combine(logsDirectory, "automation.log")));
            Assert.True(File.Exists(Path.Combine(logsDirectory, "build-config-snapshot.json")));
            Assert.True(File.Exists(Path.Combine(logsDirectory, "unity-process.log")));
            Assert.Contains("[dry-run] Command was not executed.", File.ReadAllText(Path.Combine(logsDirectory, "unity-process.log")));
        }
        finally
        {
            TestHelpers.CleanupTempDir(workspaceRoot);
            TestHelpers.CleanupTempDir(artifactsRoot);
        }
    }
}
