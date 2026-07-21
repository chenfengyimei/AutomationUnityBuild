namespace AutomationUnityBuildIOS;

internal static class BuildPlatforms
{
    public const string Ios = "ios";
    public const string Android = "android";
    public const string Tiktok = "tiktok";

    public static bool IsKnown(string value)
    {
        return string.Equals(value, Ios, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Android, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Tiktok, StringComparison.OrdinalIgnoreCase);
    }
}
