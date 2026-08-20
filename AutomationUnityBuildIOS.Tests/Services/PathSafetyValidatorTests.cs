using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class PathSafetyValidatorTests
{
    private static BuildRunContext CreateContext(
        string? workspaceRoot = null,
        string? artifactsRoot = null,
        Action<BuildConfig>? configure = null)
    {
        workspaceRoot ??= TestHelpers.CreateTempDir();
        artifactsRoot ??= TestHelpers.CreateTempDir();

        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = workspaceRoot,
            AllowedWorkspaceRoots = [workspaceRoot],
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = artifactsRoot,
            AllowedArtifactsRoots = [artifactsRoot]
        };
        configure?.Invoke(config);

        CliOptions options = CliOptions.Parse(["--dry-run", "--allow-non-mac"]);
        return BuildRunContext.Create(config, options);
    }

    [Fact]
    public void Validate_ValidPaths_DoesNotThrow()
    {
        using BuildRunContext context = CreateContext();
        var validator = new PathSafetyValidator(context);
        validator.Validate();
    }

    [Fact]
    public void Validate_WorkspaceRootNotInAllowList_Throws()
    {
        string workspace = TestHelpers.CreateTempDir();
        string otherDir = TestHelpers.CreateTempDir();
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = workspace,
            AllowedWorkspaceRoots = [otherDir],
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = TestHelpers.CreateTempDir(),
            AllowedArtifactsRoots = [TestHelpers.CreateTempDir()]
        };
        CliOptions options = CliOptions.Parse(["--dry-run", "--allow-non-mac"]);
        using BuildRunContext context = BuildRunContext.Create(config, options);
        var validator = new PathSafetyValidator(context);

        Assert.Throws<InvalidOperationException>(() => validator.Validate());
    }

    [Fact]
    public void Validate_ArtifactsRootNotInAllowList_Throws()
    {
        string artifacts = TestHelpers.CreateTempDir();
        string otherDir = TestHelpers.CreateTempDir();
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            AllowedWorkspaceRoots = [TestHelpers.CreateTempDir()],
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = artifacts,
            AllowedArtifactsRoots = [otherDir]
        };
        CliOptions options = CliOptions.Parse(["--dry-run", "--allow-non-mac"]);
        using BuildRunContext context = BuildRunContext.Create(config, options);
        var validator = new PathSafetyValidator(context);

        Assert.Throws<InvalidOperationException>(() => validator.Validate());
    }

    [Fact]
    public void Validate_EmptyAllowList_UsesConfiguredRoot()
    {
        using BuildRunContext context = CreateContext();
        var validator = new PathSafetyValidator(context);
        validator.Validate();
    }

    [Fact]
    public void Validate_AndroidPaths_Valid()
    {
        string workspace = TestHelpers.CreateTempDir();
        string artifacts = TestHelpers.CreateTempDir();
        BuildConfig config = new()
        {
            BuildPlatform = "android",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = workspace,
            AllowedWorkspaceRoots = [workspace],
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = artifacts,
            AllowedArtifactsRoots = [artifacts],
            AndroidBuildFormat = "both",
            ProductName = "TestGame"
        };
        CliOptions options = CliOptions.Parse(["--dry-run", "--allow-non-mac"]);
        using BuildRunContext context = BuildRunContext.Create(config, options);
        var validator = new PathSafetyValidator(context);
        validator.Validate();
    }

    [Fact]
    public void Validate_FilesystemRootInAllowList_Throws()
    {
        BuildConfig config = new()
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            AllowedWorkspaceRoots = ["/"],
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            ArtifactsRoot = TestHelpers.CreateTempDir(),
            AllowedArtifactsRoots = [TestHelpers.CreateTempDir()]
        };
        CliOptions options = CliOptions.Parse(["--dry-run", "--allow-non-mac"]);
        using BuildRunContext context = BuildRunContext.Create(config, options);
        var validator = new PathSafetyValidator(context);

        Assert.Throws<InvalidOperationException>(() => validator.Validate());
    }

    [Fact]
    public void Validate_ExternalExportOptionsInput_IsAllowedAndItsParentIsNotCreated()
    {
        string externalRoot = Path.Combine(Path.GetTempPath(), $"external-plist-{Guid.NewGuid():N}");
        string externalPlist = Path.Combine(externalRoot, "ExportOptions.plist");
        using BuildRunContext context = CreateContext(configure: config =>
        {
            config.GenerateExportOptionsPlist = false;
            config.ExportOptionsPlistPath = externalPlist;
        });

        new PathSafetyValidator(context).Validate();
        new BuildDirectoryPreparer(context).Prepare();

        Assert.False(Directory.Exists(externalRoot));
    }

    [Fact]
    public void Validate_ExternalGeneratedExportOptionsOutput_Throws()
    {
        string externalPlist = Path.Combine(
            Path.GetTempPath(),
            $"external-generated-plist-{Guid.NewGuid():N}",
            "ExportOptions.plist");
        using BuildRunContext context = CreateContext(configure: config =>
        {
            config.GenerateExportOptionsPlist = true;
            config.ExportOptionsPlistPath = externalPlist;
        });

        Assert.Throws<InvalidOperationException>(() => new PathSafetyValidator(context).Validate());
    }
}
