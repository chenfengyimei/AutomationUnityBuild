using BuildServer;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.BuildServer;

public sealed class BuildServerEnvironmentTests
{
    [Fact]
    public void Load_FilesystemRootDataDirectory_IsRejected()
    {
        string contentRoot = TestHelpers.CreateTempDir();
        try
        {
            string filesystemRoot = Path.GetPathRoot(Path.GetFullPath(Path.DirectorySeparatorChar.ToString()))!;
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BuildServer:DataRoot"] = filesystemRoot
                })
                .Build();

            Assert.Throws<InvalidOperationException>(() =>
                BuildServerEnvironment.Load(configuration, new TestWebHostEnvironment(contentRoot)));
        }
        finally
        {
            TestHelpers.CleanupTempDir(contentRoot);
        }
    }

    [Fact]
    public void Load_CaseDistinctAllowedRoots_FollowHostFilesystemSemantics()
    {
        string contentRoot = TestHelpers.CreateTempDir();
        try
        {
            string upper = Path.Combine(contentRoot, "Builds");
            string lower = Path.Combine(contentRoot, "builds");
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BuildServer:DataRoot"] = Path.Combine(contentRoot, "data"),
                    ["BuildServer:AllowedWorkspaceRoots:0"] = upper,
                    ["BuildServer:AllowedWorkspaceRoots:1"] = lower
                })
                .Build();

            BuildServerOptions options = BuildServerEnvironment.Load(
                configuration,
                new TestWebHostEnvironment(contentRoot));

            Assert.Equal(OperatingSystem.IsWindows() ? 1 : 2, options.AllowedWorkspaceRoots.Count);
        }
        finally
        {
            TestHelpers.CleanupTempDir(contentRoot);
        }
    }

    [Theory]
    [InlineData("AllowedWorkspaceRoots")]
    [InlineData("AllowedArtifactsRoots")]
    [InlineData("AllowedConfigRoots")]
    public void Load_FilesystemRootInAllowedRoots_IsRejected(string setting)
    {
        string contentRoot = TestHelpers.CreateTempDir();
        try
        {
            string filesystemRoot = Path.GetPathRoot(Path.GetFullPath(Path.DirectorySeparatorChar.ToString()))!;
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BuildServer:DataRoot"] = Path.Combine(contentRoot, "data"),
                    [$"BuildServer:{setting}:0"] = filesystemRoot
                })
                .Build();

            Assert.Throws<InvalidOperationException>(() =>
                BuildServerEnvironment.Load(configuration, new TestWebHostEnvironment(contentRoot)));
        }
        finally
        {
            TestHelpers.CleanupTempDir(contentRoot);
        }
    }
}
