using Xunit;
using System.Text.Json;

namespace AutomationUnityBuildIOS.Tests;

public class ConfigRoundTripTests
{
    [Fact]
    public void IosConfig_SerializeDeserialize_RoundTrip()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string configPath = Path.Combine(tempDir, "build-ios.json");
            BuildConfig original = new()
            {
                ConfigName = "roundtrip-test",
                BuildPlatform = "ios",
                RepositoryUrl = "https://github.com/company/game.git",
                Branch = "main",
                WorkspaceRoot = "~/workspace",
                ProjectDirectoryName = "game",
                UnityProjectRelativePath = ".",
                UnityVersion = "2022.3.62f2c1",
                BundleIdentifier = "com.company.game",
                ProductName = "Game",
                BundleVersion = "1.0.0",
                BuildNumber = "42",
                TeamId = "ABCDE12345",
                ExportMethod = "app-store",
                ArtifactsRoot = "~/artifacts",
                AppStoreConnectUploadEnabled = true,
                AppStoreConnectApiKeyPath = "~/key.p8",
                AppStoreConnectApiKeyId = "KEY123",
                AppStoreConnectApiIssuerId = "ISSUER123"
            };

            ConfigFileWriter.Save(configPath, original);
            BuildConfig loaded = BuildConfig.Load(configPath);

            Assert.Equal(original.ConfigName, loaded.ConfigName);
            Assert.Equal(original.BuildPlatform, loaded.BuildPlatform);
            Assert.Equal(original.RepositoryUrl, loaded.RepositoryUrl);
            Assert.Equal(original.Branch, loaded.Branch);
            Assert.Equal(original.BundleIdentifier, loaded.BundleIdentifier);
            Assert.Equal(original.ProductName, loaded.ProductName);
            Assert.Equal(original.BundleVersion, loaded.BundleVersion);
            Assert.Equal(original.BuildNumber, loaded.BuildNumber);
            Assert.Equal(original.TeamId, loaded.TeamId);
            Assert.Equal(original.ExportMethod, loaded.ExportMethod);
            Assert.Equal(original.AppStoreConnectUploadEnabled, loaded.AppStoreConnectUploadEnabled);
            Assert.Equal(original.AppStoreConnectApiKeyPath, loaded.AppStoreConnectApiKeyPath);
            Assert.Equal(original.AppStoreConnectApiKeyId, loaded.AppStoreConnectApiKeyId);
            Assert.Equal(original.AppStoreConnectApiIssuerId, loaded.AppStoreConnectApiIssuerId);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void AndroidConfig_SerializeDeserialize_RoundTrip()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string configPath = Path.Combine(tempDir, "build-android.json");
            BuildConfig original = new()
            {
                ConfigName = "android-roundtrip",
                BuildPlatform = "android",
                RepositoryUrl = "https://github.com/company/game.git",
                Branch = "main",
                WorkspaceRoot = "~/workspace",
                ProjectDirectoryName = "game",
                UnityProjectRelativePath = ".",
                UnityVersion = "2022.3.62f2c1",
                BundleIdentifier = "com.company.game",
                ProductName = "Game",
                BundleVersion = "1.0.0",
                BuildNumber = "100",
                ArtifactsRoot = "~/artifacts",
                AndroidBuildFormat = "both",
                GooglePlayUploadEnabled = true,
                GooglePlayPackageName = "com.company.game",
                GooglePlayServiceAccountJsonPath = "~/service.json",
                GooglePlayTrack = "internal",
                GooglePlayReleaseStatus = "draft"
            };

            ConfigFileWriter.Save(configPath, original);
            BuildConfig loaded = BuildConfig.Load(configPath);

            Assert.Equal(original.BuildPlatform, loaded.BuildPlatform);
            Assert.Equal(original.AndroidBuildFormat, loaded.AndroidBuildFormat);
            Assert.Equal(original.GooglePlayUploadEnabled, loaded.GooglePlayUploadEnabled);
            Assert.Equal(original.GooglePlayPackageName, loaded.GooglePlayPackageName);
            Assert.Equal(original.GooglePlayTrack, loaded.GooglePlayTrack);
            Assert.Equal(original.GooglePlayReleaseStatus, loaded.GooglePlayReleaseStatus);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void Load_NonExistentFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => BuildConfig.Load("/nonexistent/path/config.json"));
    }

    [Fact]
    public void Load_InvalidJson_Throws()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string configPath = TestHelpers.WriteTempConfig(tempDir, "{ invalid json }");
            Assert.Throws<JsonException>(() => BuildConfig.Load(configPath));
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void Load_EmptyFile_Throws()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string configPath = TestHelpers.WriteTempConfig(tempDir, "");
            Assert.Throws<JsonException>(() => BuildConfig.Load(configPath));
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }
}
