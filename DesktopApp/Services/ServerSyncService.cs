using System.Net.Http;
using System.Text;
using System.Text.Json;
using DesktopApp.Models;

namespace DesktopApp.Services;

/// <summary>
/// 连接 BuildServer，同步项目模板、证书模板、配置文件。
/// </summary>
public sealed class ServerSyncService : IDisposable
{
    private readonly HttpClient _http = new(new HttpClientHandler { UseCookies = true, CookieContainer = new System.Net.CookieContainer() });
    private string? _baseUrl;
    private bool _isLoggedIn;

    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public bool IsLoggedIn => _isLoggedIn;
    public string? BaseUrl => _baseUrl;

    // ---- 服务器连接设置持久化 ----

    private static string SettingsPath => Path.Combine(Environment.CurrentDirectory, "profiles", "server-settings.json");

    public ServerSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                return JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(SettingsPath), s_json) ?? new ServerSettings();
            }
        }
        catch { }
        return new ServerSettings();
    }

    public void SaveSettings(ServerSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ---- 登录 ----

    public async Task<bool> LoginAsync(string baseUrl, string username, string password)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        var payload = JsonSerializer.Serialize(new { userName = username, password });
        var resp = await _http.PostAsync($"{_baseUrl}/api/auth/login",
            new StringContent(payload, Encoding.UTF8, "application/json"));
        if (!resp.IsSuccessStatusCode) return false;
        _isLoggedIn = true;
        return true;
    }

    // ---- 项目模板 ----

    public async Task<List<ProjectProfile>> PullProjectProfilesAsync()
    {
        EnsureLoggedIn();
        var resp = await _http.GetAsync($"{_baseUrl}/api/project-profiles");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<List<ServerProjectProfile>>(json, s_json) ?? new();
        return list.Select(MapToLocal).ToList();
    }

    public async Task<bool> PushProjectProfileAsync(ProjectProfile profile)
    {
        EnsureLoggedIn();
        var payload = MapToServerProject(profile);
        var json = JsonSerializer.Serialize(payload, s_json);
        var resp = await _http.PostAsync($"{_baseUrl}/api/project-profiles",
            new StringContent(json, Encoding.UTF8, "application/json"));
        return resp.IsSuccessStatusCode;
    }

    // ---- 证书模板 ----

    public async Task<List<CertificateProfile>> PullCertificateProfilesAsync()
    {
        EnsureLoggedIn();
        var resp = await _http.GetAsync($"{_baseUrl}/api/certificate-profiles");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<List<ServerCertificateProfile>>(json, s_json) ?? new();
        return list.Select(MapToLocal).ToList();
    }

    public async Task<bool> PushCertificateProfileAsync(CertificateProfile profile)
    {
        EnsureLoggedIn();
        var payload = MapToServerCert(profile);
        var json = JsonSerializer.Serialize(payload, s_json);
        var resp = await _http.PostAsync($"{_baseUrl}/api/certificate-profiles",
            new StringContent(json, Encoding.UTF8, "application/json"));
        return resp.IsSuccessStatusCode;
    }

    // ---- Unity Profiles ----

    public async Task<List<UnityProfile>> PullUnityProfilesAsync()
    {
        EnsureLoggedIn();
        var resp = await _http.GetAsync($"{_baseUrl}/api/unity-project-profiles");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<List<ServerUnityProfile>>(json, s_json) ?? new();
        return list.Select(s => new UnityProfile
        {
            Id = s.Id ?? Guid.NewGuid().ToString("N"),
            Name = s.Name ?? "",
            UnityProjectRelativePath = s.UnityProjectRelativePath ?? ".",
            UnityVersion = s.UnityVersion ?? "",
            UnityExecutablePath = s.UnityExecutablePath ?? "",
            UnityBuildMethod = s.UnityBuildMethod ?? "",
            ProductName = s.ProductName ?? "",
            BundleIdentifier = s.BundleIdentifier ?? ""
        }).ToList();
    }

    public async Task<bool> PushUnityProfileAsync(UnityProfile profile)
    {
        EnsureLoggedIn();
        var payload = new
        {
            name = profile.Name,
            unityProjectRelativePath = profile.UnityProjectRelativePath,
            unityVersion = profile.UnityVersion,
            unityExecutablePath = profile.UnityExecutablePath,
            unityBuildMethod = profile.UnityBuildMethod,
            productName = profile.ProductName,
            bundleIdentifier = profile.BundleIdentifier
        };
        var json = JsonSerializer.Serialize(payload, s_json);
        var resp = await _http.PostAsync($"{_baseUrl}/api/unity-project-profiles",
            new StringContent(json, Encoding.UTF8, "application/json"));
        return resp.IsSuccessStatusCode;
    }

    // ---- Signing Profiles ----

    public async Task<List<SigningProfile>> PullSigningProfilesAsync()
    {
        EnsureLoggedIn();
        var resp = await _http.GetAsync($"{_baseUrl}/api/signing-profiles");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<List<ServerSigningProfile>>(json, s_json) ?? new();
        return list.Select(s => new SigningProfile
        {
            Id = s.Id ?? Guid.NewGuid().ToString("N"),
            Name = s.Name ?? "",
            Platform = s.Platform ?? "ios",
            TeamId = s.TeamId ?? "",
            ExportMethod = s.ExportMethod ?? "development",
            SigningStyle = s.SigningStyle ?? "automatic",
            IosDeploymentTarget = s.IosDeploymentTarget ?? "",
            AndroidKeystoreName = s.AndroidKeystoreName ?? "",
            AndroidKeystorePass = s.AndroidKeystorePass ?? "",
            AndroidKeyaliasName = s.AndroidKeyaliasName ?? "",
            AndroidKeyaliasPass = s.AndroidKeyaliasPass ?? ""
        }).ToList();
    }

    public async Task<bool> PushSigningProfileAsync(SigningProfile profile)
    {
        EnsureLoggedIn();
        var payload = new
        {
            name = profile.Name,
            platform = profile.Platform,
            teamId = profile.TeamId,
            exportMethod = profile.ExportMethod,
            signingStyle = profile.SigningStyle,
            iosDeploymentTarget = profile.IosDeploymentTarget,
            androidKeystoreName = profile.AndroidKeystoreName,
            androidKeystorePass = profile.AndroidKeystorePass,
            androidKeyaliasName = profile.AndroidKeyaliasName,
            androidKeyaliasPass = profile.AndroidKeyaliasPass
        };
        var json = JsonSerializer.Serialize(payload, s_json);
        var resp = await _http.PostAsync($"{_baseUrl}/api/signing-profiles",
            new StringContent(json, Encoding.UTF8, "application/json"));
        return resp.IsSuccessStatusCode;
    }

    // ---- 配置文件 ----

    public async Task<List<ServerConfigInfo>> ListServerConfigsAsync()
    {
        EnsureLoggedIn();
        var resp = await _http.GetAsync($"{_baseUrl}/api/configs");
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<ServerConfigInfo>>(json, s_json) ?? new();
    }

    public async Task<string?> DownloadConfigAsync(string configId)
    {
        EnsureLoggedIn();
        var resp = await _http.GetAsync($"{_baseUrl}/api/configs/{configId}/file");
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("content", out var contentEl))
            return contentEl.GetRawText();
        return json;
    }

    // ---- Helpers ----

    private void EnsureLoggedIn()
    {
        if (!_isLoggedIn) throw new InvalidOperationException("请先连接到服务器。");
    }

    private static ProjectProfile MapToLocal(ServerProjectProfile s) => new()
    {
        Id = s.Id ?? Guid.NewGuid().ToString("N"),
        Name = s.Name ?? "",
        RepositoryUrl = s.RepositoryUrl ?? "",
        Branch = s.DefaultBranch ?? "main",
        ProjectDirectoryName = s.ProjectDirectoryName ?? "",
        UnityProjectRelativePath = s.UnityProjectRelativePath ?? ".",
        UnityVersion = s.UnityVersion ?? "",
        UnityExecutablePath = s.UnityExecutablePath ?? "",
        UnityBuildMethod = s.UnityBuildMethod ?? "",
        WorkspaceRoot = s.WorkspaceRoot ?? "~/UnityBuildWorkspace",
        ArtifactsRoot = s.ArtifactsRoot ?? "~/UnityBuildArtifacts",
        ProductName = s.ProductName ?? "",
        BundleIdentifier = s.BundleIdentifier ?? ""
    };

    private static object MapToServerProject(ProjectProfile p) => new
    {
        name = p.Name,
        repositoryUrl = p.RepositoryUrl,
        defaultBranch = p.Branch,
        projectDirectoryName = p.ProjectDirectoryName,
        unityProjectRelativePath = p.UnityProjectRelativePath,
        unityVersion = p.UnityVersion,
        unityExecutablePath = p.UnityExecutablePath,
        unityBuildMethod = p.UnityBuildMethod,
        workspaceRoot = p.WorkspaceRoot,
        artifactsRoot = p.ArtifactsRoot,
        productName = p.ProductName,
        bundleIdentifier = p.BundleIdentifier
    };

    private static CertificateProfile MapToLocal(ServerCertificateProfile s) => new()
    {
        Id = s.Id ?? Guid.NewGuid().ToString("N"),
        Name = s.Name ?? "",
        Platform = s.Platform ?? "ios",
        TeamId = s.TeamId ?? "",
        ExportMethod = s.ExportMethod ?? "development",
        IosDeploymentTarget = s.IosDeploymentTarget ?? "",
        AppStoreConnectApiKeyPath = s.AppStoreConnectApiKeyPath ?? "",
        AppStoreConnectApiKeyId = s.AppStoreConnectApiKeyId ?? "",
        AppStoreConnectApiIssuerId = s.AppStoreConnectApiIssuerId ?? "",
        AppStoreConnectUploadEnabled = s.AppStoreConnectUploadEnabled,
        AndroidKeystoreName = s.AndroidKeystoreName ?? "",
        AndroidKeystorePass = s.AndroidKeystorePass ?? "",
        AndroidKeyaliasName = s.AndroidKeyaliasName ?? "",
        AndroidKeyaliasPass = s.AndroidKeyaliasPass ?? "",
        GooglePlayUploadEnabled = s.GooglePlayUploadEnabled,
        GooglePlayPackageName = s.GooglePlayPackageName ?? "",
        GooglePlayServiceAccountJsonPath = s.GooglePlayServiceAccountJsonPath ?? "",
        GooglePlayTrack = s.GooglePlayTrack ?? "internal",
        TiktokAppId = s.TiktokAppId ?? "",
        TiktokAccessToken = s.TiktokAccessToken ?? "",
        TiktokGameName = s.TiktokGameName ?? "",
        TiktokApiEndpoint = s.TiktokApiEndpoint ?? "https://open-api.tiktokglobalshop.com",
        TiktokUploadEnabled = s.TiktokUploadEnabled
    };

    private static object MapToServerCert(CertificateProfile c) => new
    {
        name = c.Name,
        platform = c.Platform,
        teamId = c.TeamId,
        exportMethod = c.ExportMethod,
        iosDeploymentTarget = c.IosDeploymentTarget,
        appStoreConnectApiKeyPath = c.AppStoreConnectApiKeyPath,
        appStoreConnectApiKeyId = c.AppStoreConnectApiKeyId,
        appStoreConnectApiIssuerId = c.AppStoreConnectApiIssuerId,
        appStoreConnectUploadEnabled = c.AppStoreConnectUploadEnabled,
        androidKeystoreName = c.AndroidKeystoreName,
        androidKeystorePass = c.AndroidKeystorePass,
        androidKeyaliasName = c.AndroidKeyaliasName,
        androidKeyaliasPass = c.AndroidKeyaliasPass,
        googlePlayUploadEnabled = c.GooglePlayUploadEnabled,
        googlePlayPackageName = c.GooglePlayPackageName,
        googlePlayServiceAccountJsonPath = c.GooglePlayServiceAccountJsonPath,
        googlePlayTrack = c.GooglePlayTrack,
        tiktokAppId = c.TiktokAppId,
        tiktokAccessToken = c.TiktokAccessToken,
        tiktokGameName = c.TiktokGameName,
        tiktokApiEndpoint = c.TiktokApiEndpoint,
        tiktokUploadEnabled = c.TiktokUploadEnabled
    };

    public void Dispose() => _http.Dispose();
}

