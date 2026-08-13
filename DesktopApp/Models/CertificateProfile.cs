using DesktopApp.ViewModels;

namespace DesktopApp.Models;

public class CertificateProfile : ViewModelBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = "";
    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    private string _platform = "ios";
    /// <summary>
    /// 适用平台: ios / android / tiktok / all
    /// </summary>
    public string Platform
    {
        get => _platform;
        set => Set(ref _platform, value);
    }

    // ---- iOS ----
    private string _teamId = "";
    public string TeamId
    {
        get => _teamId;
        set => Set(ref _teamId, value);
    }

    private string _exportMethod = "development";
    public string ExportMethod
    {
        get => _exportMethod;
        set => Set(ref _exportMethod, value);
    }

    private string _iosDeploymentTarget = "";
    public string IosDeploymentTarget
    {
        get => _iosDeploymentTarget;
        set => Set(ref _iosDeploymentTarget, value);
    }

    private string _appStoreConnectApiKeyPath = "";
    public string AppStoreConnectApiKeyPath
    {
        get => _appStoreConnectApiKeyPath;
        set => Set(ref _appStoreConnectApiKeyPath, value);
    }

    private string _appStoreConnectApiKeyId = "";
    public string AppStoreConnectApiKeyId
    {
        get => _appStoreConnectApiKeyId;
        set => Set(ref _appStoreConnectApiKeyId, value);
    }

    private string _appStoreConnectApiIssuerId = "";
    public string AppStoreConnectApiIssuerId
    {
        get => _appStoreConnectApiIssuerId;
        set => Set(ref _appStoreConnectApiIssuerId, value);
    }

    private bool _appStoreConnectUploadEnabled;
    public bool AppStoreConnectUploadEnabled
    {
        get => _appStoreConnectUploadEnabled;
        set => Set(ref _appStoreConnectUploadEnabled, value);
    }

    // ---- Android ----
    private string _androidKeystoreName = "";
    public string AndroidKeystoreName
    {
        get => _androidKeystoreName;
        set => Set(ref _androidKeystoreName, value);
    }

    private string _androidKeystorePass = "";
    public string AndroidKeystorePass
    {
        get => _androidKeystorePass;
        set => Set(ref _androidKeystorePass, value);
    }

    private string _androidKeyaliasName = "";
    public string AndroidKeyaliasName
    {
        get => _androidKeyaliasName;
        set => Set(ref _androidKeyaliasName, value);
    }

    private string _androidKeyaliasPass = "";
    public string AndroidKeyaliasPass
    {
        get => _androidKeyaliasPass;
        set => Set(ref _androidKeyaliasPass, value);
    }

    private bool _googlePlayUploadEnabled;
    public bool GooglePlayUploadEnabled
    {
        get => _googlePlayUploadEnabled;
        set => Set(ref _googlePlayUploadEnabled, value);
    }

    private string _googlePlayPackageName = "";
    public string GooglePlayPackageName
    {
        get => _googlePlayPackageName;
        set => Set(ref _googlePlayPackageName, value);
    }

    private string _googlePlayServiceAccountJsonPath = "";
    public string GooglePlayServiceAccountJsonPath
    {
        get => _googlePlayServiceAccountJsonPath;
        set => Set(ref _googlePlayServiceAccountJsonPath, value);
    }

    private string _googlePlayTrack = "internal";
    public string GooglePlayTrack
    {
        get => _googlePlayTrack;
        set => Set(ref _googlePlayTrack, value);
    }

    // ---- TikTok ----
    private string _tiktokAppId = "";
    public string TiktokAppId
    {
        get => _tiktokAppId;
        set => Set(ref _tiktokAppId, value);
    }

    private string _tiktokAccessToken = "";
    public string TiktokAccessToken
    {
        get => _tiktokAccessToken;
        set => Set(ref _tiktokAccessToken, value);
    }

    private string _tiktokGameName = "";
    public string TiktokGameName
    {
        get => _tiktokGameName;
        set => Set(ref _tiktokGameName, value);
    }

    private string _tiktokApiEndpoint = "https://open-api.tiktokglobalshop.com";
    public string TiktokApiEndpoint
    {
        get => _tiktokApiEndpoint;
        set => Set(ref _tiktokApiEndpoint, value);
    }

    private bool _tiktokUploadEnabled;
    public bool TiktokUploadEnabled
    {
        get => _tiktokUploadEnabled;
        set => Set(ref _tiktokUploadEnabled, value);
    }

    // ---- Helper flags ----
    public bool IsIos => string.Equals(Platform, "ios", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(Platform, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsAndroid => string.Equals(Platform, "android", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(Platform, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsTiktok => string.Equals(Platform, "tiktok", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(Platform, "all", StringComparison.OrdinalIgnoreCase);

    public CertificateProfile Clone()
    {
        return new CertificateProfile
        {
            Id = Id,
            Name = Name,
            Platform = Platform,
            TeamId = TeamId,
            ExportMethod = ExportMethod,
            IosDeploymentTarget = IosDeploymentTarget,
            AppStoreConnectApiKeyPath = AppStoreConnectApiKeyPath,
            AppStoreConnectApiKeyId = AppStoreConnectApiKeyId,
            AppStoreConnectApiIssuerId = AppStoreConnectApiIssuerId,
            AppStoreConnectUploadEnabled = AppStoreConnectUploadEnabled,
            AndroidKeystoreName = AndroidKeystoreName,
            AndroidKeystorePass = AndroidKeystorePass,
            AndroidKeyaliasName = AndroidKeyaliasName,
            AndroidKeyaliasPass = AndroidKeyaliasPass,
            GooglePlayUploadEnabled = GooglePlayUploadEnabled,
            GooglePlayPackageName = GooglePlayPackageName,
            GooglePlayServiceAccountJsonPath = GooglePlayServiceAccountJsonPath,
            GooglePlayTrack = GooglePlayTrack,
            TiktokAppId = TiktokAppId,
            TiktokAccessToken = TiktokAccessToken,
            TiktokGameName = TiktokGameName,
            TiktokApiEndpoint = TiktokApiEndpoint,
            TiktokUploadEnabled = TiktokUploadEnabled
        };
    }

    public override string ToString() => string.IsNullOrEmpty(Name) ? "(未命名证书)" : Name;
}
