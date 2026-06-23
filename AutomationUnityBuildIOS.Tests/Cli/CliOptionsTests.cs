using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class CliOptionsTests
{
    [Fact]
    public void Parse_ConfigPath_SetsValue()
    {
        CliOptions options = CliOptions.Parse(["--config", "my.json"]);
        Assert.Equal("my.json", options.ConfigPath);
        Assert.True(options.ConfigWasSpecified);
    }

    [Fact]
    public void Parse_ShortConfig_SetsValue()
    {
        CliOptions options = CliOptions.Parse(["-c", "my.json"]);
        Assert.Equal("my.json", options.ConfigPath);
        Assert.True(options.ConfigWasSpecified);
    }

    [Fact]
    public void Parse_DryRun_SetsTrue()
    {
        CliOptions options = CliOptions.Parse(["--dry-run"]);
        Assert.True(options.DryRun);
    }

    [Fact]
    public void Parse_Force_SetsTrue()
    {
        CliOptions options = CliOptions.Parse(["--force"]);
        Assert.True(options.Force);
    }

    [Fact]
    public void Parse_SkipGit_SetsTrue()
    {
        CliOptions options = CliOptions.Parse(["--skip-git"]);
        Assert.True(options.SkipGit);
    }

    [Fact]
    public void Parse_SkipUnity_SetsTrue()
    {
        CliOptions options = CliOptions.Parse(["--skip-unity"]);
        Assert.True(options.SkipUnity);
    }

    [Fact]
    public void Parse_SkipXcode_SetsTrue()
    {
        CliOptions options = CliOptions.Parse(["--skip-xcode"]);
        Assert.True(options.SkipXcode);
    }

    [Fact]
    public void Parse_AllowNonMac_SetsTrue()
    {
        CliOptions options = CliOptions.Parse(["--allow-non-mac"]);
        Assert.True(options.AllowNonMac);
    }

    [Fact]
    public void Parse_Verbose_SetsTrue()
    {
        CliOptions options = CliOptions.Parse(["--verbose"]);
        Assert.True(options.Verbose);
    }

    [Fact]
    public void Parse_Template_SetsTrue()
    {
        CliOptions options = CliOptions.Parse(["--template"]);
        Assert.True(options.Template);
    }

    [Fact]
    public void Parse_Platform_SetsValue()
    {
        CliOptions options = CliOptions.Parse(["--platform", "android"]);
        Assert.Equal("android", options.TemplatePlatform);
    }

    [Fact]
    public void Parse_InvalidPlatform_Throws()
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--platform", "windows"]));
    }

    [Fact]
    public void Parse_ConfigWithoutValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--config"]));
    }

    [Fact]
    public void Parse_UnknownArg_Throws()
    {
        Assert.Throws<ArgumentException>(() => CliOptions.Parse(["--unknown"]));
    }

    [Fact]
    public void Parse_NoArgs_ReturnsDefaults()
    {
        CliOptions options = CliOptions.Parse([]);
        Assert.False(options.DryRun);
        Assert.False(options.Force);
        Assert.False(options.ConfigWasSpecified);
        Assert.Equal(BuildPlatforms.Ios, options.TemplatePlatform);
    }
}