// ---- DTOs ----

public sealed class ServerSettings
{
    public string Url { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class ServerProjectProfile
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? RepositoryUrl { get; set; }
    public string? DefaultBranch { get; set; }
    public string? ProjectDirectoryName { get; set; }
    public string? UnityProjectRelativePath { get; set; }
    public string? UnityVersion { get; set; }
    public string? UnityExecutablePath { get; set; }
    public string? UnityBuildMethod { get; set; }
    public string? WorkspaceRoot { get; set; }
    public string? ArtifactsRoot { get; set; }
    public string? ProductName { get; set; }
    public string? BundleIdentifier { get; set; }
}

public sealed class ServerCertificateProfile
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Platform { get; set; }
    public string? TeamId { get; set; }
    public string? ExportMethod { get; set; }
    public string? IosDeploymentTarget { get; set; }
    public string? AppStoreConnectApiKeyPath { get; set; }
    public string? AppStoreConnectApiKeyId { get; set; }
    public string? AppStoreConnectApiIssuerId { get; set; }
    public bool AppStoreConnectUploadEnabled { get; set; }
    public string? AndroidKeystoreName { get; set; }
    public string? AndroidKeystorePass { get; set; }
    public string? AndroidKeyaliasName { get; set; }
    public string? AndroidKeyaliasPass { get; set; }
    public bool GooglePlayUploadEnabled { get; set; }
    public string? GooglePlayPackageName { get; set; }
    public string? GooglePlayServiceAccountJsonPath { get; set; }
    public string? GooglePlayTrack { get; set; }
    public string? TiktokAppId { get; set; }
    public string? TiktokAccessToken { get; set; }
    public string? TiktokGameName { get; set; }
    public string? TiktokApiEndpoint { get; set; }
    public bool TiktokUploadEnabled { get; set; }
}

public sealed class ServerConfigInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string BuildPlatform { get; set; } = "";
    public string ConfigPath { get; set; } = "";
    public string ProjectId { get; set; } = "";
}

public sealed class ServerUnityProfile
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? UnityProjectRelativePath { get; set; }
    public string? UnityVersion { get; set; }
    public string? UnityExecutablePath { get; set; }
    public string? UnityBuildMethod { get; set; }
    public string? ProductName { get; set; }
    public string? BundleIdentifier { get; set; }
}

public sealed class ServerSigningProfile
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Platform { get; set; }
    public string? TeamId { get; set; }
    public string? ExportMethod { get; set; }
    public string? SigningStyle { get; set; }
    public string? IosDeploymentTarget { get; set; }
    public string? AndroidKeystoreName { get; set; }
    public string? AndroidKeystorePass { get; set; }
    public string? AndroidKeyaliasName { get; set; }
    public string? AndroidKeyaliasPass { get; set; }
}
