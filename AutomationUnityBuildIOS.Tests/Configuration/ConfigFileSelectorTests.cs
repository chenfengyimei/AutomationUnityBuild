using Xunit;

namespace AutomationUnityBuildIOS.Tests;

public sealed class ConfigFileSelectorTests
{
    [Fact]
    public void FindConfigFiles_CustomRoot_DoesNotDependOnProcessCurrentDirectory()
    {
        string root = TestHelpers.CreateTempDir();
        try
        {
            string configsDirectory = Path.Combine(root, "configs");
            Directory.CreateDirectory(configsDirectory);
            string configPath = Path.Combine(configsDirectory, "build.json");
            File.WriteAllText(configPath, "{\"configName\":\"custom-root\"}");

            ConfigFileEntry entry = Assert.Single(ConfigFileSelector.FindConfigFiles(root));

            Assert.Equal(Path.GetFullPath(configPath), entry.FullPath);
            Assert.Equal("custom-root", entry.DisplayName);
        }
        finally
        {
            TestHelpers.CleanupTempDir(root);
        }
    }

    [Fact]
    public void FindConfigFiles_CaseDistinctPaths_FollowHostFilesystemSemantics()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = TestHelpers.CreateTempDir();
        try
        {
            string configsDirectory = Path.Combine(root, "configs");
            Directory.CreateDirectory(configsDirectory);
            File.WriteAllText(Path.Combine(configsDirectory, "build.json"), "{}");
            File.WriteAllText(Path.Combine(configsDirectory, "Build.json"), "{}");

            Assert.Equal(2, ConfigFileSelector.FindConfigFiles(root).Count);
        }
        finally
        {
            TestHelpers.CleanupTempDir(root);
        }
    }
}
