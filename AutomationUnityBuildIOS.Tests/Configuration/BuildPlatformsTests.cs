using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class BuildPlatformsTests
{
    [Fact]
    public void IsKnown_Ios_ReturnsTrue()
    {
        Assert.True(BuildPlatforms.IsKnown("ios"));
    }

    [Fact]
    public void IsKnown_Android_ReturnsTrue()
    {
        Assert.True(BuildPlatforms.IsKnown("android"));
    }

    [Fact]
    public void IsKnown_CaseInsensitive_ReturnsTrue()
    {
        Assert.True(BuildPlatforms.IsKnown("IOS"));
        Assert.True(BuildPlatforms.IsKnown("Android"));
    }

    [Fact]
    public void IsKnown_InvalidValue_ReturnsFalse()
    {
        Assert.False(BuildPlatforms.IsKnown("windows"));
        Assert.False(BuildPlatforms.IsKnown(""));
    }
}
