using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class RuntimeConfigUpdaterTests
{
    [Fact]
    public void NextBuildNumber_Empty_Returns1()
    {
        Assert.Equal("1", RuntimeConfigUpdater.NextBuildNumber(""));
    }

    [Fact]
    public void NextBuildNumber_Whitespace_Returns1()
    {
        Assert.Equal("1", RuntimeConfigUpdater.NextBuildNumber("  "));
    }

    [Fact]
    public void NextBuildNumber_SimpleIncrement()
    {
        Assert.Equal("2", RuntimeConfigUpdater.NextBuildNumber("1"));
        Assert.Equal("11", RuntimeConfigUpdater.NextBuildNumber("10"));
        Assert.Equal("101", RuntimeConfigUpdater.NextBuildNumber("100"));
    }

    [Fact]
    public void NextBuildNumber_PreservesLeadingZeros()
    {
        Assert.Equal("010", RuntimeConfigUpdater.NextBuildNumber("009"));
        Assert.Equal("100", RuntimeConfigUpdater.NextBuildNumber("099"));
    }

    [Fact]
    public void NextBuildNumber_NonNumeric_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RuntimeConfigUpdater.NextBuildNumber("abc"));
    }

    [Fact]
    public void NextBuildNumber_MixedChars_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => RuntimeConfigUpdater.NextBuildNumber("1a"));
    }

    [Fact]
    public void NextBuildNumber_LargeNumber_Increments()
    {
        Assert.Equal("3000000001", RuntimeConfigUpdater.NextBuildNumber("3000000000"));
    }
}
