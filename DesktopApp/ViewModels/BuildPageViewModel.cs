using System.Collections.ObjectModel;
using AutomationUnityBuildIOS;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class BuildPageViewModel : ViewModelBase
{
    private readonly BuildRunner _runner = new();
    private ConfigItem? _selectedConfig;
    private bool _dryRun = true;
    private bool _skipGit;
    private bool _skipUnity;
    private bool _skipXcode;
    private bool _allowNonMac = true;
    private string _logText = "";
    private bool _isRunning;
    private bool _runCompleted;
    private bool _runSucceeded;
    private string _statusMessage = "选择配置后点击「开始打包」。";
    private string? _artifactsRoot;
    private CancellationTokenSource? _cts;

    public ObservableCollection<ConfigItem> Configs { get; } = new();

    public ConfigItem? SelectedConfig
    {
        get => _selectedConfig;
        set => Set(ref _selectedConfig, value);
    }

    public bool DryRun { get => _dryRun; set => Set(ref _dryRun, value); }
    public bool SkipGit { get => _skipGit; set => Set(ref _skipGit, value); }
    public bool SkipUnity { get => _skipUnity; set => Set(ref _skipUnity, value); }
    public bool SkipXcode { get => _skipXcode; set => Set(ref _skipXcode, value); }
    public bool AllowNonMac { get => _allowNonMac; set => Set(ref _allowNonMac, value); }

    public string LogText
    {
        get => _logText;
        set => Set(ref _logText, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => Set(ref _isRunning, value);
    }

    public bool RunCompleted
    {
        get => _runCompleted;
        set => Set(ref _runCompleted, value);
    }

    public bool RunSucceeded
    {
        get => _runSucceeded;
        set => Set(ref _runSucceeded, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public bool CanOpenArtifacts => _artifactsRoot is not null;

    public BuildPageViewModel()
    {
        RefreshConfigs();
    }

    public void RefreshConfigs()
    {
        Configs.Clear();
        try
        {
            foreach (var entry in ConfigFileSelector.FindConfigFiles())
            {
                var item = new ConfigItem
                {
                    FullPath = entry.FullPath,
                    DisplayPath = entry.DisplayPath,
                    DisplayName = entry.DisplayName
                };
                Configs.Add(item);
            }
        }
        catch { }
    }

    public async Task StartBuildAsync()
    {
        if (SelectedConfig is null)
        {
            StatusMessage = "请先选择一个配置文件。";
            return;
        }

        _cts = new CancellationTokenSource();
        IsRunning = true;
        RunCompleted = false;
        LogText = "";
        StatusMessage = "正在执行打包...";

        var sb = new System.Text.StringBuilder();

        var (success, logPath, artifactsRoot, error) = await _runner.RunAsync(
            SelectedConfig.FullPath,
            DryRun, SkipGit, SkipUnity, SkipXcode, AllowNonMac,
            line =>
            {
                sb.AppendLine(line);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => LogText = sb.ToString());
            },
            _cts.Token);

        IsRunning = false;
        RunCompleted = true;
        RunSucceeded = success;
        _artifactsRoot = artifactsRoot;
        Raise(nameof(CanOpenArtifacts));
        StatusMessage = success ? "✅ 打包成功！" : $"❌ 打包失败: {error}";
    }

    public void CancelBuild()
    {
        _cts?.Cancel();
        StatusMessage = "正在取消...";
    }

    public void OpenArtifacts()
    {
        if (_artifactsRoot is null) return;
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", _artifactsRoot) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                System.Diagnostics.Process.Start("open", $"\"{_artifactsRoot}\"");
            else
                System.Diagnostics.Process.Start("xdg-open", $"\"{_artifactsRoot}\"");
        }
        catch { }
    }
}
