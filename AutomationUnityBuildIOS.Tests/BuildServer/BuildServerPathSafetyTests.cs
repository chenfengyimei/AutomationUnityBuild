using BuildServer;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.BuildServer;

public sealed class BuildServerPathSafetyTests
{
    [Theory]
    [InlineData("build-ios.json", true)]
    [InlineData("AuthKey_ABC123.p8", true)]
    [InlineData("..", false)]
    [InlineData("../secret.p8", false)]
    [InlineData("CON.json", false)]
    [InlineData("bad\\name.json", false)]
    [InlineData("bad/name.json", false)]
    public void IsPortableFileName_UsesCrossPlatformRules(string value, bool expected)
    {
        Assert.Equal(expected, BuildServerPathSafety.IsPortableFileName(value));
    }

    [Theory]
    [InlineData("C:\\Builds\\Game")]
    [InlineData("D:/Builds/Game")]
    [InlineData("/Users/build/Game")]
    [InlineData("\\\\server\\share\\Game")]
    [InlineData("//server/share/Game")]
    [InlineData("\\Builds\\Game")]
    public void IsAbsolutePathFromAnyPlatform_AllPlatformAbsolutePaths_ReturnTrue(string path)
    {
        Assert.True(BuildServerPathSafety.IsAbsolutePathFromAnyPlatform(path));
    }

    [Theory]
    [InlineData("C:relative")]
    [InlineData("relative/path")]
    [InlineData("~/portable/path")]
    [InlineData("")]
    public void IsAbsolutePathFromAnyPlatform_RelativeOrPortablePaths_ReturnFalse(string path)
    {
        Assert.False(BuildServerPathSafety.IsAbsolutePathFromAnyPlatform(path));
    }

    [Theory]
    [InlineData("C:\\Unity\\Editor\\Unity.exe")]
    [InlineData("D:/Unity/Editor/Unity.exe")]
    [InlineData("\\\\server\\share\\Unity.exe")]
    public void IsWindowsAbsolutePath_WindowsPaths_ReturnTrue(string path)
    {
        Assert.True(BuildServerPathSafety.IsWindowsAbsolutePath(path));
    }

    [Theory]
    [InlineData("/Applications/Unity/Unity")]
    [InlineData("relative/Unity")]
    [InlineData("~/Unity")]
    public void IsWindowsAbsolutePath_NonWindowsPaths_ReturnFalse(string path)
    {
        Assert.False(BuildServerPathSafety.IsWindowsAbsolutePath(path));
    }

    [Fact]
    public void ExpandHome_WindowsStyleNestedPath_UsesCurrentPlatformSeparators()
    {
        string result = BuildServerEnvironment.ExpandHome("~\\first\\second");
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "first",
            "second");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandHome_Whitespace_RemainsWhitespace()
    {
        Assert.Equal(" ", BuildServerEnvironment.ExpandHome(" "));
    }

    [Fact]
    public void IsSafeSameOrChild_RejectsDirectoryLinkEscapingRoot()
    {
        string allowedRoot = TestHelpers.CreateTempDir();
        string outsideRoot = TestHelpers.CreateTempDir();
        string linkPath = Path.Combine(allowedRoot, "outside-link");
        try
        {
            try
            {
                Directory.CreateSymbolicLink(linkPath, outsideRoot);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
            {
                return;
            }

            string candidate = Path.Combine(linkPath, "artifact.bin");
            Assert.True(BuildServerPathSafety.IsSameOrChild(candidate, allowedRoot));
            Assert.False(BuildServerPathSafety.IsSafeSameOrChild(candidate, allowedRoot));
        }
        finally
        {
            try
            {
                if (Directory.Exists(linkPath)) Directory.Delete(linkPath);
            }
            catch
            {
            }

            TestHelpers.CleanupTempDir(allowedRoot);
            TestHelpers.CleanupTempDir(outsideRoot);
        }
    }
}
