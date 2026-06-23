using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class GitRepositoryUrlTests
{
    [Fact]
    public void CanonicalKey_HttpsUrl_Normalizes()
    {
        string key = GitRepositoryUrl.CanonicalKey("https://github.com/company/game.git");
        Assert.Equal("github.com/company/game", key);
    }

    [Fact]
    public void CanonicalKey_SshUrl_NormalizesToSameAsHttps()
    {
        string sshKey = GitRepositoryUrl.CanonicalKey("git@github.com:company/game.git");
        string httpsKey = GitRepositoryUrl.CanonicalKey("https://github.com/company/game.git");
        Assert.Equal(httpsKey, sshKey);
    }

    [Fact]
    public void CanonicalKey_CaseInsensitive_SameKey()
    {
        string upper = GitRepositoryUrl.CanonicalKey("https://GitHub.com/Company/Game.git");
        string lower = GitRepositoryUrl.CanonicalKey("https://github.com/company/game.git");
        Assert.Equal(lower, upper);
    }

    [Fact]
    public void CanonicalKey_DifferentRepo_DifferentKey()
    {
        string key1 = GitRepositoryUrl.CanonicalKey("https://github.com/company/game.git");
        string key2 = GitRepositoryUrl.CanonicalKey("https://github.com/company/other.git");
        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void CanonicalKey_GitLabUrl_Normalizes()
    {
        string key = GitRepositoryUrl.CanonicalKey("https://gitlab.com/group/project.git");
        Assert.Equal("gitlab.com/group/project", key);
    }

    [Fact]
    public void Redact_UrlWithCredentials_ReplacesUserInfo()
    {
        string result = GitRepositoryUrl.Redact("https://user:pass@github.com/repo.git");
        Assert.Contains("***", result);
        Assert.DoesNotContain("user:pass", result);
    }

    [Fact]
    public void Redact_UrlWithoutCredentials_ReturnsAsIs()
    {
        string url = "https://github.com/company/game.git";
        string result = GitRepositoryUrl.Redact(url);
        Assert.Equal(url, result);
    }

    [Fact]
    public void Redact_SshUrl_ReturnsAsIs()
    {
        string url = "git@github.com:company/game.git";
        string result = GitRepositoryUrl.Redact(url);
        Assert.Equal(url, result);
    }

    [Fact]
    public void CanonicalKey_TrailingSlash_Normalized()
    {
        string key1 = GitRepositoryUrl.CanonicalKey("https://github.com/company/game.git/");
        string key2 = GitRepositoryUrl.CanonicalKey("https://github.com/company/game.git");
        Assert.Equal(key2, key1);
    }
}
