using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class PathToolsTests
{
    [Fact]
    public void ExpandHome_Tilde_ReturnsUserProfile()
    {
        string result = PathTools.ExpandHome("~");
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), result);
    }

    [Fact]
    public void ExpandHome_TildeSlashPath_ReturnsCombinedPath()
    {
        string result = PathTools.ExpandHome("~/subdir");
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "subdir");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandHome_TildeBackslashPath_ReturnsCombinedPath()
    {
        string result = PathTools.ExpandHome("~\\subdir");
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "subdir");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandHome_EmptyString_ReturnsEmptyString()
    {
        Assert.Equal("", PathTools.ExpandHome(""));
    }

    [Fact]
    public void ExpandHome_Null_ReturnsNull()
    {
        Assert.Null(PathTools.ExpandHome(null!));
    }

    [Fact]
    public void ExpandHome_AbsolutePath_ReturnsAsIs()
    {
        Assert.Equal("/usr/local/bin", PathTools.ExpandHome("/usr/local/bin"));
    }

    [Fact]
    public void EnsureParentDirectory_CreatesParentDir()
    {
        string tempBase = TestHelpers.CreateTempDir();
        try
        {
            string filePath = Path.Combine(tempBase, "sub", "file.txt");
            PathTools.EnsureParentDirectory(filePath);
            Assert.True(Directory.Exists(Path.Combine(tempBase, "sub")));
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempBase);
        }
    }

    [Fact]
    public void EnsureParentDirectory_RootPath_DoesNotThrow()
    {
        PathTools.EnsureParentDirectory("/file.txt");
    }
}
