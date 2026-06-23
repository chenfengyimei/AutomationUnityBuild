using Xunit;
using System.Text.Json;

namespace AutomationUnityBuildIOS.Tests;

public class BuildConfigTests
{
    private static BuildConfig CreateValidIosConfig()
    {
        return new BuildConfig
        {
            ConfigName = "test",
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            UnityBuildMethod = DefaultUnityBuildMethods.Ios,
            BundleIdentifier = "com.company.game",
            ProductName = "Game",
            BundleVersion = "1.0.0",
            BuildNumber = "1",
            TeamId = "ABCDE12345",
            ExportMethod = "development",
            ArtifactsRoot = TestHelpers.CreateTempDir()
        };
    }

    private static BuildConfig CreateValidAndroidConfig()
    {
        return new BuildConfig
        {
            ConfigName = "test-android",
            BuildPlatform = "android",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            WorkspaceRoot = TestHelpers.CreateTempDir(),
            ProjectDirectoryName = "game",
            UnityProjectRelativePath = ".",
            UnityVersion = "2022.3.62f2c1",
            UnityBuildMethod = DefaultUnityBuildMethods.Android,
            BundleIdentifier = "com.company.game",
            ProductName = "Game",
            BundleVersion = "1.0.0",
            BuildNumber = "1",
            ArtifactsRoot = TestHelpers.CreateTempDir()
        };
    }

    [Fact]
    public void Validate_ValidIosConfig_DoesNotThrow()
    {
        BuildConfig config = CreateValidIosConfig();
        config.EnsureValid();
    }

    [Fact]
    public void Validate_ValidAndroidConfig_DoesNotThrow()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.EnsureValid();
    }

    [Fact]
    public void Validate_InvalidPlatform_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.BuildPlatform = "windows";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_EmptyRepositoryUrl_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.RepositoryUrl = "";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_EmptyBranch_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.Branch = "";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_TeamIdNot10Chars_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.TeamId = "ABC";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_TeamIdWithSpecialChars_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.TeamId = "ABCDE1234!";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AppStoreUploadWithoutApiKey_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.ExportMethod = "app-store";
        config.AppStoreConnectUploadEnabled = true;
        config.AppStoreConnectApiKeyPath = "";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AppStoreUploadWithoutKeyId_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.ExportMethod = "app-store";
        config.AppStoreConnectUploadEnabled = true;
        config.AppStoreConnectApiKeyPath = "~/key.p8";
        config.AppStoreConnectApiKeyId = "";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AppStoreUploadWrongExportMethod_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.ExportMethod = "development";
        config.AppStoreConnectUploadEnabled = true;
        config.AppStoreConnectApiKeyPath = "~/key.p8";
        config.AppStoreConnectApiKeyId = "KEY123";
        config.AppStoreConnectApiIssuerId = "ISSUER123";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_GenerateExportOptionsFalseWithoutPath_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.GenerateExportOptionsPlist = false;
        config.ExportOptionsPlistPath = "";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_InvalidIosDeploymentTarget_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.IosDeploymentTarget = "abc";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_SyncBundleVersionFalseWithoutBundleVersion_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.SyncBundleVersionFromUnity = false;
        config.BundleVersion = "";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AutoIncrementWithNonNumericBuildNumber_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.AutoIncrementBuildNumber = true;
        config.BuildNumber = "abc";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AutoIncrementWithNumericBuildNumber_DoesNotThrow()
    {
        BuildConfig config = CreateValidIosConfig();
        config.AutoIncrementBuildNumber = true;
        config.BuildNumber = "42";
        config.EnsureValid();
    }

    [Fact]
    public void Validate_AutoIncrementWithEmptyBuildNumber_DoesNotThrow()
    {
        BuildConfig config = CreateValidIosConfig();
        config.AutoIncrementBuildNumber = true;
        config.BuildNumber = "";
        config.EnsureValid();
    }

    [Fact]
    public void Validate_AndroidInvalidBuildFormat_Throws()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.AndroidBuildFormat = "ipa";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AndroidNegativeBuildNumber_Throws()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.BuildNumber = "-1";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AndroidZeroBuildNumber_Throws()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.BuildNumber = "0";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AndroidNonNumericBuildNumber_Throws()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.BuildNumber = "abc";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AndroidLargeBuildNumber_DoesNotThrow()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.BuildNumber = "3000000000";
        config.EnsureValid();
    }

    [Fact]
    public void Validate_AndroidGooglePlayWithoutPackageName_Throws()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.GooglePlayUploadEnabled = true;
        config.GooglePlayServiceAccountJsonPath = "~/key.json";
        config.BundleIdentifier = "";
        config.GooglePlayPackageName = "";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AndroidGooglePlayWithoutServiceAccount_Throws()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.GooglePlayUploadEnabled = true;
        config.GooglePlayServiceAccountJsonPath = "";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AndroidGooglePlayInvalidReleaseStatus_Throws()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.GooglePlayUploadEnabled = true;
        config.GooglePlayServiceAccountJsonPath = "~/key.json";
        config.GooglePlayReleaseStatus = "invalid";
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AndroidGooglePlayUserFractionOutOfRange_Throws()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.GooglePlayUploadEnabled = true;
        config.GooglePlayServiceAccountJsonPath = "~/key.json";
        config.GooglePlayUserFraction = 1.5;
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AndroidGooglePlayUserFractionZero_Throws()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.GooglePlayUploadEnabled = true;
        config.GooglePlayServiceAccountJsonPath = "~/key.json";
        config.GooglePlayUserFraction = 0;
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_IosPlatformWithAndroidBuildMethod_Throws()
    {
        BuildConfig config = CreateValidIosConfig();
        config.UnityBuildMethod = DefaultUnityBuildMethods.Android;
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void Validate_AndroidPlatformWithIosBuildMethod_Throws()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.UnityBuildMethod = DefaultUnityBuildMethods.Ios;
        Assert.Throws<InvalidOperationException>(() => config.EnsureValid());
    }

    [Fact]
    public void IsIos_IosPlatform_ReturnsTrue()
    {
        BuildConfig config = CreateValidIosConfig();
        Assert.True(config.IsIos);
        Assert.False(config.IsAndroid);
    }

    [Fact]
    public void IsAndroid_AndroidPlatform_ReturnsTrue()
    {
        BuildConfig config = CreateValidAndroidConfig();
        Assert.True(config.IsAndroid);
        Assert.False(config.IsIos);
    }

    [Fact]
    public void EffectiveGooglePlayPackageName_WithExplicitName_ReturnsExplicit()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.GooglePlayPackageName = "com.company.other";
        Assert.Equal("com.company.other", config.EffectiveGooglePlayPackageName());
    }

    [Fact]
    public void EffectiveGooglePlayPackageName_WithoutExplicitName_ReturnsBundleId()
    {
        BuildConfig config = CreateValidAndroidConfig();
        config.GooglePlayPackageName = "";
        Assert.Equal("com.company.game", config.EffectiveGooglePlayPackageName());
    }
}
