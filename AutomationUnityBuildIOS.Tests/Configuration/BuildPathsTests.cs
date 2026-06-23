using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class BuildPathsTests
{
    [Fact]
    public void Create_GeneratesRunId_WithMillisecondPrecision()
    {
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = TestHelpers.CreateTempDir()
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.Matches(@"^\d{8}-\d{6}-\d{3}$", paths.RunId);
    }

    [Fact]
    public void Create_WorkspaceRoot_ExpandedFromTilde()
    {
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = "~/testworkspace",
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = "~/testartifacts"
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.False(paths.WorkspaceRoot.StartsWith("~"));
        Assert.False(paths.ArtifactsRoot.StartsWith("~"));
    }

    [Fact]
    public void Create_RepositoryRoot_CombinesWorkspaceAndProjectDir()
    {
        string workspace = TestHelpers.CreateTempDir();
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = workspace,
            ProjectDirectoryName = "mygame",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = TestHelpers.CreateTempDir()
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.EndsWith("mygame", paths.RepositoryRoot);
    }

    [Fact]
    public void Create_UnityProjectRoot_IsAbsolute()
    {
        string workspace = TestHelpers.CreateTempDir();
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = workspace,
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = TestHelpers.CreateTempDir()
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.True(Path.IsPathRooted(paths.UnityProjectRoot));
    }

    [Fact]
    public void Create_LogsDirectory_UnderArtifactsRunRoot()
    {
        string artifacts = TestHelpers.CreateTempDir();
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = artifacts
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.StartsWith(paths.ArtifactsRunRoot, paths.LogsDirectory);
    }

    [Fact]
    public void Create_AutomationLogPath_UnderLogsDirectory()
    {
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = TestHelpers.CreateTempDir()
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.StartsWith(paths.LogsDirectory, paths.AutomationLogPath);
    }

    [Fact]
    public void Create_UnityExecutable_FromExplicitPath()
    {
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = TestHelpers.CreateTempDir(),
            UnityExecutablePath = Path.Combine(TestHelpers.CreateTempDir(), "Unity.exe")
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.Equal(config.UnityExecutablePath, paths.UnityExecutable);
    }

    [Fact]
    public void Create_UnityExecutable_FromVersion()
    {
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            ArtifactsRoot = TestHelpers.CreateTempDir(),
            UnityVersion = "2022.3.62f2c1"
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.Contains("2022.3.62f2c1", paths.UnityExecutable);
    }

    [Fact]
    public void Create_ApkOutputPath_HasApkExtension()
    {
        BuildConfig config = new()
        {
            BuildPlatform = "android",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = TestHelpers.CreateTempDir(),
            ProductName = "MyGame"
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.EndsWith(".apk", paths.ApkOutputPath);
    }

    [Fact]
    public void Create_AabOutputPath_HasAabExtension()
    {
        BuildConfig config = new()
        {
            BuildPlatform = "android",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = TestHelpers.CreateTempDir(),
            ProductName = "MyGame"
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.EndsWith(".aab", paths.AabOutputPath);
    }

    [Fact]
    public void Create_ProjectDirectoryNameInferredFromUrl_WhenEmpty()
    {
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/mygame.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = TestHelpers.CreateTempDir()
        };

        BuildPaths paths = BuildPaths.Create(config);
        Assert.EndsWith("mygame", paths.RepositoryRoot);
    }
}
