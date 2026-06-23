using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class PathSafetyTests
{
    [Fact]
    public void IsSameOrChildPath_SamePath_ReturnsTrue()
    {
        Assert.True(PathSafety.IsSameOrChildPath("/home/user/work", "/home/user/work"));
    }

    [Fact]
    public void IsSameOrChildPath_ChildPath_ReturnsTrue()
    {
        Assert.True(PathSafety.IsSameOrChildPath("/home/user/work/sub", "/home/user/work"));
    }

    [Fact]
    public void IsSameOrChildPath_UnrelatedPath_ReturnsFalse()
    {
        Assert.False(PathSafety.IsSameOrChildPath("/home/other/work", "/home/user/work"));
    }

    [Fact]
    public void IsSameOrChildPath_ParentPath_ReturnsFalse()
    {
        Assert.False(PathSafety.IsSameOrChildPath("/home/user", "/home/user/work"));
    }

    [Fact]
    public void IsSameOrChildPath_SiblingWithSimilarPrefix_ReturnsFalse()
    {
        Assert.False(PathSafety.IsSameOrChildPath("/home/userwork", "/home/user/work"));
    }

    [Fact]
    public void IsStrictChildPath_SamePath_ReturnsFalse()
    {
        Assert.False(PathSafety.IsStrictChildPath("/home/user/work", "/home/user/work"));
    }

    [Fact]
    public void IsStrictChildPath_ChildPath_ReturnsTrue()
    {
        Assert.True(PathSafety.IsStrictChildPath("/home/user/work/sub", "/home/user/work"));
    }

    [Fact]
    public void IsStrictChildPath_UnrelatedPath_ReturnsFalse()
    {
        Assert.False(PathSafety.IsStrictChildPath("/home/other/work", "/home/user/work"));
    }

    [Fact]
    public void IsFilesystemRoot_RootPath_ReturnsTrue()
    {
        Assert.True(PathSafety.IsFilesystemRoot("/"));
    }

    [Fact]
    public void IsFilesystemRoot_NonRootPath_ReturnsFalse()
    {
        Assert.False(PathSafety.IsFilesystemRoot("/home/user"));
    }

    [Fact]
    public void NormalizeDirectoryPath_AddsTrailingSeparator()
    {
        string result = PathSafety.NormalizeDirectoryPath("/home/user/work");
        Assert.True(result.EndsWith(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void NormalizePath_RemovesTrailingSeparator()
    {
        string result = PathSafety.NormalizePath("/home/user/work/");
        Assert.False(result.EndsWith(Path.DirectorySeparatorChar));
    }
}
