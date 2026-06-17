namespace AutomationUnityBuildIOS;

internal static class AndroidBuildFormats
{
    public const string Apk = "apk";
    public const string Aab = "aab";
    public const string Both = "both";

    public static bool IsKnown(string value)
    {
        return string.Equals(value, Apk, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Aab, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Both, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IncludesApk(string value)
    {
        return string.Equals(value, Apk, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Both, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IncludesAab(string value)
    {
        return string.Equals(value, Aab, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Both, StringComparison.OrdinalIgnoreCase);
    }
}
