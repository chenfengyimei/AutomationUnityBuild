using Xunit;

namespace AutomationUnityBuildIOS.Tests;

public class GitAuthFailureDetectorTests
{
    [Theory]
    [InlineData("命令执行失败(128): git clone\nfatal: Authentication failed for 'https://github.com/org/repo.git'")]
    [InlineData("命令执行失败(128): git fetch\nfatal: could not read Username for 'https://github.com': No such device or address")]
    [InlineData("命令执行失败(128): git clone\nremote: Invalid username or token.\nfatal: Authentication failed")]
    [InlineData("命令执行失败(128): git pull\nfatal: could not read Password for 'https://github.com': No such device or address")]
    [InlineData("命令执行失败(128): git clone\nPermission denied (publickey).")]
    [InlineData("命令执行失败(128): git fetch\nremote: Support for password authentication was removed.")]
    [InlineData("命令执行失败(128): git clone\nremote: Personal access tokens with read:org scope are required.")]
    public void IsAuthFailure_DetectsKnownPatterns(string message)
    {
        Assert.True(GitAuthFailureDetector.IsAuthFailure(message));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("命令执行失败(1): git checkout -b main")]
    [InlineData("命令执行失败(128): git clone\nfatal: destination path already exists")]
    [InlineData("命令执行失败(128): git pull\nerror: Your local changes would be overwritten")]
    [InlineData("命令执行失败(128): git fetch\nfatal: unable to auto-detect email address")]
    public void IsAuthFailure_RejectsNonAuthErrors(string message)
    {
        Assert.False(GitAuthFailureDetector.IsAuthFailure(message));
    }

    [Fact]
    public void ExtractStderrSummary_ReturnsStderrContent()
    {
        string message = "命令执行失败(128): git clone\nfatal: Authentication failed for 'https://github.com/org/repo.git'";
        string summary = GitAuthFailureDetector.ExtractStderrSummary(message);
        Assert.Contains("Authentication failed", summary);
        Assert.DoesNotContain("命令执行失败", summary);
    }

    [Fact]
    public void ExtractStderrSummary_HandlesMultiLineStderr()
    {
        string message = "命令执行失败(128): git clone\nremote: Invalid username or token.\nfatal: Authentication failed for 'https://github.com/org/repo.git'";
        string summary = GitAuthFailureDetector.ExtractStderrSummary(message);
        Assert.Contains("Invalid username or token", summary);
        Assert.Contains("Authentication failed", summary);
    }

    [Fact]
    public void ExtractStderrSummary_HandlesNoStderr()
    {
        string message = "命令执行失败(128): git clone";
        string summary = GitAuthFailureDetector.ExtractStderrSummary(message);
        Assert.NotEmpty(summary);
    }

    [Fact]
    public void ExtractStderrSummary_HandlesEmptyMessage()
    {
        string summary = GitAuthFailureDetector.ExtractStderrSummary("");
        Assert.NotEmpty(summary);
    }
}
