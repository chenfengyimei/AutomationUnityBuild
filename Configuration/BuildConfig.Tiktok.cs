namespace AutomationUnityBuildIOS;

internal sealed partial class BuildConfig
{
    public string TiktokAppId { get; set; } = "";
    public string TiktokAccessToken { get; set; } = "";
    public string TiktokGameName { get; set; } = "";
    public string TiktokWebglOutputDirectory { get; set; } = "";
    public bool TiktokUploadEnabled { get; set; }
    public string TiktokApiEndpoint { get; set; } = "https://open-api.tiktokglobalshop.com";

    private void ValidateTiktok()
    {
        if (string.Equals(UnityBuildMethod, DefaultUnityBuildMethods.Ios, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "buildPlatform=tiktok 时 unityBuildMethod 不能使用 IOSBuilder。请改为 BuildAutomation.TiktokBuilder.Build。");
        }

        if (string.Equals(UnityBuildMethod, DefaultUnityBuildMethods.Android, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "buildPlatform=tiktok 时 unityBuildMethod 不能使用 AndroidBuilder。请改为 BuildAutomation.TiktokBuilder.Build。");
        }

        if (TiktokUploadEnabled)
        {
            if (string.IsNullOrWhiteSpace(TiktokAppId))
            {
                throw new InvalidOperationException("tiktokUploadEnabled=true 时必须配置 tiktokAppId。");
            }

            if (string.IsNullOrWhiteSpace(TiktokAccessToken))
            {
                throw new InvalidOperationException("tiktokUploadEnabled=true 时必须配置 tiktokAccessToken。");
            }
        }
    }
}