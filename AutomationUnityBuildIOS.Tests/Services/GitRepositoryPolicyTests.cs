using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class GitRepositoryPolicyTests
{
    private static BuildLogger CreateLogger() => TestHelpers.CreateTestLogger();

    [Fact]
    public void Validate_ValidHttpsUrl_DoesNotThrow()
    {
        BuildConfig config = new()
        {
            RepositoryUrl = "https://github.com/company/game.git"
        };
        GitRepositoryPolicy.Validate(config, CreateLogger());
    }

    [Fact]
    public void Validate_ValidSshUrl_DoesNotThrow()
    {
        BuildConfig config = new()
        {
            RepositoryUrl = "git@github.com:company/game.git"
        };
        GitRepositoryPolicy.Validate(config, CreateLogger());
    }

    [Fact]
    public void Validate_UrlWithWhitespace_Throws()
    {
        BuildConfig config = new()
        {
            RepositoryUrl = "https://github.com/company/game.git with space"
        };
        Assert.Throws<InvalidOperationException>(() => GitRepositoryPolicy.Validate(config, CreateLogger()));
    }

    [Fact]
    public void Validate_UrlWithBrackets_Throws()
    {
        BuildConfig config = new()
        {
            RepositoryUrl = "https://github.com/company/[game].git"
        };
        Assert.Throws<InvalidOperationException>(() => GitRepositoryPolicy.Validate(config, CreateLogger()));
    }

    [Fact]
    public void Validate_InWhitelist_DoesNotThrow()
    {
        BuildConfig config = new()
        {
            RepositoryUrl = "https://github.com/company/game.git",
            AllowedRepositoryUrls = ["https://github.com/company/game.git"]
        };
        GitRepositoryPolicy.Validate(config, CreateLogger());
    }

    [Fact]
    public void Validate_NotInWhitelist_Throws()
    {
        BuildConfig config = new()
        {
            RepositoryUrl = "https://github.com/company/game.git",
            AllowedRepositoryUrls = ["https://github.com/company/other.git"]
        };
        Assert.Throws<InvalidOperationException>(() => GitRepositoryPolicy.Validate(config, CreateLogger()));
    }

    [Fact]
    public void Validate_EmptyWhitelist_DoesNotThrow()
    {
        BuildConfig config = new()
        {
            RepositoryUrl = "https://github.com/company/game.git",
            AllowedRepositoryUrls = []
        };
        GitRepositoryPolicy.Validate(config, CreateLogger());
    }

    [Fact]
    public void Validate_SshAndHttpsSameRepo_InWhitelist_DoesNotThrow()
    {
        BuildConfig config = new()
        {
            RepositoryUrl = "git@github.com:company/game.git",
            AllowedRepositoryUrls = ["https://github.com/company/game.git"]
        };
        GitRepositoryPolicy.Validate(config, CreateLogger());
    }
}
