using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class SensitiveTextTests
{
    [Fact]
    public void Redact_NullOrEmpty_ReturnsAsIs()
    {
        Assert.Equal("", SensitiveText.Redact(""));
        Assert.Null(SensitiveText.Redact(null!));
    }

    [Fact]
    public void Redact_UrlCredential_ReplacesWithStars()
    {
        string result = SensitiveText.Redact("https://user:pass@github.com/repo.git");
        Assert.Contains("***@", result);
        Assert.DoesNotContain("pass", result);
    }

    [Fact]
    public void Redact_GitHubToken_ReplacesWithStars()
    {
        string result = SensitiveText.Redact("token ghp_1234567890abcdefghijklm");
        Assert.Contains("***", result);
        Assert.DoesNotContain("ghp_1234567890abcdefghijklm", result);
    }

    [Fact]
    public void Redact_GitHubPatToken_ReplacesWithStars()
    {
        string result = SensitiveText.Redact("github_pat_1234567890abcdefghijklm");
        Assert.Contains("***", result);
    }

    [Fact]
    public void Redact_BearerToken_ReplacesWithStars()
    {
        string result = SensitiveText.Redact("Authorization: Bearer abcdefghijklmnopqrstuvwxyz12");
        Assert.Contains("Bearer ***", result);
        Assert.DoesNotContain("abcdefghijklmnopqrstuvwxyz12", result);
    }

    [Fact]
    public void Redact_PasswordKeyValue_ReplacesValue()
    {
        string result = SensitiveText.Redact("password=mysecret123");
        Assert.Contains("password=***", result);
        Assert.DoesNotContain("mysecret123", result);
    }

    [Fact]
    public void Redact_ApiKeyKeyValue_ReplacesValue()
    {
        string result = SensitiveText.Redact("api_key=sk-1234567890abcdef");
        Assert.Contains("***", result);
        Assert.DoesNotContain("sk-1234567890abcdef", result);
    }

    [Fact]
    public void Redact_UnityKeystorePassArg_ReplacesValue()
    {
        string result = SensitiveText.Redact("-customAndroidKeystorePass secret123");
        Assert.Contains("-customAndroidKeystorePass ***", result);
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void Redact_NormalText_ReturnsUnchanged()
    {
        string result = SensitiveText.Redact("git clone --branch main https://github.com/company/game.git");
        Assert.Contains("github.com/company/game.git", result);
    }

    [Fact]
    public void Redact_MultipleSecrets_AllRedacted()
    {
        string result = SensitiveText.Redact("password=abc token=ghp_1234567890abcdefghijklmn");
        Assert.DoesNotContain("abc", result);
        Assert.DoesNotContain("ghp_1234567890abcdefghijklmn", result);
    }
}
