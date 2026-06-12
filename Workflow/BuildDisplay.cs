namespace AutomationUnityBuildIOS;

internal static class BuildDisplay
{
    public static string BuildNumber(string buildNumber)
    {
        return string.IsNullOrWhiteSpace(buildNumber) ? "(空)" : buildNumber;
    }

    public static string BundleVersion(string bundleVersion)
    {
        return string.IsNullOrWhiteSpace(bundleVersion) ? "(空)" : bundleVersion;
    }
}
