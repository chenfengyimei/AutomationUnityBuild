using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class GooglePlayReleaseStatusTests
{
    [Fact]
    public void Normalize_Draft_ReturnsDraft()
    {
        Assert.Equal("draft", GooglePlayReleaseStatus.Normalize("draft"));
    }

    [Fact]
    public void Normalize_InProgress_ReturnsCamelCase()
    {
        Assert.Equal("inProgress", GooglePlayReleaseStatus.Normalize("inProgress"));
    }

    [Fact]
    public void Normalize_InProgressLowerCase_ReturnsCamelCase()
    {
        Assert.Equal("inProgress", GooglePlayReleaseStatus.Normalize("inprogress"));
    }

    [Fact]
    public void Normalize_Halted_ReturnsHalted()
    {
        Assert.Equal("halted", GooglePlayReleaseStatus.Normalize("halted"));
    }

    [Fact]
    public void Normalize_Completed_ReturnsCompleted()
    {
        Assert.Equal("completed", GooglePlayReleaseStatus.Normalize("completed"));
    }

    [Fact]
    public void Normalize_Unknown_ReturnsTrimmed()
    {
        Assert.Equal("unknown", GooglePlayReleaseStatus.Normalize("unknown"));
    }

    [Fact]
    public void Normalize_Null_ReturnsEmpty()
    {
        Assert.Equal("", GooglePlayReleaseStatus.Normalize(null!));
    }

    [Fact]
    public void Normalize_WithSpaces_TrimsAndNormalizes()
    {
        Assert.Equal("draft", GooglePlayReleaseStatus.Normalize("  draft  "));
    }
}
