using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutomationUnityBuildIOS;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using DesktopApp.Models;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class ConfigItem : ViewModelBase
{
    public string FullPath { get => _fullPath; set => Set(ref _fullPath, value); }
    private string _fullPath = "";
    public string DisplayName { get; set; } = "";
    public string DisplayPath { get; set; } = "";
    private string _platform = "";
    public string Platform { get => _platform; set => Set(ref _platform, value); }
    private string _configName = "";
    public string ConfigName { get => _configName; set => Set(ref _configName, value); }
    public string RepositoryUrl { get; set; } = "";
    public string Branch { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string BundleIdentifier { get; set; } = "";
    public string BuildNumber { get; set; } = "";
    public string BundleVersion { get; set; } = "";
    public string UnityVersion { get; set; } = "";
    public string UnityExecutablePath { get; set; } = "";
    public string UnityBuildMethod { get; set; } = "";
    public string WorkspaceRoot { get; set; } = "";
    public string ArtifactsRoot { get; set; } = "";
    public string ProjectDirectoryName { get; set; } = "";
    public string UnityProjectRelativePath { get; set; } = "";
    public string RawJson { get; set; } = "";

    // iOS fields
    public string TeamId { get; set; } = "";
    public string ExportMethod { get; set; } = "";
    public string IosDeploymentTarget { get; set; } = "";
    public string AppStoreConnectApiKeyPath { get; set; } = "";
    public string AppStoreConnectApiKeyId { get; set; } = "";
    public string AppStoreConnectApiIssuerId { get; set; } = "";
    public bool AppStoreConnectUploadEnabled { get; set; }

    // Android fields
    public string AndroidBuildFormat { get; set; } = "";
    public string AndroidKeystoreName { get; set; } = "";
    public string AndroidKeystorePass { get; set; } = "";
    public string AndroidKeyaliasName { get; set; } = "";
    public string AndroidKeyaliasPass { get; set; } = "";
    public bool GooglePlayUploadEnabled { get; set; }
    public string GooglePlayPackageName { get; set; } = "";
    public string GooglePlayServiceAccountJsonPath { get; set; } = "";
    public string GooglePlayTrack { get; set; } = "";

    // TikTok fields
    public string TiktokAppId { get; set; } = "";
    public string TiktokAccessToken { get; set; } = "";
    public string TiktokGameName { get; set; } = "";
    public string TiktokWebglOutputDirectory { get; set; } = "";
    public bool TiktokUploadEnabled { get; set; }
    public string TiktokApiEndpoint { get; set; } = "";

    public string DisplayText => $"{DisplayName} ({DisplayPath})";
}

public class ConfigPageViewModel : ViewModelBase
{
    private ConfigItem? _selectedConfig;
    private string _statusMessage = "";
    private bool _isEditing;
    private bool _isNewConfig;

    public ObservableCollection<ConfigItem> Configs { get; } = new();

    // Editable working copy
    private ConfigItem _editConfig = new();
    public ConfigItem EditConfig
    {
        get => _editConfig;
        set
        {
            if (_editConfig is not null)
                _editConfig.PropertyChanged -= OnEditConfigPropertyChanged;
            Set(ref _editConfig, value);
            if (_editConfig is not null)
                _editConfig.PropertyChanged += OnEditConfigPropertyChanged;
            RaisePlatformFlags();
        }
    }

    private void OnEditConfigPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConfigItem.Platform))
        {
            RaisePlatformFlags();
            SyncFileName();
        }
        else if (e.PropertyName == nameof(ConfigItem.ConfigName))
        {
            SyncFileName();
        }
    }

    private void SyncFileName()
    {
        if (!IsNewConfig) return;
        string platform = EditConfig.Platform ?? "ios";
        string name = string.IsNullOrWhiteSpace(EditConfig.ConfigName) ? "" : EditConfig.ConfigName.Trim();
        string fileName = string.IsNullOrEmpty(name)
            ? $"build-{platform}.json"
            : $"build-{platform}.{name}.json";
        string dir = Path.GetDirectoryName(EditConfig.FullPath);
        if (string.IsNullOrEmpty(dir)) dir = Environment.CurrentDirectory;
        EditConfig.FullPath = Path.Combine(dir, fileName);
    }

    private void RaisePlatformFlags()
    {
        Raise(nameof(IsEditIos));
        Raise(nameof(IsEditAndroid));
        Raise(nameof(IsEditTiktok));
    }

    public ConfigItem? SelectedConfig
    {
        get => _selectedConfig;
        set
        {
            Set(ref _selectedConfig, value);
            if (value is not null) LoadConfigDetails(value);
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    public bool IsNewConfig
    {
        get => _isNewConfig;
        set => Set(ref _isNewConfig, value);
    }

    public bool IsEditIos => string.Equals(EditConfig.Platform, "ios", StringComparison.OrdinalIgnoreCase);
    public bool IsEditAndroid => string.Equals(EditConfig.Platform, "android", StringComparison.OrdinalIgnoreCase);
    public bool IsEditTiktok => string.Equals(EditConfig.Platform, "tiktok", StringComparison.OrdinalIgnoreCase);

    // 模板应用后隐藏对应字段区域
    private bool _hasProjectApplied;
    public bool HasProjectApplied { get => _hasProjectApplied; set => Set(ref _hasProjectApplied, value); }

    private bool _hasUnityApplied;
    public bool HasUnityApplied { get => _hasUnityApplied; set => Set(ref _hasUnityApplied, value); }

    private bool _hasSigningApplied;
    public bool HasSigningApplied { get => _hasSigningApplied; set => Set(ref _hasSigningApplied, value); }

    private bool _hasCertApplied;
    public bool HasCertApplied { get => _hasCertApplied; set => Set(ref _hasCertApplied, value); }

    // 反转绑定用
    public bool ShowProjectFields => !HasProjectApplied;
    public bool ShowUnityFields => !HasUnityApplied;
    public bool ShowSigningFields => !HasSigningApplied;
    public bool ShowCertFields => !HasCertApplied;

    private void ResetTemplateFlags()
    {
        HasProjectApplied = false;
        HasUnityApplied = false;
        HasSigningApplied = false;
        HasCertApplied = false;
        Raise(nameof(ShowProjectFields));
        Raise(nameof(ShowUnityFields));
        Raise(nameof(ShowSigningFields));
        Raise(nameof(ShowCertFields));
    }

    // ---- Profile selection ----
    public ObservableCollection<ProjectProfile> AvailableProjects { get; } = new();
    public ObservableCollection<UnityProfile> AvailableUnityProfiles { get; } = new();
    public ObservableCollection<SigningProfile> AvailableSigningProfiles { get; } = new();
    public ObservableCollection<CertificateProfile> AvailableCertificates { get; } = new();

    private ProjectProfile? _selectedProjectProfile;
    public ProjectProfile? SelectedProjectProfile
    {
        get => _selectedProjectProfile;
        set => Set(ref _selectedProjectProfile, value);
    }

    private UnityProfile? _selectedUnityProfile;
    public UnityProfile? SelectedUnityProfile
    {
        get => _selectedUnityProfile;
        set => Set(ref _selectedUnityProfile, value);
    }

    private SigningProfile? _selectedSigningProfile;
    public SigningProfile? SelectedSigningProfile
    {
        get => _selectedSigningProfile;
        set => Set(ref _selectedSigningProfile, value);
    }

    private CertificateProfile? _selectedCertificateProfile;
    public CertificateProfile? SelectedCertificateProfile
    {
        get => _selectedCertificateProfile;
        set => Set(ref _selectedCertificateProfile, value);
    }

    public void LoadProfiles()
    {
        AvailableProjects.Clear();
        foreach (var p in ProfileStore.LoadProjects())
            AvailableProjects.Add(p);

        AvailableUnityProfiles.Clear();
        foreach (var u in ProfileStore.LoadUnityProfiles())
            AvailableUnityProfiles.Add(u);

        AvailableSigningProfiles.Clear();
        foreach (var s in ProfileStore.LoadSigningProfiles())
            AvailableSigningProfiles.Add(s);

        AvailableCertificates.Clear();
        foreach (var c in ProfileStore.LoadCertificates())
            AvailableCertificates.Add(c);
    }

    public void ApplyProjectProfile()
    {
        if (SelectedProjectProfile is null) { StatusMessage = "请先选择一个项目模板。"; return; }
        var p = SelectedProjectProfile;
        if (!string.IsNullOrEmpty(p.RepositoryUrl)) EditConfig.RepositoryUrl = p.RepositoryUrl;
        if (!string.IsNullOrEmpty(p.Branch)) EditConfig.Branch = p.Branch;
        if (!string.IsNullOrEmpty(p.ProjectDirectoryName)) EditConfig.ProjectDirectoryName = p.ProjectDirectoryName;
        if (!string.IsNullOrEmpty(p.WorkspaceRoot)) EditConfig.WorkspaceRoot = p.WorkspaceRoot;
        if (!string.IsNullOrEmpty(p.ArtifactsRoot)) EditConfig.ArtifactsRoot = p.ArtifactsRoot;
        HasProjectApplied = true;
        Raise(nameof(ShowProjectFields));
        StatusMessage = $"✅ 已从项目模板「{p.Name}」填充，项目字段已折叠。";
    }

    public void ApplyUnityProfile()
    {
        if (SelectedUnityProfile is null) { StatusMessage = "请先选择一个工程模板。"; return; }
        var u = SelectedUnityProfile;
        if (!string.IsNullOrEmpty(u.UnityProjectRelativePath)) EditConfig.UnityProjectRelativePath = u.UnityProjectRelativePath;
        if (!string.IsNullOrEmpty(u.UnityVersion)) EditConfig.UnityVersion = u.UnityVersion;
        if (!string.IsNullOrEmpty(u.UnityExecutablePath)) EditConfig.UnityExecutablePath = u.UnityExecutablePath;
        if (!string.IsNullOrEmpty(u.UnityBuildMethod)) EditConfig.UnityBuildMethod = u.UnityBuildMethod;
        if (!string.IsNullOrEmpty(u.ProductName)) EditConfig.ProductName = u.ProductName;
        if (!string.IsNullOrEmpty(u.BundleIdentifier)) EditConfig.BundleIdentifier = u.BundleIdentifier;
        HasUnityApplied = true;
        Raise(nameof(ShowUnityFields));
        StatusMessage = $"✅ 已从工程模板「{u.Name}」填充，Unity 工程字段已折叠。";
    }

    public void ApplySigningProfile()
    {
        if (SelectedSigningProfile is null) { StatusMessage = "请先选择一个签名模板。"; return; }
        var s = SelectedSigningProfile;
        if (!string.IsNullOrEmpty(s.TeamId)) EditConfig.TeamId = s.TeamId;
        if (!string.IsNullOrEmpty(s.ExportMethod)) EditConfig.ExportMethod = s.ExportMethod;
        if (!string.IsNullOrEmpty(s.IosDeploymentTarget)) EditConfig.IosDeploymentTarget = s.IosDeploymentTarget;
        if (!string.IsNullOrEmpty(s.AndroidKeystoreName)) EditConfig.AndroidKeystoreName = s.AndroidKeystoreName;
        if (!string.IsNullOrEmpty(s.AndroidKeystorePass)) EditConfig.AndroidKeystorePass = s.AndroidKeystorePass;
        if (!string.IsNullOrEmpty(s.AndroidKeyaliasName)) EditConfig.AndroidKeyaliasName = s.AndroidKeyaliasName;
        if (!string.IsNullOrEmpty(s.AndroidKeyaliasPass)) EditConfig.AndroidKeyaliasPass = s.AndroidKeyaliasPass;
        HasSigningApplied = true;
        Raise(nameof(ShowSigningFields));
        StatusMessage = $"✅ 已从签名模板「{s.Name}」填充，签名字段已折叠。";
    }

    public void ApplyCertificateProfile()
    {
        if (SelectedCertificateProfile is null)
        {
            StatusMessage = "请先选择一个证书模板。";
            return;
        }
        var c = SelectedCertificateProfile;
        // iOS — 始终填充，不按平台过滤
        if (!string.IsNullOrEmpty(c.TeamId)) EditConfig.TeamId = c.TeamId;
        if (!string.IsNullOrEmpty(c.ExportMethod)) EditConfig.ExportMethod = c.ExportMethod;
        if (!string.IsNullOrEmpty(c.IosDeploymentTarget)) EditConfig.IosDeploymentTarget = c.IosDeploymentTarget;
        if (!string.IsNullOrEmpty(c.AppStoreConnectApiKeyPath)) EditConfig.AppStoreConnectApiKeyPath = c.AppStoreConnectApiKeyPath;
        if (!string.IsNullOrEmpty(c.AppStoreConnectApiKeyId)) EditConfig.AppStoreConnectApiKeyId = c.AppStoreConnectApiKeyId;
        if (!string.IsNullOrEmpty(c.AppStoreConnectApiIssuerId)) EditConfig.AppStoreConnectApiIssuerId = c.AppStoreConnectApiIssuerId;
        EditConfig.AppStoreConnectUploadEnabled = c.AppStoreConnectUploadEnabled;
        // Android — 始终填充
        if (!string.IsNullOrEmpty(c.AndroidKeystoreName)) EditConfig.AndroidKeystoreName = c.AndroidKeystoreName;
        if (!string.IsNullOrEmpty(c.AndroidKeystorePass)) EditConfig.AndroidKeystorePass = c.AndroidKeystorePass;
        if (!string.IsNullOrEmpty(c.AndroidKeyaliasName)) EditConfig.AndroidKeyaliasName = c.AndroidKeyaliasName;
        if (!string.IsNullOrEmpty(c.AndroidKeyaliasPass)) EditConfig.AndroidKeyaliasPass = c.AndroidKeyaliasPass;
        if (!string.IsNullOrEmpty(c.GooglePlayPackageName)) EditConfig.GooglePlayPackageName = c.GooglePlayPackageName;
        if (!string.IsNullOrEmpty(c.GooglePlayServiceAccountJsonPath)) EditConfig.GooglePlayServiceAccountJsonPath = c.GooglePlayServiceAccountJsonPath;
        if (!string.IsNullOrEmpty(c.GooglePlayTrack)) EditConfig.GooglePlayTrack = c.GooglePlayTrack;
        EditConfig.GooglePlayUploadEnabled = c.GooglePlayUploadEnabled;
        // TikTok — 始终填充
        if (!string.IsNullOrEmpty(c.TiktokAppId)) EditConfig.TiktokAppId = c.TiktokAppId;
        if (!string.IsNullOrEmpty(c.TiktokAccessToken)) EditConfig.TiktokAccessToken = c.TiktokAccessToken;
        if (!string.IsNullOrEmpty(c.TiktokGameName)) EditConfig.TiktokGameName = c.TiktokGameName;
        if (!string.IsNullOrEmpty(c.TiktokApiEndpoint)) EditConfig.TiktokApiEndpoint = c.TiktokApiEndpoint;
        EditConfig.TiktokUploadEnabled = c.TiktokUploadEnabled;
        HasCertApplied = true;
        Raise(nameof(ShowCertFields));
        StatusMessage = $"✅ 已从证书模板「{c.Name}」填充，证书字段已折叠。";
    }

    public ConfigPageViewModel()
    {
        LoadProfiles();
        RefreshConfigs();
    }

    public void RefreshConfigs()
    {
        Configs.Clear();
        try
        {
            var entries = ConfigFileSelector.FindConfigFiles();
            foreach (var entry in entries)
            {
                var item = new ConfigItem
                {
                    FullPath = entry.FullPath,
                    DisplayPath = entry.DisplayPath,
                    DisplayName = entry.DisplayName
                };
                LoadConfigDetails(item);
                Configs.Add(item);
            }
            StatusMessage = $"找到 {Configs.Count} 个配置文件。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"刷新失败: {ex.Message}";
        }
    }

    private static void LoadConfigDetails(ConfigItem item)
    {
        try
        {
            item.RawJson = File.ReadAllText(item.FullPath);
            using var doc = JsonDocument.Parse(item.RawJson);
            var root = doc.RootElement;
            item.Platform = GetString(root, "buildPlatform");
            item.ConfigName = GetString(root, "configName");
            item.RepositoryUrl = GetString(root, "repositoryUrl");
            item.Branch = GetString(root, "branch");
            item.ProductName = GetString(root, "productName");
            item.BundleIdentifier = GetString(root, "bundleIdentifier");
            item.BuildNumber = GetString(root, "buildNumber");
            item.BundleVersion = GetString(root, "bundleVersion");
            item.UnityVersion = GetString(root, "unityVersion");
            item.UnityExecutablePath = GetString(root, "unityExecutablePath");
            item.UnityBuildMethod = GetString(root, "unityBuildMethod");
            item.WorkspaceRoot = GetString(root, "workspaceRoot");
            item.ArtifactsRoot = GetString(root, "artifactsRoot");
            item.ProjectDirectoryName = GetString(root, "projectDirectoryName");
            item.UnityProjectRelativePath = GetString(root, "unityProjectRelativePath");
            // iOS
            item.TeamId = GetString(root, "teamId");
            item.ExportMethod = GetString(root, "exportMethod");
            item.IosDeploymentTarget = GetString(root, "iosDeploymentTarget");
            item.AppStoreConnectApiKeyPath = GetString(root, "appStoreConnectApiKeyPath");
            item.AppStoreConnectApiKeyId = GetString(root, "appStoreConnectApiKeyId");
            item.AppStoreConnectApiIssuerId = GetString(root, "appStoreConnectApiIssuerId");
            item.AppStoreConnectUploadEnabled = GetBool(root, "appStoreConnectUploadEnabled");
            // Android
            item.AndroidBuildFormat = GetString(root, "androidBuildFormat");
            item.AndroidKeystoreName = GetString(root, "androidKeystoreName");
            item.AndroidKeystorePass = GetString(root, "androidKeystorePass");
            item.AndroidKeyaliasName = GetString(root, "androidKeyaliasName");
            item.AndroidKeyaliasPass = GetString(root, "androidKeyaliasPass");
            item.GooglePlayUploadEnabled = GetBool(root, "googlePlayUploadEnabled");
            item.GooglePlayPackageName = GetString(root, "googlePlayPackageName");
            item.GooglePlayServiceAccountJsonPath = GetString(root, "googlePlayServiceAccountJsonPath");
            item.GooglePlayTrack = GetString(root, "googlePlayTrack");
            // TikTok
            item.TiktokAppId = GetString(root, "tiktokAppId");
            item.TiktokAccessToken = GetString(root, "tiktokAccessToken");
            item.TiktokGameName = GetString(root, "tiktokGameName");
            item.TiktokWebglOutputDirectory = GetString(root, "tiktokWebglOutputDirectory");
            item.TiktokUploadEnabled = GetBool(root, "tiktokUploadEnabled");
            item.TiktokApiEndpoint = GetString(root, "tiktokApiEndpoint");
        }
        catch { }
    }

    public void StartEdit()
    {
        if (SelectedConfig is null) { StatusMessage = "请先选择一个配置文件。"; return; }
        EditConfig = CloneConfig(SelectedConfig);
        ResetTemplateFlags();
        IsEditing = true;
        IsNewConfig = false;
        StatusMessage = "正在编辑配置，修改后点击保存。";
    }

    public void StartNew(string platform)
    {
        try
        {
            string fileName = platform switch
            {
                "android" => "build-android.json",
                "tiktok" => "build-tiktok.json",
                _ => "build-ios.json"
            };
            string template = platform switch
            {
                "android" => SampleFiles.BuildAndroidConfigJson,
                "tiktok" => SampleFiles.BuildTiktokConfigJson,
                _ => SampleFiles.BuildIosConfigJson
            };

            EditConfig = new ConfigItem
            {
                FullPath = Path.Combine(Environment.CurrentDirectory, "configs", fileName),
                Platform = platform,
                RawJson = template
            };
            ResetTemplateFlags();
            LoadConfigDetails(EditConfig);
            IsEditing = true;
            IsNewConfig = true;
            StatusMessage = $"正在创建新的 {platform} 配置，填写后点击保存。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"创建失败: {ex.Message}";
        }
    }

    public void SaveConfig()
    {
        try
        {
            var json = new JsonObject
            {
                ["configName"] = EditConfig.ConfigName ?? "",
                ["buildPlatform"] = EditConfig.Platform ?? "ios",
                ["repositoryUrl"] = EditConfig.RepositoryUrl ?? "",
                ["allowedRepositoryUrls"] = new JsonArray(EditConfig.RepositoryUrl ?? ""),
                ["branch"] = EditConfig.Branch ?? "main",
                ["workspaceRoot"] = EditConfig.WorkspaceRoot ?? "~/UnityBuildWorkspace",
                ["allowedWorkspaceRoots"] = new JsonArray(EditConfig.WorkspaceRoot ?? "~/UnityBuildWorkspace"),
                ["projectDirectoryName"] = EditConfig.ProjectDirectoryName ?? "",
                ["unityProjectRelativePath"] = EditConfig.UnityProjectRelativePath ?? ".",
                ["unityVersion"] = EditConfig.UnityVersion ?? "",
                ["unityExecutablePath"] = EditConfig.UnityExecutablePath ?? "",
                ["unityBuildMethod"] = string.IsNullOrWhiteSpace(EditConfig.UnityBuildMethod)
                    ? (EditConfig.Platform == "android" ? DefaultUnityBuildMethods.Android
                      : EditConfig.Platform == "tiktok" ? DefaultUnityBuildMethods.Tiktok
                      : DefaultUnityBuildMethods.Ios)
                    : EditConfig.UnityBuildMethod,
                ["artifactsRoot"] = EditConfig.ArtifactsRoot ?? "~/UnityBuildArtifacts",
                ["allowedArtifactsRoots"] = new JsonArray(EditConfig.ArtifactsRoot ?? "~/UnityBuildArtifacts"),
                ["logsDirectory"] = "",
                ["bundleIdentifier"] = EditConfig.BundleIdentifier ?? "",
                ["productName"] = EditConfig.ProductName ?? "",
                ["bundleVersion"] = EditConfig.BundleVersion ?? "1.0.0",
                ["syncBundleVersionFromUnity"] = true,
                ["buildNumber"] = EditConfig.BuildNumber ?? "1",
                ["autoIncrementBuildNumber"] = true,
                ["resetRepository"] = true,
                ["preserveUnityLibraryOnReset"] = true,
                ["saveConfigSnapshot"] = true,
                ["environment"] = new JsonObject()
            };

            if (EditConfig.Platform == "ios")
            {
                json["scheme"] = "Unity-iPhone";
                json["configuration"] = "Release";
                json["exportMethod"] = string.IsNullOrWhiteSpace(EditConfig.ExportMethod) ? "development" : EditConfig.ExportMethod;
                json["teamId"] = EditConfig.TeamId ?? "";
                json["signingStyle"] = "automatic";
                json["iosDeploymentTarget"] = string.IsNullOrWhiteSpace(EditConfig.IosDeploymentTarget) ? "13.0" : EditConfig.IosDeploymentTarget;
                json["allowProvisioningUpdates"] = true;
                json["generateExportOptionsPlist"] = true;
                json["copyArchiveToOrganizer"] = true;
                json["appStoreConnectUploadEnabled"] = EditConfig.AppStoreConnectUploadEnabled;
                json["appStoreConnectApiKeyPath"] = EditConfig.AppStoreConnectApiKeyPath ?? "";
                json["appStoreConnectApiKeyId"] = EditConfig.AppStoreConnectApiKeyId ?? "";
                json["appStoreConnectApiIssuerId"] = EditConfig.AppStoreConnectApiIssuerId ?? "";
                json["xcodeBuildSettings"] = new JsonObject();
                json["provisioningProfiles"] = new JsonObject();
            }
            else if (EditConfig.Platform == "android")
            {
                json["androidBuildFormat"] = string.IsNullOrWhiteSpace(EditConfig.AndroidBuildFormat) ? "aab" : EditConfig.AndroidBuildFormat;
                json["androidOutputDirectory"] = "";
                json["apkOutputPath"] = "";
                json["aabOutputPath"] = "";
                json["androidKeystoreName"] = EditConfig.AndroidKeystoreName ?? "";
                json["androidKeystorePass"] = EditConfig.AndroidKeystorePass ?? "";
                json["androidKeyaliasName"] = EditConfig.AndroidKeyaliasName ?? "";
                json["androidKeyaliasPass"] = EditConfig.AndroidKeyaliasPass ?? "";
                json["googlePlayUploadEnabled"] = EditConfig.GooglePlayUploadEnabled;
                json["googlePlayPackageName"] = EditConfig.GooglePlayPackageName ?? "";
                json["googlePlayServiceAccountJsonPath"] = EditConfig.GooglePlayServiceAccountJsonPath ?? "";
                json["googlePlayTrack"] = string.IsNullOrWhiteSpace(EditConfig.GooglePlayTrack) ? "internal" : EditConfig.GooglePlayTrack;
            }
            else if (EditConfig.Platform == "tiktok")
            {
                json["tiktokAppId"] = EditConfig.TiktokAppId ?? "";
                json["tiktokAccessToken"] = EditConfig.TiktokAccessToken ?? "";
                json["tiktokGameName"] = EditConfig.TiktokGameName ?? "";
                json["tiktokWebglOutputDirectory"] = EditConfig.TiktokWebglOutputDirectory ?? "";
                json["tiktokUploadEnabled"] = EditConfig.TiktokUploadEnabled;
                json["tiktokApiEndpoint"] = string.IsNullOrWhiteSpace(EditConfig.TiktokApiEndpoint)
                    ? "https://open-api.tiktokglobalshop.com"
                    : EditConfig.TiktokApiEndpoint;
            }

            string jsonStr = json.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            string path = EditConfig.FullPath;

            if (IsNewConfig && File.Exists(path))
            {
                path = Path.Combine(
                    Path.GetDirectoryName(path)!,
                    $"{Path.GetFileNameWithoutExtension(path)}-{DateTime.Now:HHmmss}.json");
            }

            PathTools.EnsureParentDirectory(path);
            File.WriteAllText(path, jsonStr + Environment.NewLine, TextEncodings.Utf8Bom);
            StatusMessage = $"✅ 配置已保存: {path}";
            IsEditing = false;
            IsNewConfig = false;
            RefreshConfigs();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 保存失败: {ex.Message}";
        }
    }

    public void CancelEdit()
    {
        IsEditing = false;
        IsNewConfig = false;
        StatusMessage = "已取消编辑。";
    }

    public void DeleteConfig(ConfigItem item)
    {
        try
        {
            File.Delete(item.FullPath);
            StatusMessage = $"已删除: {item.FullPath}";
            RefreshConfigs();
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除失败: {ex.Message}";
        }
    }

    public void OpenInEditor(ConfigItem item)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("notepad.exe", item.FullPath) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", $"\"{item.FullPath}\"");
            else
                Process.Start("xdg-open", $"\"{item.FullPath}\"");
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开失败: {ex.Message}";
        }
    }

    public void OpenConfigDirectory()
    {
        try
        {
            string dir = Environment.CurrentDirectory;
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", $"\"{dir}\"");
            else
                Process.Start("xdg-open", $"\"{dir}\"");
        }
        catch { }
    }

    public async Task ImportConfigAsync()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择要导入的配置文件",
                Filters = new List<FileDialogFilter>
                {
                    new() { Name = "JSON 配置文件", Extensions = new List<string> { "json" } },
                    new() { Name = "所有文件", Extensions = new List<string> { "*" } }
                },
                AllowMultiple = false
            };

            string[]? results = await dialog.ShowAsync(GetMainWindow());
            if (results is null || results.Length == 0)
            {
                StatusMessage = "未选择文件。";
                return;
            }

            string srcPath = results[0];
            string fileName = Path.GetFileName(srcPath);

            string destDir = Path.Combine(Environment.CurrentDirectory, "configs");
            Directory.CreateDirectory(destDir);
            string destPath = Path.Combine(destDir, fileName);

            if (File.Exists(destPath))
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                destPath = Path.Combine(destDir, $"{nameNoExt}-{DateTime.Now:HHmmss}{ext}");
            }

            File.Copy(srcPath, destPath);
            StatusMessage = $"✅ 配置已导入: {destPath}";
            RefreshConfigs();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 导入失败: {ex.Message}";
        }
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private static ConfigItem CloneConfig(ConfigItem src)
    {
        return new ConfigItem
        {
            FullPath = src.FullPath,
            DisplayName = src.DisplayName,
            DisplayPath = src.DisplayPath,
            Platform = src.Platform,
            ConfigName = src.ConfigName,
            RepositoryUrl = src.RepositoryUrl,
            Branch = src.Branch,
            ProductName = src.ProductName,
            BundleIdentifier = src.BundleIdentifier,
            BuildNumber = src.BuildNumber,
            BundleVersion = src.BundleVersion,
            UnityVersion = src.UnityVersion,
            UnityExecutablePath = src.UnityExecutablePath,
            UnityBuildMethod = src.UnityBuildMethod,
            WorkspaceRoot = src.WorkspaceRoot,
            ArtifactsRoot = src.ArtifactsRoot,
            ProjectDirectoryName = src.ProjectDirectoryName,
            UnityProjectRelativePath = src.UnityProjectRelativePath,
            TeamId = src.TeamId,
            ExportMethod = src.ExportMethod,
            IosDeploymentTarget = src.IosDeploymentTarget,
            AppStoreConnectApiKeyPath = src.AppStoreConnectApiKeyPath,
            AppStoreConnectApiKeyId = src.AppStoreConnectApiKeyId,
            AppStoreConnectApiIssuerId = src.AppStoreConnectApiIssuerId,
            AppStoreConnectUploadEnabled = src.AppStoreConnectUploadEnabled,
            AndroidBuildFormat = src.AndroidBuildFormat,
            AndroidKeystoreName = src.AndroidKeystoreName,
            AndroidKeystorePass = src.AndroidKeystorePass,
            AndroidKeyaliasName = src.AndroidKeyaliasName,
            AndroidKeyaliasPass = src.AndroidKeyaliasPass,
            GooglePlayUploadEnabled = src.GooglePlayUploadEnabled,
            GooglePlayPackageName = src.GooglePlayPackageName,
            GooglePlayServiceAccountJsonPath = src.GooglePlayServiceAccountJsonPath,
            GooglePlayTrack = src.GooglePlayTrack,
            TiktokAppId = src.TiktokAppId,
            TiktokAccessToken = src.TiktokAccessToken,
            TiktokGameName = src.TiktokGameName,
            TiktokWebglOutputDirectory = src.TiktokWebglOutputDirectory,
            TiktokUploadEnabled = src.TiktokUploadEnabled,
            TiktokApiEndpoint = src.TiktokApiEndpoint,
            RawJson = src.RawJson
        };
    }

    private static string GetString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? ""
            : "";
    }

    private static bool GetBool(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? el.GetBoolean()
            : false;
    }
}