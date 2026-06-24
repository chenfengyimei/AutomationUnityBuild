using System.Text;
using BuildServer.Services;
using Xunit;

namespace AutomationUnityBuildIOS.Tests.BuildServer;

public sealed class LogFileReaderTests
{
    [Fact]
    public async Task ReadAll_AndTail_ReadLogWhileWriterKeepsFileOpen()
    {
        string root = Path.Combine(Path.GetTempPath(), $"log-reader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string logPath = Path.Combine(root, "worker.log");

        try
        {
            await using var stream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            await writer.WriteLineAsync("[STEP] START");
            await writer.WriteLineAsync("[STDOUT] Unity running");

            string full = LogFileReader.ReadAll(logPath);
            string tail = LogFileReader.Tail(logPath, 1);

            Assert.Contains("[STEP] START", full);
            Assert.Contains("[STDOUT] Unity running", full);
            Assert.DoesNotContain("[STEP] START", tail);
            Assert.Contains("[STDOUT] Unity running", tail);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
