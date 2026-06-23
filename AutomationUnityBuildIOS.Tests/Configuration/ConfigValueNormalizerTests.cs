using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class ConfigValueNormalizerTests
{
    [Fact]
    public void NormalizeRepositoryUrl_GitHubHttps_AddsGitSuffix()
    {
        string result = ConfigValueNormalizer.NormalizeRepositoryUrl("https://github.com/company/game");
        Assert.Equal("https://github.com/company/game.git", result);
    }

    [Fact]
    public void NormalizeRepositoryUrl_GitHubHttpsAlreadyGit_KeepsAsIs()
    {
        string result = ConfigValueNormalizer.NormalizeRepositoryUrl("https://github.com/company/game.git");
        Assert.Equal("https://github.com/company/game.git", result);
    }

    [Fact]
    public void NormalizeRepositoryUrl_SshGitHub_AddsGitSuffix()
    {
        string result = ConfigValueNormalizer.NormalizeRepositoryUrl("git@github.com:company/game");
        Assert.Equal("git@github.com:company/game.git", result);
    }

    [Fact]
    public void NormalizeRepositoryUrl_MarkdownLink_ExtractsUrl()
    {
        string result = ConfigValueNormalizer.NormalizeRepositoryUrl("see [repo](https://github.com/company/game.git)");
        Assert.Equal("https://github.com/company/game.git", result);
    }

    [Fact]
    public void NormalizeRepositoryUrl_WithQueryStripsQuery()
    {
        string result = ConfigValueNormalizer.NormalizeRepositoryUrl("https://github.com/company/game.git?tab=readme");
        Assert.Equal("https://github.com/company/game.git", result);
    }

    [Fact]
    public void NormalizeRepositoryUrl_WithFragmentStripsFragment()
    {
        string result = ConfigValueNormalizer.NormalizeRepositoryUrl("https://github.com/company/game.git#section");
        Assert.Equal("https://github.com/company/game.git", result);
    }

    [Fact]
    public void NormalizeRepositoryUrl_TrailingSlash_Removed()
    {
        string result = ConfigValueNormalizer.NormalizeRepositoryUrl("https://github.com/company/game.git/");
        Assert.Equal("https://github.com/company/game.git", result);
    }

    [Fact]
    public void NormalizeRepositoryUrl_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", ConfigValueNormalizer.NormalizeRepositoryUrl(""));
    }
}
