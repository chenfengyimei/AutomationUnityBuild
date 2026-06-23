using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void Split_Null_ReturnsEmpty()
    {
        Assert.Empty(CommandLineParser.Split(null));
    }

    [Fact]
    public void Split_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(CommandLineParser.Split(""));
    }

    [Fact]
    public void Split_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Empty(CommandLineParser.Split("   "));
    }

    [Fact]
    public void Split_SimpleArgs_SplitsBySpace()
    {
        string[] result = CommandLineParser.Split("run --config test.json");
        Assert.Equal(3, result.Count());
        Assert.Equal("run", result[0]);
        Assert.Equal("--config", result[1]);
        Assert.Equal("test.json", result[2]);
    }

    [Fact]
    public void Split_QuotedArgWithSpaces_KeepsTogether()
    {
        string[] result = CommandLineParser.Split("run --config \"path with spaces.json\"");
        Assert.Equal(3, result.Count());
        Assert.Equal("path with spaces.json", result[2]);
    }

    [Fact]
    public void Split_MultipleQuotedArgs_KeepsEachTogether()
    {
        string[] result = CommandLineParser.Split("\"arg one\" \"arg two\"");
        Assert.Equal(2, result.Count());
        Assert.Equal("arg one", result[0]);
        Assert.Equal("arg two", result[1]);
    }

    [Fact]
    public void Split_TrailingSpaces_NoEmptyArgs()
    {
        string[] result = CommandLineParser.Split("run --dry-run   ");
        Assert.Equal(2, result.Count());
    }
}
