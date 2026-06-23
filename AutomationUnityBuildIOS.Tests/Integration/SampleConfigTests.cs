using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class SampleConfigTests
{
    [Fact]
    public void LoadIosSampleConfig_DoesNotThrow()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string configPath = Path.Combine(tempDir, "build-ios.sample.json");
            File.WriteAllText(configPath, SampleFiles.BuildIosConfigJson);
            BuildConfig config = BuildConfig.Load(configPath);
            Assert.True(config.IsIos);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void LoadAndroidSampleConfig_DoesNotThrow()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string configPath = Path.Combine(tempDir, "build-android.sample.json");
            File.WriteAllText(configPath, SampleFiles.BuildAndroidConfigJson);
            BuildConfig config = BuildConfig.Load(configPath);
            Assert.True(config.IsAndroid);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }
}
