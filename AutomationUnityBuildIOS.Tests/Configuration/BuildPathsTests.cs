using System.Text.Json;
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
    public void Create_RelativeRoots_AreImmediatelyNormalizedToAbsolutePaths()
    {
        string unique = $"relative-{Guid.NewGuid():N}";
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = Path.Combine(unique, "workspace"),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = Path.Combine(unique, "artifacts")
        };

        BuildPaths paths = BuildPaths.Create(config);

        Assert.True(Path.IsPathFullyQualified(paths.WorkspaceRoot));
        Assert.True(Path.IsPathFullyQualified(paths.RepositoryRoot));
        Assert.True(Path.IsPathFullyQualified(paths.ArtifactsRoot));
        Assert.True(Path.IsPathFullyQualified(paths.ArtifactsRunRoot));
    }

    [Fact]
    public void Create_RelativeOutputPaths_AreResolvedUnderTheirRunRoots()
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
            AndroidOutputDirectory = Path.Combine("packages", "android"),
            ApkOutputPath = Path.Combine("apk", "game.apk")
        };

        BuildPaths paths = BuildPaths.Create(config);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(paths.ArtifactsRunRoot, "packages", "android")),
            paths.AndroidOutputDirectory);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(paths.AndroidOutputDirectory, "apk", "game.apk")),
            paths.ApkOutputPath);
    }

    [Fact]
    public void Create_LoadedRelativePaths_AreResolvedFromConfigDirectory()
    {
        string configDirectory = TestHelpers.CreateTempDir();
        try
        {
            string configPath = Path.Combine(configDirectory, "build-ios.json");
            BuildConfig source = new()
            {
                BuildPlatform = "ios",
                RepositoryUrl = "https://github.com/company/game.git",
                Branch = "main",
                WorkspaceRoot = "workspace",
                ProjectDirectoryName = "game",
                UnityProjectRelativePath = ".",
                UnityVersion = "2022.3.62f2c1",
                UnityExecutablePath = Path.Combine("tools", "Unity"),
                ArtifactsRoot = "artifacts",
                GenerateExportOptionsPlist = false,
                ExportOptionsPlistPath = Path.Combine("signing", "ExportOptions.plist")
            };
            File.WriteAllText(configPath, JsonSerializer.Serialize(source));

            BuildPaths paths = BuildPaths.Create(BuildConfig.Load(configPath));

            Assert.Equal(Path.GetFullPath(Path.Combine(configDirectory, "workspace")), paths.WorkspaceRoot);
            Assert.Equal(Path.GetFullPath(Path.Combine(configDirectory, "artifacts")), paths.ArtifactsRoot);
            Assert.Equal(Path.GetFullPath(Path.Combine(configDirectory, "tools", "Unity")), paths.UnityExecutable);
            Assert.Equal(
                Path.GetFullPath(Path.Combine(configDirectory, "signing", "ExportOptions.plist")),
                paths.ExportOptionsPlistPath);
        }
        finally
        {
            TestHelpers.CleanupTempDir(configDirectory);
        }
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
    public void Create_UnityExecutable_FromForeignPlatformPath_FallsBackToVersion()
    {
        string foreignPath = OperatingSystem.IsWindows()
            ? "/Applications/Unity/Hub/Editor/2022.3.1f1/Unity.app/Contents/MacOS/Unity"
            : @"C:\Program Files\Unity\Hub\Editor\2022.3.1f1\Editor\Unity.exe";
        BuildConfig config = new()
        {
            BuildPlatform = "android",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            UnityExecutablePath = foreignPath,
            ArtifactsRoot = TestHelpers.CreateTempDir()
        };

        BuildPaths paths = BuildPaths.Create(config);

        Assert.Contains("2022.3.62f2c1", paths.UnityExecutable);
        Assert.NotEqual(foreignPath, paths.UnityExecutable);
    }

    [Fact]
    public void ResolveConfiguredPath_ForeignPlatformAbsolutePath_Throws()
    {
        string foreignPath = OperatingSystem.IsWindows()
            ? "/opt/unity-builds"
            : @"C:\UnityBuilds";

        Assert.Throws<InvalidOperationException>(() => new BuildConfig().ResolveConfiguredPath(foreignPath));
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
    public void FindLatestUnityEditorDirectory_OrdersUnityVersionsNumerically()
    {
        string editorRoot = TestHelpers.CreateTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(editorRoot, "2022.3.9f1"));
            Directory.CreateDirectory(Path.Combine(editorRoot, "2022.3.10f1"));
            Directory.CreateDirectory(Path.Combine(editorRoot, "2021.3.40f1"));

            DirectoryInfo? latest = BuildPaths.FindLatestUnityEditorDirectory(editorRoot);

            Assert.NotNull(latest);
            Assert.Equal("2022.3.10f1", latest.Name);
        }
        finally
        {
            TestHelpers.CleanupTempDir(editorRoot);
        }
    }

    [Fact]
    public void CompareUnityVersionNames_HandlesUnityMajorVersionFamilies()
    {
        Assert.True(BuildPaths.CompareUnityVersionNames("6000.0.1f1", "2023.3.50f1") > 0);
        Assert.True(BuildPaths.CompareUnityVersionNames("2022.3.10f1", "2022.3.9f1") > 0);
        Assert.True(BuildPaths.CompareUnityVersionNames("2022.3.10f2", "2022.3.10f1") > 0);
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
    public void Create_ProductNameWithWindowsInvalidCharacters_UsesPortableArtifactName()
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
            ProductName = "Game:Release*Candidate"
        };

        BuildPaths paths = BuildPaths.Create(config);

        Assert.Equal("Game_Release_Candidate.apk", Path.GetFileName(paths.ApkOutputPath));
        Assert.Equal("Game_Release_Candidate.aab", Path.GetFileName(paths.AabOutputPath));
    }

    [Fact]
    public void Create_WindowsReservedProductName_IsMadePortableOnEveryHost()
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
            ProductName = "CON"
        };

        BuildPaths paths = BuildPaths.Create(config);

        Assert.Equal("_CON.apk", Path.GetFileName(paths.ApkOutputPath));
        Assert.Equal("_CON.aab", Path.GetFileName(paths.AabOutputPath));
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
