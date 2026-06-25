using Xunit;

namespace AutomationUnityBuildIOS.Tests;

public class UnityLogDiagnosticsTests
{
    [Fact]
    public void LogTail_WhenUnityLogIsLocked_DoesNotThrow()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string logPath = Path.Combine(tempDir, "unity-editor.log");
            File.WriteAllText(logPath, "error: unity failed");
            using FileStream _ = new(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            UnityLogDiagnostics diagnostics = new(TestHelpers.CreateTestLogger());

            Exception? exception = Record.Exception(() => diagnostics.LogTail(logPath, "Unity Editor", 20));

            Assert.Null(exception);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void LogMatchingLogLines_WhenUnityLogCanBeShared_DoesNotThrow()
    {
        string tempDir = TestHelpers.CreateTempDir();
        try
        {
            string logPath = Path.Combine(tempDir, "unity-editor.log");
            File.WriteAllText(logPath, "line 1" + Environment.NewLine + "CommandInvokationFailure: Gradle failed");
            using FileStream _ = new(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            UnityLogDiagnostics diagnostics = new(TestHelpers.CreateTestLogger());

            Exception? exception = Record.Exception(() => diagnostics.LogMatchingLogLines(logPath, "Unity Editor", ["Gradle"]));

            Assert.Null(exception);
        }
        finally
        {
            TestHelpers.CleanupTempDir(tempDir);
        }
    }
}
