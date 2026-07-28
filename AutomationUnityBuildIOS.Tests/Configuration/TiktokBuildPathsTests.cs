using Xunit;

namespace AutomationUnityBuildIOS.Tests;

public class TiktokBuildPathsTests
{
    private static BuildConfig CreateValidTiktokConfig(string artifactsRoot)
    {
        return new BuildConfig
        {
            ConfigName = "tiktok-test",
            BuildPlatform = "tiktok",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "6000.0.0f1",
            UnityBuildMethod = DefaultUnityBuildMethods.Tiktok,
            BundleIdentifier = "com.company.game",
            ProductName = "Game",
            BundleVersion = "1.0.0",
            BuildNumber = "1",
            ArtifactsRoot = artifactsRoot
        };
    }

    [Fact]
    public void Create_TiktokConfig_ResolvesWebglOutputDirectory()
    {
        string artifactsRoot = TestHelpers.CreateTempDir();
        try
        {
            BuildConfig config = CreateValidTiktokConfig(artifactsRoot);
            BuildPaths paths = BuildPaths.Create(config);
            Assert.False(string.IsNullOrWhiteSpace(paths.TiktokWebglOutputDirectory));
            Assert.Contains("TiktokWebGL", paths.TiktokWebglOutputDirectory);
        }
        finally
        {
            TestHelpers.CleanupTempDir(artifactsRoot);
        }
    }

    [Fact]
    public void Create_TiktokConfig_WebglDirUnderArtifactsRoot()
    {
        string artifactsRoot = TestHelpers.CreateTempDir();
        try
        {
            BuildConfig config = CreateValidTiktokConfig(artifactsRoot);
            BuildPaths paths = BuildPaths.Create(config);
            string normalizedArtifactsRoot = Path.GetFullPath(artifactsRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedWebglDir = Path.GetFullPath(paths.TiktokWebglOutputDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.StartsWith(normalizedArtifactsRoot, normalizedWebglDir, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TestHelpers.CleanupTempDir(artifactsRoot);
        }
    }
}
