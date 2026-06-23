using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class AndroidBuildFormatsTests
{
    [Fact]
    public void IsKnown_Apk_ReturnsTrue()
    {
        Assert.True(AndroidBuildFormats.IsKnown("apk"));
    }

    [Fact]
    public void IsKnown_Aab_ReturnsTrue()
    {
        Assert.True(AndroidBuildFormats.IsKnown("aab"));
    }

    [Fact]
    public void IsKnown_Both_ReturnsTrue()
    {
        Assert.True(AndroidBuildFormats.IsKnown("both"));
    }

    [Fact]
    public void IsKnown_CaseInsensitive_ReturnsTrue()
    {
        Assert.True(AndroidBuildFormats.IsKnown("APK"));
        Assert.True(AndroidBuildFormats.IsKnown("AAB"));
        Assert.True(AndroidBuildFormats.IsKnown("BOTH"));
    }

    [Fact]
    public void IsKnown_InvalidValue_ReturnsFalse()
    {
        Assert.False(AndroidBuildFormats.IsKnown("ipa"));
        Assert.False(AndroidBuildFormats.IsKnown(""));
        Assert.False(AndroidBuildFormats.IsKnown("unknown"));
    }

    [Fact]
    public void IncludesApk_Apk_ReturnsTrue()
    {
        Assert.True(AndroidBuildFormats.IncludesApk("apk"));
    }

    [Fact]
    public void IncludesApk_Both_ReturnsTrue()
    {
        Assert.True(AndroidBuildFormats.IncludesApk("both"));
    }

    [Fact]
    public void IncludesApk_Aab_ReturnsFalse()
    {
        Assert.False(AndroidBuildFormats.IncludesApk("aab"));
    }

    [Fact]
    public void IncludesAab_Aab_ReturnsTrue()
    {
        Assert.True(AndroidBuildFormats.IncludesAab("aab"));
    }

    [Fact]
    public void IncludesAab_Both_ReturnsTrue()
    {
        Assert.True(AndroidBuildFormats.IncludesAab("both"));
    }

    [Fact]
    public void IncludesAab_Apk_ReturnsFalse()
    {
        Assert.False(AndroidBuildFormats.IncludesAab("apk"));
    }
}
