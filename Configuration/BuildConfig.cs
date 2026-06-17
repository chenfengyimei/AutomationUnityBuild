using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomationUnityBuildIOS;

internal sealed class BuildConfig
{
    public string ConfigName { get; set; } = "";
    public string BuildPlatform { get; set; } = BuildPlatforms.Ios;
    public string RepositoryUrl { get; set; } = "";
    public List<string> AllowedRepositoryUrls { get; set; } = [];
    public string Branch { get; set; } = "main";
    public string WorkspaceRoot { get; set; } = "~/UnityBuildWorkspace";
    public List<string> AllowedWorkspaceRoots { get; set; } = [];
    public string ProjectDirectoryName { get; set; } = "";
    public string UnityProjectRelativePath { get; set; } = ".";

    public string UnityVersion { get; set; } = "";
    public string UnityExecutablePath { get; set; } = "";
    public string UnityBuildMethod { get; set; } = "";

    public string ArtifactsRoot { get; set; } = "~/UnityBuildArtifacts/UnityGame";
    public List<string> AllowedArtifactsRoots { get; set; } = [];
    public string XcodeOutputDirectory { get; set; } = "";
    public string ArchivePath { get; set; } = "";
    public string ExportPath { get; set; } = "";
    public string LogsDirectory { get; set; } = "";

    public string Scheme { get; set; } = "Unity-iPhone";
    public string Configuration { get; set; } = "Release";
    public string ExportMethod { get; set; } = "development";
    public string TeamId { get; set; } = "";
    public string SigningStyle { get; set; } = "automatic";
    public string ExportOptionsPlistPath { get; set; } = "";

