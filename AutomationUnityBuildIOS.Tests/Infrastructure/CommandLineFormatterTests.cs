using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class CommandLineFormatterTests
{
    [Fact]
    public void Format_SimpleCommand_ReturnsJoined()
    {
        string result = CommandLineFormatter.Format("git", ["clone", "url"]);
        Assert.Equal("git clone url", result);
    }

    [Fact]
    public void Format_QuotedFileName_ReturnsQuoted()
    {
        string result = CommandLineFormatter.Format("/path with spaces/unity", ["-batchmode"]);
        Assert.Contains("\"/path with spaces/unity\"", result);
    }

    [Fact]
    public void Format_EmptyArgs_ReturnsEmptyStrings()
    {
        string result = CommandLineFormatter.Format("git", ["", "clone"]);
        Assert.Contains("\"\"", result);
    }

    [Fact]
    public void Quote_NoSpaces_ReturnsAsIs()
    {
        string result = CommandLineFormatter.Format("test", ["nospace"]);
        Assert.Equal("test nospace", result);
    }

    [Fact]
    public void Quote_WithSpaces_ReturnsQuoted()
    {
        string result = CommandLineFormatter.Format("test", ["path with spaces"]);
        Assert.Contains("\"path with spaces\"", result);
    }

    [Fact]
    public void Quote_WithDoubleQuote_EscapesAndQuotes()
    {
        string result = CommandLineFormatter.Format("test", ["val\"ue"]);
        Assert.Contains("\\\"", result);
    }

    [Fact]
    public void Quote_WithBackslash_EscapesAndQuotes()
    {
        string result = CommandLineFormatter.Format("test", ["path\\to\\file with space"]);
        Assert.Contains("\\\\", result);
    }

    [Fact]
    public void Quote_EmptyString_ReturnsDoubleQuotes()
    {
        string result = CommandLineFormatter.Format("test", [""]);
        Assert.Contains("\"\"", result);
    }
}
