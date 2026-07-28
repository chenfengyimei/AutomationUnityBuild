using Xunit;

namespace AutomationUnityBuildIOS.Tests;

public class TiktokBuildPlatformsTests
{
    [Fact]
    public void IsKnown_Tiktok_ReturnsTrue()
    {
        Assert.True(BuildPlatforms.IsKnown("tiktok"));
    }

    [Fact]
    public void IsKnown_TiktokCaseInsensitive_ReturnsTrue()
    {
        Assert.True(BuildPlatforms.IsKnown("TikTok"));
        Assert.True(BuildPlatforms.IsKnown("TIKTOK"));
        Assert.True(BuildPlatforms.IsKnown("Tiktok"));
    }
}