    public string BundleIdentifier { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string BundleVersion { get; set; } = "";
    public bool SyncBundleVersionFromUnity { get; set; } = true;
    public string BuildNumber { get; set; } = "";
    public string IosDeploymentTarget { get; set; } = "";
    public bool AutoIncrementBuildNumber { get; set; } = true;

    public bool AllowProvisioningUpdates { get; set; } = true;
    public bool ResetRepository { get; set; }
    public bool PreserveUnityLibraryOnReset { get; set; } = true;
    public bool CleanXcodeOutputBeforeBuild { get; set; } = true;
    public bool UseWorkspaceIfPresent { get; set; } = true;
    public bool GenerateExportOptionsPlist { get; set; } = true;
    public bool CopyArchiveToOrganizer { get; set; } = true;
    public bool SaveConfigSnapshot { get; set; } = true;

    public bool? CompileBitcode { get; set; }
    public bool? UploadSymbols { get; set; } = true;

    public string AndroidBuildFormat { get; set; } = AndroidBuildFormats.Aab;
    public string AndroidOutputDirectory { get; set; } = "";
    public string ApkOutputPath { get; set; } = "";
    public string AabOutputPath { get; set; } = "";
    public string AndroidMinSdkVersion { get; set; } = "";
    public string AndroidTargetSdkVersion { get; set; } = "";
    public string AndroidKeystoreName { get; set; } = "";
    public string AndroidKeystorePass { get; set; } = "";
    public string AndroidKeyaliasName { get; set; } = "";
    public string AndroidKeyaliasPass { get; set; } = "";

    public bool GooglePlayUploadEnabled { get; set; }
    public string GooglePlayPackageName { get; set; } = "";
    public string GooglePlayServiceAccountJsonPath { get; set; } = "";
    public string GooglePlayTrack { get; set; } = "internal";
    public string GooglePlayReleaseStatus { get; set; } = "draft";
    public string GooglePlayReleaseName { get; set; } = "";
    public string GooglePlayUploadArtifact { get; set; } = AndroidBuildFormats.Aab;
    public bool GooglePlayChangesNotSentForReview { get; set; }
    public double? GooglePlayUserFraction { get; set; }

    public Dictionary<string, string> XcodeBuildSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Environment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> ProvisioningProfiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static BuildConfig Load(string configPath)
    {
        string fullPath = Path.GetFullPath(configPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"找不到配置文件 {fullPath}。可以先运行 init-config 生成模板。");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        BuildConfig? config = JsonSerializer.Deserialize<BuildConfig>(File.ReadAllText(fullPath), options);
        if (config is null)
        {
            throw new InvalidOperationException($"配置文件为空或格式不正确: {fullPath}");
        }

        config.NormalizeLoadedValues();
        config.Validate();
        return config;
    }

    private void NormalizeLoadedValues()
    {
        ConfigName ??= "";
        BuildPlatform = NormalizeChoice(BuildPlatform, BuildPlatforms.Ios);
        RepositoryUrl = ConfigValueNormalizer.NormalizeRepositoryUrl(RepositoryUrl ?? "");
        AllowedRepositoryUrls = NormalizeRepositoryList(AllowedRepositoryUrls);
        Branch ??= "";
        WorkspaceRoot ??= "";
        AllowedWorkspaceRoots = NormalizeStringList(AllowedWorkspaceRoots);
        ProjectDirectoryName ??= "";
        UnityProjectRelativePath ??= "";
        UnityVersion ??= "";
        UnityExecutablePath ??= "";
        UnityBuildMethod = string.IsNullOrWhiteSpace(UnityBuildMethod)
            ? DefaultUnityBuildMethod()
            : UnityBuildMethod.Trim();
        ArtifactsRoot ??= "";
        AllowedArtifactsRoots = NormalizeStringList(AllowedArtifactsRoots);
        XcodeOutputDirectory ??= "";
        ArchivePath ??= "";
        ExportPath ??= "";
        LogsDirectory ??= "";
        Scheme ??= "";
        Configuration ??= "";
        ExportMethod ??= "";
        TeamId ??= "";
        SigningStyle ??= "";
        ExportOptionsPlistPath ??= "";
        BundleIdentifier ??= "";
        ProductName ??= "";
        BundleVersion ??= "";
        BuildNumber ??= "";
        IosDeploymentTarget = (IosDeploymentTarget ?? "").Trim();
        AndroidBuildFormat = NormalizeChoice(AndroidBuildFormat, AndroidBuildFormats.Aab);
        AndroidOutputDirectory ??= "";
        ApkOutputPath ??= "";
        AabOutputPath ??= "";
        AndroidMinSdkVersion = (AndroidMinSdkVersion ?? "").Trim();
        AndroidTargetSdkVersion = (AndroidTargetSdkVersion ?? "").Trim();
        AndroidKeystoreName ??= "";
        AndroidKeystorePass ??= "";
        AndroidKeyaliasName ??= "";
        AndroidKeyaliasPass ??= "";
        GooglePlayPackageName ??= "";
        GooglePlayServiceAccountJsonPath ??= "";
        GooglePlayTrack = string.IsNullOrWhiteSpace(GooglePlayTrack) ? "internal" : GooglePlayTrack.Trim();
        GooglePlayReleaseStatus = string.IsNullOrWhiteSpace(GooglePlayReleaseStatus) ? "draft" : GooglePlayReleaseStatus.Trim();
        GooglePlayReleaseName ??= "";
        GooglePlayUploadArtifact = NormalizeChoice(GooglePlayUploadArtifact, AndroidBuildFormats.Aab);
        XcodeBuildSettings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Environment ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ProvisioningProfiles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> NormalizeRepositoryList(List<string>? values)
    {
        return NormalizeStringList(values)
            .Select(ConfigValueNormalizer.NormalizeRepositoryUrl)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> NormalizeStringList(List<string>? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private void Validate()
    {
        if (!BuildPlatforms.IsKnown(BuildPlatform))
        {
            throw new InvalidOperationException("配置 buildPlatform 必须是 ios 或 android。");
        }

        if (string.IsNullOrWhiteSpace(RepositoryUrl))
        {
            throw new InvalidOperationException("配置 repositoryUrl 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(Branch))
        {
            throw new InvalidOperationException("配置 branch 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(UnityBuildMethod))
        {
            throw new InvalidOperationException("配置 unityBuildMethod 不能为空。");
        }

        if (IsIos &&
            !string.IsNullOrWhiteSpace(TeamId) &&
            (TeamId.Length != 10 || !TeamId.All(char.IsLetterOrDigit)))
        {
            throw new InvalidOperationException("配置 teamId 必须是 10 位 Apple Developer Team ID，例如 ABCDE12345，不能填公司名。");
        }

        if (IsIos && !GenerateExportOptionsPlist && string.IsNullOrWhiteSpace(ExportOptionsPlistPath))
        {
            throw new InvalidOperationException("generateExportOptionsPlist=false 时必须配置 exportOptionsPlistPath。");
        }

        if (IsIos && !string.IsNullOrWhiteSpace(IosDeploymentTarget) && !Version.TryParse(IosDeploymentTarget, out _))
        {
            throw new InvalidOperationException("配置 iosDeploymentTarget 必须是版本号格式，例如 13.0 或 14.0。");
        }

        if (!SyncBundleVersionFromUnity && string.IsNullOrWhiteSpace(BundleVersion))
        {
            throw new InvalidOperationException("syncBundleVersionFromUnity=false 时必须配置 bundleVersion。");
        }

        if (IsAndroid)
        {
            ValidateAndroid();
        }
        else if (string.Equals(UnityBuildMethod, DefaultUnityBuildMethods.Android, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("buildPlatform=ios 时 unityBuildMethod 不能使用 AndroidBuilder。");
        }
    }

    public bool IsIos => string.Equals(BuildPlatform, BuildPlatforms.Ios, StringComparison.OrdinalIgnoreCase);
    public bool IsAndroid => string.Equals(BuildPlatform, BuildPlatforms.Android, StringComparison.OrdinalIgnoreCase);

    public bool ShouldBuildApk => AndroidBuildFormats.IncludesApk(AndroidBuildFormat);
    public bool ShouldBuildAab => AndroidBuildFormats.IncludesAab(AndroidBuildFormat);

    public string EffectiveGooglePlayPackageName()
    {
        return string.IsNullOrWhiteSpace(GooglePlayPackageName)
            ? BundleIdentifier.Trim()
            : GooglePlayPackageName.Trim();
    }

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

    private static string NormalizeChoice(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }
}

internal static class BuildPlatforms
{
    public const string Ios = "ios";
    public const string Android = "android";

    public static bool IsKnown(string value)
    {
        return string.Equals(value, Ios, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, Android, StringComparison.OrdinalIgnoreCase);
    }
}

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

internal static class DefaultUnityBuildMethods
{
    public const string Ios = "BuildAutomation.IOSBuilder.Build";
    public const string Android = "BuildAutomation.AndroidBuilder.Build";
}
