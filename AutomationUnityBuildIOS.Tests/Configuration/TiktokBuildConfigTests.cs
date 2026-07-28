using Xunit;

namespace AutomationUnityBuildIOS.Tests;

public class TiktokBuildConfigTests
{
    private static BuildConfig CreateValidTiktokConfig()
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
            ArtifactsRoot = TestHelpers.CreateTempDir(),
            TiktokAppId = "test_app_id",
            TiktokAccessToken = "test_token",
            TiktokGameName = "Test Game"
        };
    }

    [Fact]
    public void Validate_ValidTiktokConfig_DoesNotThrow()
    {
        BuildConfig config = CreateValidTiktokConfig();
        config.EnsureValid();
    }

    [Fact]
    public void Validate_TiktokWithIosBuilder_Throws()
    {
        BuildConfig config = CreateValidTiktokConfig();
        config.UnityBuildMethod = DefaultUnityBuildMethods.Ios;
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_TiktokWithAndroidBuilder_Throws()
    {
        BuildConfig config = CreateValidTiktokConfig();
        config.UnityBuildMethod = DefaultUnityBuildMethods.Android;
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_TiktokUploadWithoutAppId_Throws()
    {
        BuildConfig config = CreateValidTiktokConfig();
        config.TiktokUploadEnabled = true;
        config.TiktokAppId = "";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_TiktokUploadWithoutAccessToken_Throws()
    {
        BuildConfig config = CreateValidTiktokConfig();
        config.TiktokUploadEnabled = true;
        config.TiktokAccessToken = "";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_TiktokUploadDisabled_WithoutAppId_DoesNotThrow()
    {
        BuildConfig config = CreateValidTiktokConfig();
        config.TiktokUploadEnabled = false;
        config.TiktokAppId = "";
        config.TiktokAccessToken = "";
        config.EnsureValid();
    }

    [Fact]
    public void IsTiktok_TiktokPlatform_ReturnsTrue()
    {
        BuildConfig config = CreateValidTiktokConfig();
        Assert.True(config.IsTiktok);
        Assert.False(config.IsIos);
        Assert.False(config.IsAndroid);
    }

    [Fact]
    public void IsTiktok_IosPlatform_ReturnsFalse()
    {
        BuildConfig config = CreateValidTiktokConfig();
        config.BuildPlatform = BuildPlatforms.Ios;
        config.UnityBuildMethod = DefaultUnityBuildMethods.Ios;
        Assert.False(config.IsTiktok);
        Assert.True(config.IsIos);
    }

    [Fact]
    public void Validate_IosPlatformWithTiktokBuilder_Throws()
    {
        BuildConfig config = CreateValidTiktokConfig();
        config.BuildPlatform = BuildPlatforms.Ios;
        config.UnityBuildMethod = DefaultUnityBuildMethods.Tiktok;
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }
}
