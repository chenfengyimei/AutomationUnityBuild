using System.Collections.ObjectModel;
using System.Diagnostics;
using AutomationUnityBuildIOS;

namespace DesktopApp.ViewModels;

public class StoragePageViewModel : ViewModelBase
{
    private ConfigItem? _selectedConfig;
    private string _artifactsRoot = "";
    private string _statusMessage = "选择配置后自动加载产物目录。";
    private long _totalBytes;
    private int _totalFolders;

    public ObservableCollection<ConfigItem> Configs { get; } = new();
    public ObservableCollection<RunFolder> RunFolders { get; } = new();
    public ObservableCollection<RunFolder> SelectedFolders { get; } = new();

    public ConfigItem? SelectedConfig
    {
        get => _selectedConfig;
        set
        {
            Set(ref _selectedConfig, value);
            LoadArtifactsRoot(value);
        }
    }

    public string ArtifactsRoot
    {
        get => _artifactsRoot;
        set => Set(ref _artifactsRoot, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public long TotalBytes
    {
        get => _totalBytes;
        set => Set(ref _totalBytes, value);
    }

    public int TotalFolders
    {
        get => _totalFolders;
        set => Set(ref _totalFolders, value);
    }

    public string TotalDisplay => ArtifactEntry.FormatBytes(TotalBytes);

    public StoragePageViewModel()
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

    private void LoadArtifactsRoot(ConfigItem? config)
    {
        RunFolders.Clear();
        SelectedFolders.Clear();
        TotalBytes = 0;
        TotalFolders = 0;

        if (config is null) return;

        try
        {
            var json = File.ReadAllText(config.FullPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("artifactsRoot", out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                ArtifactsRoot = PathTools.ExpandHome(el.GetString() ?? "");
            }
            else
            {
                StatusMessage = "配置文件中没有 artifactsRoot 字段。";
                return;
            }

            if (!Directory.Exists(ArtifactsRoot))
            {
                StatusMessage = $"产物目录不存在: {ArtifactsRoot}";
                return;
            }

            LoadRunFolders();
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    private void LoadRunFolders()
    {
        RunFolders.Clear();
        try
        {
            var dirs = Directory.GetDirectories(ArtifactsRoot)
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d => d.CreationTime)
                .Take(200);

            foreach (var dir in dirs)
            {
                var files = dir.GetFiles("*", SearchOption.AllDirectories);
                var run = new RunFolder
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    Created = dir.CreationTime,
                    FileCount = files.Length,
                    TotalBytes = files.Sum(f => f.Length)
                };
                RunFolders.Add(run);
            }

            TotalFolders = RunFolders.Count;
            TotalBytes = RunFolders.Sum(r => r.TotalBytes);
            Raise(nameof(TotalDisplay));
            StatusMessage = $"找到 {TotalFolders} 个历史产物目录，共占用 {TotalDisplay}。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载产物目录失败: {ex.Message}";
        }
    }

    public void DeleteFolder(RunFolder folder)
    {
        try
        {
            Directory.Delete(folder.FullPath, recursive: true);
            RunFolders.Remove(folder);
            SelectedFolders.Remove(folder);
            TotalFolders = RunFolders.Count;
            TotalBytes = RunFolders.Sum(r => r.TotalBytes);
            Raise(nameof(TotalDisplay));
            StatusMessage = $"已删除: {folder.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除失败: {ex.Message}";
        }
    }

    public void DeleteSelected()
    {
        int deleted = 0;
        foreach (var folder in SelectedFolders.ToList())
        {
            try
            {
                Directory.Delete(folder.FullPath, recursive: true);
                RunFolders.Remove(folder);
                deleted++;
            }
            catch { }
        }
        SelectedFolders.Clear();
        TotalFolders = RunFolders.Count;
        TotalBytes = RunFolders.Sum(r => r.TotalBytes);
        Raise(nameof(TotalDisplay));
        StatusMessage = $"已批量删除 {deleted} 个目录。";
    }

    public void OpenFolder(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", $"\"{path}\"");
            else
                Process.Start("xdg-open", $"\"{path}\"");
        }
        catch { }
    }

    public void RefreshStorage()
    {
        if (!string.IsNullOrEmpty(ArtifactsRoot))
        {
            LoadRunFolders();
        }
    }
}