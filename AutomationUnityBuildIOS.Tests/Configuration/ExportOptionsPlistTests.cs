using Xunit;
namespace AutomationUnityBuildIOS.Tests;

public class ExportOptionsPlistTests
{
    [Fact]
    public void Write_GeneratesValidXmlPlist()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string path = Path.Combine(tempDir, "ExportOptions.plist");
            BuildConfig config = new()
            {
                ExportMethod = "development",
                TeamId = "ABCDE12345",
                SigningStyle = "automatic"
            };

            ExportOptionsPlist.Write(config, path);

            Assert.True(File.Exists(path));
            string content = File.ReadAllText(path);
            Assert.Contains("<?xml version=\"1.0\"", content);
            Assert.Contains("<plist version=\"1.0\">", content);
            Assert.Contains("<dict>", content);
            Assert.Contains("development", content);
            Assert.Contains("ABCDE12345", content);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void Write_WithProvisioningProfiles_IncludesDict()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string path = Path.Combine(tempDir, "ExportOptions.plist");
            BuildConfig config = new()
            {
                ExportMethod = "app-store",
                TeamId = "ABCDE12345",
                SigningStyle = "manual",
                ProvisioningProfiles = new Dictionary<string, string>
                {
                    ["com.company.game"] = "My Profile"
                }
            };

            ExportOptionsPlist.Write(config, path);

            string content = File.ReadAllText(path);
            Assert.Contains("provisioningProfiles", content);
            Assert.Contains("com.company.game", content);
            Assert.Contains("My Profile", content);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void Write_WithCompileBitcodeTrue_IncludesTrue()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string path = Path.Combine(tempDir, "ExportOptions.plist");
            BuildConfig config = new()
            {
                ExportMethod = "development",
                TeamId = "ABCDE12345",
                SigningStyle = "automatic",
                CompileBitcode = true
            };

            ExportOptionsPlist.Write(config, path);

            string content = File.ReadAllText(path);
            Assert.Contains("<true/>", content);
            Assert.Contains("compileBitcode", content);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void Write_WithNullCompileBitcode_OmitsField()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string path = Path.Combine(tempDir, "ExportOptions.plist");
            BuildConfig config = new()
            {
                ExportMethod = "development",
                TeamId = "ABCDE12345",
                SigningStyle = "automatic",
                CompileBitcode = null
            };

            ExportOptionsPlist.Write(config, path);

            string content = File.ReadAllText(path);
            Assert.DoesNotContain("compileBitcode", content);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void Write_EmptyFields_Omitted()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string path = Path.Combine(tempDir, "ExportOptions.plist");
            BuildConfig config = new()
            {
                ExportMethod = "development",
                TeamId = "",
                SigningStyle = ""
            };

            ExportOptionsPlist.Write(config, path);

            string content = File.ReadAllText(path);
            Assert.DoesNotContain("teamID", content);
            Assert.DoesNotContain("signingStyle", content);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void Write_WithDestination_IncludesDestination()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string path = Path.Combine(tempDir, "ExportOptions.plist");
            BuildConfig config = new()
            {
                ExportMethod = "app-store",
                TeamId = "ABCDE12345",
                SigningStyle = "automatic"
            };

            ExportOptionsPlist.Write(config, path, destination: "upload");

            string content = File.ReadAllText(path);
            Assert.Contains("upload", content);
            Assert.Contains("destination", content);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }
}
