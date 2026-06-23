namespace AutomationUnityBuildIOS.Tests;

internal static class TestHelpers
{
    public static BuildLogger CreateTestLogger()
    {
        return BuildLogger.CreateForConsoleOnly(verbose: false);
    }

    public static string CreateTempDir()
    {
        string path = Path.Combine(Path.GetTempPath(), "aut_tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }

    public static void CleanupTempDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    public static string WriteTempConfig(string dir, string content)
    {
        string path = Path.Combine(dir, "test-config.json");
        File.WriteAllText(path, content);
        return path;
    }
}
