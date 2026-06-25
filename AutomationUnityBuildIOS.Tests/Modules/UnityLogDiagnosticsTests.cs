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

    [Fact]
    public void TryGetKnownFailureMessage_WhenUnityLicenseMissing_ReturnsActionableMessage()
    {
        string[] lines =
        [
            "[Licensing::Module] Error: Access token is unavailable; failed to update",
            "[Licensing::Client] Error: Code 500 while processing request (status: Unable to update licenses. Errors: No ULF license found.,Token not found in cache)",
            "No valid Unity Editor license found. Please activate your license."
        ];

        string? message = UnityLogDiagnostics.TryGetKnownFailureMessage(lines);

        Assert.NotNull(message);
        Assert.Contains("Unity Editor License", message);
        Assert.Contains("激活", message);
        Assert.Contains("No valid Unity Editor license found", message);
    }
}
