using System.Collections.ObjectModel;
using AutomationUnityBuildIOS;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class DoctorPageViewModel : ViewModelBase
{
    private readonly BuildRunner _runner = new();
    private ConfigItem? _selectedConfig;
    private string _logText = "";
    private bool _isRunning;
    private string _statusMessage = "选择配置后点击「检查环境」。";
    private CancellationTokenSource? _cts;

    public ObservableCollection<ConfigItem> Configs { get; } = new();

    public ConfigItem? SelectedConfig
    {
        get => _selectedConfig;
        set => Set(ref _selectedConfig, value);
    }

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

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public DoctorPageViewModel()
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

    public async Task RunDoctorAsync()
    {
        if (SelectedConfig is null)
        {
            StatusMessage = "请先选择一个配置文件。";
            return;
        }

        _cts = new CancellationTokenSource();
        IsRunning = true;
        LogText = "";
        StatusMessage = "正在检查环境...";

        var sb = new System.Text.StringBuilder();

        var (success, error) = await _runner.CheckPrerequisitesAsync(
            SelectedConfig.FullPath,
            line =>
            {
                sb.AppendLine(line);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => LogText = sb.ToString());
            },
            _cts.Token);

        IsRunning = false;
        StatusMessage = success ? "✅ 环境检查完成。" : $"❌ 环境检查失败: {error}";
    }
}