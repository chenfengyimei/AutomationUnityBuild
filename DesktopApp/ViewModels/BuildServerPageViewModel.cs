using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using DesktopApp.ViewModels;

namespace DesktopApp.ViewModels;

public class BuildServerPageViewModel : ViewModelBase
{
    private static readonly HttpClient s_healthClient = new() { Timeout = TimeSpan.FromSeconds(2) };
    private Process? _buildServerProcess;

    private static string CustomPathFile => Path.Combine(Environment.CurrentDirectory, "profiles", "buildserver-path.json");

    private bool _isRunning;
    private bool _canStart;
    private string _statusText = "未检测";
    private string _statusColor = "#94A3B8";
    private string _serverUrl = "";
    private string _exePath = "";
    private string _detectMessage = "正在检测...";
    private string _detectColor = "#64748B";

    public bool IsRunning { get => _isRunning; set => Set(ref _isRunning, value); }
    public bool CanStart { get => _canStart; set => Set(ref _canStart, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public string StatusColor { get => _statusColor; set => Set(ref _statusColor, value); }
    public string ServerUrl { get => _serverUrl; set => Set(ref _serverUrl, value); }
    public bool HasServerUrl => !string.IsNullOrEmpty(ServerUrl);
    public string ExePath { get => _exePath; set => Set(ref _exePath, value); }
    public bool HasExePath => !string.IsNullOrEmpty(ExePath);
    public string DetectMessage { get => _detectMessage; set => Set(ref _detectMessage, value); }
    public string DetectColor { get => _detectColor; set => Set(ref _detectColor, value); }

    public BuildServerPageViewModel() => RefreshStatus();

    public void RefreshStatus()
    {
        DetectBuildServer();
        CheckRunningAsync();
    }

    private void DetectBuildServer()
    {
        // 优先使用用户手动选择并保存的路径
        string? savedPath = LoadCustomPath();
        if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
        {
            ExePath = savedPath;
            CanStart = true;
            DetectMessage = "✅ 已加载自定义路径";
            DetectColor = "#16A34A";
            Raise(nameof(HasExePath));
            return;
        }

        // 自动搜索
        string[] searchPaths =
        [
            Path.Combine(AppContext.BaseDirectory, "..", "BuildServer"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BuildServer", "bin", "Debug", "net8.0"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BuildServer", "bin", "Release", "net8.0"),
            Path.Combine(AppContext.BaseDirectory, "BuildServer"),
            Path.Combine(Directory.GetCurrentDirectory(), "BuildServer"),
            Path.Combine(Directory.GetCurrentDirectory(), "BuildServer", "bin", "Debug", "net8.0"),
            Path.Combine(Directory.GetCurrentDirectory(), "BuildServer", "bin", "Release", "net8.0"),
        ];

        string? foundPath = null;
        foreach (var dir in searchPaths)
        {
            string exe = Path.Combine(dir, "BuildServer.exe");
            string dll = Path.Combine(dir, "BuildServer.dll");
            if (File.Exists(exe)) { foundPath = exe; break; }
            if (File.Exists(dll)) { foundPath = dll; break; }
        }

        if (foundPath is not null)
        {
            ExePath = foundPath;
            CanStart = true;
            DetectMessage = "✅ 已检测到 BuildServer";
            DetectColor = "#16A34A";
        }
        else
        {
            ExePath = "";
            CanStart = false;
            DetectMessage = "❌ 未检测到，可点「手动选择」指定路径";
            DetectColor = "#DC2626";
        }

        Raise(nameof(HasExePath));
    }

    public void SetCustomPath(string path)
    {
        SaveCustomPath(path);
        ExePath = path;
        CanStart = true;
        DetectMessage = $"✅ 已设置自定义路径";
        DetectColor = "#16A34A";
        Raise(nameof(HasExePath));
    }

    public void ClearCustomPath()
    {
        try { if (File.Exists(CustomPathFile)) File.Delete(CustomPathFile); } catch { }
        DetectBuildServer();
    }

    private static string? LoadCustomPath()
    {
        try
        {
            if (!File.Exists(CustomPathFile)) return null;
            var json = JsonDocument.Parse(File.ReadAllText(CustomPathFile));
            return json.RootElement.TryGetProperty("path", out var el) ? el.GetString() : null;
        }
        catch { return null; }
    }

    private static void SaveCustomPath(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CustomPathFile)!);
        File.WriteAllText(CustomPathFile, $$"""{"path":"{{path.Replace("\\", "\\\\")}}"}""");
    }

    private async void CheckRunningAsync()
    {
        string url = "http://127.0.0.1:5088";
        ServerUrl = url;
        Raise(nameof(HasServerUrl));

        try
        {
            var resp = await s_healthClient.GetAsync($"{url}/api/health");
            if (resp.IsSuccessStatusCode)
            {
                IsRunning = true;
                CanStart = false;
                StatusText = "运行中";
                StatusColor = "#16A34A";
                DetectMessage = "✅ BuildServer 正在运行";
                DetectColor = "#16A34A";
                return;
            }
        }
        catch { }

        IsRunning = false;
        StatusText = !string.IsNullOrEmpty(ExePath) ? "已就绪" : "未检测";
        StatusColor = !string.IsNullOrEmpty(ExePath) ? "#3B82F6" : "#94A3B8";
    }

    public void StartBuildServer()
    {
        if (string.IsNullOrEmpty(ExePath)) return;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ExePath,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = Path.GetDirectoryName(ExePath)!
            };

            _buildServerProcess = Process.Start(psi);
            IsRunning = true;
            CanStart = false;
            StatusText = "运行中";
            StatusColor = "#16A34A";
            DetectMessage = "✅ BuildServer 已启动";
            DetectColor = "#16A34A";
        }
        catch (Exception ex)
        {
            DetectMessage = $"❌ 启动失败: {ex.Message}";
            DetectColor = "#DC2626";
        }
    }

    public void StopBuildServer()
    {
        try
        {
            if (_buildServerProcess is not null && !_buildServerProcess.HasExited)
            {
                _buildServerProcess.Kill();
                _buildServerProcess = null;
            }
            IsRunning = false;
            CanStart = !string.IsNullOrEmpty(ExePath);
            StatusText = CanStart ? "已就绪" : "未检测";
            StatusColor = CanStart ? "#3B82F6" : "#94A3B8";
            DetectMessage = "BuildServer 已停止";
            DetectColor = "#64748B";
        }
        catch (Exception ex)
        {
            DetectMessage = $"停止失败: {ex.Message}";
            DetectColor = "#DC2626";
        }
    }

    public void OpenInBrowser()
    {
        try
        {
            string url = ServerUrl;
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch { }
    }
}
