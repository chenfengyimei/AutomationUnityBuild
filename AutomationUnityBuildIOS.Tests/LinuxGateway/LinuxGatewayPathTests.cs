using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using LinuxGateway;
using LinuxGateway.Services;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.LinuxGatewayTests;

public sealed class LinuxGatewayPathTests
{
    [Theory]
    [InlineData("~//first/second")]
    [InlineData("~\\\\first\\second")]
    public void ExpandHome_MixedOrRepeatedSeparators_UsesCurrentPlatformSeparators(string path)
    {
        string result = LinuxGatewayOptions.ExpandHome(path);
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "first",
            "second");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandHome_Whitespace_RemainsWhitespace()
    {
        Assert.Equal(" ", LinuxGatewayOptions.ExpandHome(" "));
    }

    [Theory]
    [InlineData("../release.tar.gz")]
    [InlineData("..\\release.tar.gz")]
    [InlineData("nested/release.tar.gz")]
    [InlineData("release:bad.tar.gz")]
    public void SafeDownloadFileName_RejectsUnsafeRemoteAssetNames(string fileName)
    {
        Assert.Throws<InvalidOperationException>(() => SelfUpdateService.SafeDownloadFileName(fileName));
    }

    [Fact]
    public void EnsureArchiveEntryStaysUnderDestination_RejectsTraversal()
    {
        string destination = TestHelpers.CreateTempDir();
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                SelfUpdateService.EnsureArchiveEntryStaysUnderDestination("../outside.txt", destination));
            Assert.Throws<InvalidOperationException>(() =>
                SelfUpdateService.EnsureArchiveEntryStaysUnderDestination("..\\outside.txt", destination));
            Assert.Throws<InvalidOperationException>(() =>
                SelfUpdateService.EnsureArchiveEntryStaysUnderDestination("folder/new\nline.txt", destination));
        }
        finally
        {
            TestHelpers.CleanupTempDir(destination);
        }
    }

    [Fact]
    public void ExtractTarGz_SafeArchive_ExtractsWithoutExternalTarCommand()
    {
        string root = TestHelpers.CreateTempDir();
        string archivePath = Path.Combine(root, "update.tar.gz");
        string destination = Path.Combine(root, "staging");
        Directory.CreateDirectory(destination);
        try
        {
            using (FileStream archive = File.Create(archivePath))
            using (var gzip = new GZipStream(archive, CompressionMode.Compress))
            using (var writer = new TarWriter(gzip))
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, "bin/version.txt")
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes("v1"))
                };
                writer.WriteEntry(entry);
            }

            SelfUpdateService.ExtractTarGz(archivePath, destination);

            Assert.Equal("v1", File.ReadAllText(Path.Combine(destination, "bin", "version.txt")));
        }
        finally
        {
            TestHelpers.CleanupTempDir(root);
        }
    }

    [Fact]
    public void ExtractTarGz_SymbolicLinkEntry_IsRejected()
    {
        string root = TestHelpers.CreateTempDir();
        string archivePath = Path.Combine(root, "update.tar.gz");
        string destination = Path.Combine(root, "staging");
        Directory.CreateDirectory(destination);
        try
        {
            using (FileStream archive = File.Create(archivePath))
            using (var gzip = new GZipStream(archive, CompressionMode.Compress))
            using (var writer = new TarWriter(gzip))
            {
                var entry = new PaxTarEntry(TarEntryType.SymbolicLink, "bin/current")
                {
                    LinkName = "../../outside"
                };
                writer.WriteEntry(entry);
            }

            Assert.Throws<InvalidOperationException>(() =>
                SelfUpdateService.ExtractTarGz(archivePath, destination));
        }
        finally
        {
            TestHelpers.CleanupTempDir(root);
        }
    }

    [Fact]
    public void EnsureNoReparsePointsBelowRoot_RejectsTraversalAndDirectoryLinks()
    {
        string root = TestHelpers.CreateTempDir();
        string outsideRoot = TestHelpers.CreateTempDir();
        string linkPath = Path.Combine(root, "outside-link");
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                SelfUpdateService.EnsureNoReparsePointsBelowRoot(
                    Path.Combine(root, "..", "outside.txt"),
                    root));

            try
            {
                Directory.CreateSymbolicLink(linkPath, outsideRoot);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
            {
                return;
            }

            Assert.Throws<InvalidOperationException>(() =>
                SelfUpdateService.EnsureNoReparsePointsBelowRoot(
                    Path.Combine(linkPath, "file.txt"),
                    root));
        }
        finally
        {
            try
            {
                if (Directory.Exists(linkPath)) Directory.Delete(linkPath);
            }
            catch
            {
            }

            TestHelpers.CleanupTempDir(root);
            TestHelpers.CleanupTempDir(outsideRoot);
        }
    }
}
