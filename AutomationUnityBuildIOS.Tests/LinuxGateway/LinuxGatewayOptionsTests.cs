using LinuxGateway;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.LinuxGatewayTests;

public sealed class LinuxGatewayOptionsTests
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
                    ["LinuxGateway:DataRoot"] = filesystemRoot
                })
                .Build();

            Assert.Throws<InvalidOperationException>(() =>
                LinuxGatewayOptions.Load(configuration, new TestWebHostEnvironment(contentRoot)));
        }
        finally
        {
            TestHelpers.CleanupTempDir(contentRoot);
        }
    }
}
