using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class UnityCommandBuilderTests
{
    private static BuildConfig CreateTestConfig()
    {
        return new BuildConfig
        {
            BuildPlatform = "ios",
            RepositoryUrl = "https://github.com/company/game.git",
            Branch = "main",
            UnityBuildMethod = "BuildAutomation.IOSBuilder.Build",
            UnityVersion = "2022.3.62f2c1",
            BundleIdentifier = "com.company.game",
            ProductName = "Game",
            BuildNumber = "42",
            BundleVersion = "1.0.0"
        };
    }

    [Fact]
    public void CreateBatchModeArgs_ContainsRequiredFlags()
    {
        BuildConfig config = CreateTestConfig();
        BuildPaths paths = BuildPaths.Create(config);

        List<string> args = UnityCommandBuilder.CreateBatchModeArgs(config, paths, "iOS");

        Assert.Contains("-batchmode", args);
        Assert.Contains("-quit", args);
        Assert.Contains("-nographics", args);
        Assert.Contains("-accept-apiupdate", args);
        Assert.Contains("-executeMethod", args);
        Assert.Contains("BuildAutomation.IOSBuilder.Build", args);
    }

    [Fact]
    public void CreateBatchModeArgs_ContainsProjectPath()
    {
        BuildConfig config = CreateTestConfig();
        BuildPaths paths = BuildPaths.Create(config);

        List<string> args = UnityCommandBuilder.CreateBatchModeArgs(config, paths, "iOS");

        int index = args.IndexOf("-projectPath");
        Assert.True(index >= 0);
        Assert.Equal(paths.UnityProjectRoot, args[index + 1]);
    }

    [Fact]
    public void CreateBatchModeArgs_ContainsBuildTarget()
    {
        BuildConfig config = CreateTestConfig();
        BuildPaths paths = BuildPaths.Create(config);

        List<string> args = UnityCommandBuilder.CreateBatchModeArgs(config, paths, "Android");

        int index = args.IndexOf("-buildTarget");
        Assert.True(index >= 0);
        Assert.Equal("Android", args[index + 1]);
    }

    [Fact]
    public void AddBundleVersionArgs_SyncFromUnity_DoesNotAddBundleVersion()
    {
        BuildConfig config = CreateTestConfig();
        config.SyncBundleVersionFromUnity = true;
        List<string> args = [];
        UnityCommandBuilder.AddBundleVersionArgs(args, config, TestHelpers.CreateTestLogger());

        Assert.Contains("-customBuildNumber", args);
        Assert.DoesNotContain("-customBundleVersion", args);
    }

    [Fact]
    public void AddBundleVersionArgs_NoSync_AddsBundleVersion()
    {
        BuildConfig config = CreateTestConfig();
        config.SyncBundleVersionFromUnity = false;
        List<string> args = [];
        UnityCommandBuilder.AddBundleVersionArgs(args, config, TestHelpers.CreateTestLogger());

        Assert.Contains("-customBuildNumber", args);
        Assert.Contains("-customBundleVersion", args);
    }

    [Fact]
    public void AddPair_EmptyValue_DoesNotAdd()
    {
        List<string> args = [];
        UnityCommandBuilder.AddPair(args, "-key", "");
        Assert.Empty(args);
    }

    [Fact]
    public void AddPair_NullValue_DoesNotAdd()
    {
        List<string> args = [];
        UnityCommandBuilder.AddPair(args, "-key", null!);
        Assert.Empty(args);
    }

    [Fact]
    public void AddPair_ValidValue_AddsKeyAndValue()
    {
        List<string> args = [];
        UnityCommandBuilder.AddPair(args, "-key", "value");
        Assert.Equal(2, args.Count());
        Assert.Equal("-key", args[0]);
        Assert.Equal("value", args[1]);
    }
}
