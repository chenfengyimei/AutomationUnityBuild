using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class ShortcutCommandsTests
{
    [Fact]
    public void IsShortcut_TwoDigitCode_ReturnsTrue()
    {
        Assert.True(ShortcutCommands.IsShortcut("01"));
        Assert.True(ShortcutCommands.IsShortcut("06"));
        Assert.True(ShortcutCommands.IsShortcut("11"));
    }

    [Fact]
    public void IsShortcut_SingleDigit_ReturnsTrue()
    {
        Assert.True(ShortcutCommands.IsShortcut("1"));
        Assert.True(ShortcutCommands.IsShortcut("6"));
    }

    [Fact]
    public void IsShortcut_HelpCode00_ReturnsTrue()
    {
        Assert.True(ShortcutCommands.IsShortcut("00"));
    }

    [Fact]
    public void IsShortcut_NonNumeric_ReturnsFalse()
    {
        Assert.False(ShortcutCommands.IsShortcut("abc"));
        Assert.False(ShortcutCommands.IsShortcut("run"));
    }

    [Fact]
    public void IsShortcut_TooLong_ReturnsFalse()
    {
        Assert.False(ShortcutCommands.IsShortcut("123"));
    }

    [Fact]
    public void IsShortcut_Empty_ReturnsFalse()
    {
        Assert.False(ShortcutCommands.IsShortcut(""));
        Assert.False(ShortcutCommands.IsShortcut(null!));
    }

    [Fact]
    public void IsShortcut_UnknownTwoDigit_ReturnsFalse()
    {
        Assert.False(ShortcutCommands.IsShortcut("99"));
        Assert.False(ShortcutCommands.IsShortcut("13"));
    }

    [Fact]
    public void TryNormalize_SingleDigit_PadsToTwo()
    {
        Assert.True(ShortcutCommands.IsShortcut("5"));
    }
}
