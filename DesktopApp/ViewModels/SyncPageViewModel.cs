using System.Collections.ObjectModel;
using DesktopApp.Models;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class SyncPageViewModel : ViewModelBase
{
    private readonly ServerSyncService _sync = new();
    private string _serverUrl = "";
    private string _userName = "";
    private string _password = "";
    private string _statusMessage = "";
    private bool _isLoggedIn;
    private bool _isBusy;
    private int _localProjectCount;
    private int _serverProjectCount;
    private int _localCertCount;
    private int _serverCertCount;

    public ObservableCollection<ServerConfigInfo> ServerConfigs { get; } = new();
    private ServerConfigInfo? _selectedServerConfig;
    public ServerConfigInfo? SelectedServerConfig
    {
        get => _selectedServerConfig;
        set => Set(ref _selectedServerConfig, value);
    }

    public string ServerUrl
    {
        get => _serverUrl;
        set => Set(ref _serverUrl, value);
    }

    public string UserName
    {
        get => _userName;
        set => Set(ref _userName, value);
    }

    public string Password
    {
        get => _password;
        set => Set(ref _password, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set => Set(ref _isLoggedIn, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    public int LocalProjectCount
    {
        get => _localProjectCount;
        set => Set(ref _localProjectCount, value);
    }

    public int ServerProjectCount
    {
        get => _serverProjectCount;
        set => Set(ref _serverProjectCount, value);
    }

    public int LocalCertCount
    {
        get => _localCertCount;
        set => Set(ref _localCertCount, value);
    }

    public int ServerCertCount
    {
        get => _serverCertCount;
        set => Set(ref _serverCertCount, value);
    }

    public SyncPageViewModel()
    {
        var settings = _sync.LoadSettings();
        ServerUrl = settings.Url;
        UserName = settings.UserName;
        Password = settings.Password;
        RefreshLocalCounts();
    }

    private void RefreshLocalCounts()
    {
        LocalProjectCount = ProfileStore.LoadProjects().Count;
        LocalCertCount = ProfileStore.LoadCertificates().Count;
    }

    public async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerUrl) || string.IsNullOrWhiteSpace(UserName))
        {
            StatusMessage = "❌ 请填写服务器地址和用户名。";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在连接服务器...";
        try
        {
            bool ok = await _sync.LoginAsync(ServerUrl, UserName, Password);
            if (ok)
            {
                _sync.SaveSettings(new ServerSettings { Url = ServerUrl, UserName = UserName, Password = Password });
                IsLoggedIn = true;
                StatusMessage = "✅ 已连接到服务器。";
                await RefreshServerCountsAsync();
                await RefreshServerConfigsAsync();
            }
            else
            {
                StatusMessage = "❌ 连接失败，请检查地址和密码。";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 连接错误: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshServerCountsAsync()
    {
        if (!IsLoggedIn) return;
        try
        {
            var projects = await _sync.PullProjectProfilesAsync();
            ServerProjectCount = projects.Count;
            var certs = await _sync.PullCertificateProfilesAsync();
            ServerCertCount = certs.Count;
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 获取服务器数据失败: {ex.Message}";
        }
    }

    public async Task RefreshServerConfigsAsync()
    {
        if (!IsLoggedIn) return;
        try
        {
            var configs = await _sync.ListServerConfigsAsync();
            ServerConfigs.Clear();
            foreach (var c in configs)
                ServerConfigs.Add(c);
            StatusMessage = $"找到 {configs.Count} 个服务器配置。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 获取配置列表失败: {ex.Message}";
        }
    }

    // ---- 项目模板同步 ----

    public async Task PullProjectsAsync()
    {
        if (!IsLoggedIn) { StatusMessage = "请先连接服务器。"; return; }
        IsBusy = true;
        StatusMessage = "正在拉取项目模板...";
        try
        {
            var remote = await _sync.PullProjectProfilesAsync();
            var local = ProfileStore.LoadProjects();
            int added = 0, updated = 0;
            foreach (var r in remote)
            {
                int idx = local.FindIndex(x => x.Name == r.Name);
                if (idx >= 0) { local[idx] = r; updated++; }
                else { local.Add(r); added++; }
            }
            ProfileStore.SaveProjects(local);
            RefreshLocalCounts();
            StatusMessage = $"✅ 项目模板: 新增 {added} 个, 更新 {updated} 个。";
        }
        catch (Exception ex) { StatusMessage = $"❌ 拉取失败: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public async Task PushProjectsAsync()
    {
        if (!IsLoggedIn) { StatusMessage = "请先连接服务器。"; return; }
        IsBusy = true;
        StatusMessage = "正在上传项目模板...";
        try
        {
            var local = ProfileStore.LoadProjects();
            int ok = 0, fail = 0;
            foreach (var p in local)
            {
                if (await _sync.PushProjectProfileAsync(p)) ok++;
                else fail++;
            }
            StatusMessage = $"✅ 上传项目模板: 成功 {ok}, 失败 {fail}。";
            await RefreshServerCountsAsync();
        }
        catch (Exception ex) { StatusMessage = $"❌ 上传失败: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ---- 证书模板同步 ----

    public async Task PullCertsAsync()
    {
        if (!IsLoggedIn) { StatusMessage = "请先连接服务器。"; return; }
        IsBusy = true;
        StatusMessage = "正在拉取证书模板...";
        try
        {
            var remote = await _sync.PullCertificateProfilesAsync();
            var local = ProfileStore.LoadCertificates();
            int added = 0, updated = 0;
            foreach (var r in remote)
            {
                int idx = local.FindIndex(x => x.Name == r.Name);
                if (idx >= 0) { local[idx] = r; updated++; }
                else { local.Add(r); added++; }
            }
            ProfileStore.SaveCertificates(local);
            RefreshLocalCounts();
            StatusMessage = $"✅ 证书模板: 新增 {added} 个, 更新 {updated} 个。";
        }
        catch (Exception ex) { StatusMessage = $"❌ 拉取失败: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    public async Task PushCertsAsync()
    {
        if (!IsLoggedIn) { StatusMessage = "请先连接服务器。"; return; }
        IsBusy = true;
        StatusMessage = "正在上传证书模板...";
        try
        {
            var local = ProfileStore.LoadCertificates();
            int ok = 0, fail = 0;
            foreach (var c in local)
            {
                if (await _sync.PushCertificateProfileAsync(c)) ok++;
                else fail++;
            }
            StatusMessage = $"✅ 上传证书模板: 成功 {ok}, 失败 {fail}。";
            await RefreshServerCountsAsync();
        }
        catch (Exception ex) { StatusMessage = $"❌ 上传失败: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ---- 配置文件导入 ----

    public async Task DownloadSelectedConfigAsync()
    {
        if (SelectedServerConfig is null) { StatusMessage = "请先选择一个配置。"; return; }
        IsBusy = true;
        StatusMessage = "正在下载配置文件...";
        try
        {
            string? json = await _sync.DownloadConfigAsync(SelectedServerConfig.Id);
            if (string.IsNullOrEmpty(json))
            {
                StatusMessage = "❌ 下载失败：服务器返回空内容。";
                return;
            }

            string configsDir = DesktopPaths.ConfigsDirectory;
            Directory.CreateDirectory(configsDir);
            string fileName = DesktopPaths.MakePortableFileName(
                $"{SelectedServerConfig.Name}-{SelectedServerConfig.BuildPlatform}.json",
                "build-config.json");
            string path = Path.Combine(configsDir, fileName);
            await File.WriteAllTextAsync(path, json + Environment.NewLine);
            StatusMessage = $"✅ 配置已下载到: {path}";
        }
        catch (Exception ex) { StatusMessage = $"❌ 下载失败: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
