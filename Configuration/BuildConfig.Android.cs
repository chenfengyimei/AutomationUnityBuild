namespace AutomationUnityBuildIOS;

internal sealed partial class BuildConfig
{
    private void ValidateAndroid()
    {
        if (!AndroidBuildFormats.IsKnown(AndroidBuildFormat))
        {
            throw new InvalidOperationException("配置 androidBuildFormat 必须是 apk、aab 或 both。");
        }

        if (string.Equals(UnityBuildMethod, DefaultUnityBuildMethods.Ios, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("buildPlatform=android 时 unityBuildMethod 不能使用 IOSBuilder。请改为 BuildAutomation.AndroidBuilder.Build。");
        }

        if (!string.IsNullOrWhiteSpace(BuildNumber) &&
            (!int.TryParse(BuildNumber, out int versionCode) || versionCode <= 0))
        {
            throw new InvalidOperationException("Android buildNumber/versionCode 必须是大于 0 的整数。");
        }

        ValidateOptionalInteger(AndroidMinSdkVersion, "androidMinSdkVersion");
        ValidateOptionalInteger(AndroidTargetSdkVersion, "androidTargetSdkVersion");

        if (!GooglePlayUploadEnabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(EffectiveGooglePlayPackageName()))
        {
            throw new InvalidOperationException("googlePlayUploadEnabled=true 时必须配置 googlePlayPackageName 或 bundleIdentifier。");
        }

        if (string.IsNullOrWhiteSpace(GooglePlayServiceAccountJsonPath))
        {
            throw new InvalidOperationException("googlePlayUploadEnabled=true 时必须配置 googlePlayServiceAccountJsonPath。");
        }

        if (!AndroidBuildFormats.IsKnown(GooglePlayUploadArtifact))
        {
            throw new InvalidOperationException("配置 googlePlayUploadArtifact 必须是 apk、aab 或 both。");
        }

        string[] statuses = ["draft", "inProgress", "halted", "completed"];
        if (!statuses.Contains(GooglePlayReleaseStatus, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("googlePlayReleaseStatus 必须是 draft、inProgress、halted 或 completed。");
        }

        if (GooglePlayUserFraction is <= 0 or > 1)
        {
            throw new InvalidOperationException("googlePlayUserFraction 必须大于 0 且小于等于 1。");
        }
    }

    private string DefaultUnityBuildMethod()
    {
        return IsAndroid ? DefaultUnityBuildMethods.Android : DefaultUnityBuildMethods.Ios;
    }

    private static void ValidateOptionalInteger(string value, string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(value) && !int.TryParse(value, out _))
        {
            throw new InvalidOperationException($"{fieldName} 必须是整数，例如 23、30、35。");
        }
    }
}
