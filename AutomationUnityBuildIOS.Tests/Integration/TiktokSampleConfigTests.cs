using Xunit;

namespace AutomationUnityBuildIOS.Tests;

public class TiktokSampleConfigTests
{
    [Fact]
    public void LoadTiktokSampleConfig_DoesNotThrow()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string configPath = Path.Combine(tempDir, "build-tiktok.sample.json");
            File.WriteAllText(configPath, SampleFiles.BuildTiktokConfigJson);
            BuildConfig config = BuildConfig.Load(configPath);
            Assert.True(config.IsTiktok);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void LoadTiktokSampleConfig_HasTiktokBuilderMethod()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string configPath = Path.Combine(tempDir, "build-tiktok.sample.json");
            File.WriteAllText(configPath, SampleFiles.BuildTiktokConfigJson);
            BuildConfig config = BuildConfig.Load(configPath);
            Assert.Equal(DefaultUnityBuildMethods.Tiktok, config.UnityBuildMethod);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }
}
