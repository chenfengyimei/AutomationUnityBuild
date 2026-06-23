namespace AutomationUnityBuildIOS;

internal static class GooglePlayReleaseStatus
{
    public static string Normalize(string status)
    {
        return (status ?? "").Trim().ToLowerInvariant() switch
        {
            "draft" => "draft",
            "inprogress" => "inProgress",
            "halted" => "halted",
            "completed" => "completed",
            _ => (status ?? "").Trim()
        };
    }
}
