using System.Collections.ObjectModel;
using System.Diagnostics;
using AutomationUnityBuildIOS;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class RunFolder : ViewModelBase
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long TotalBytes { get; set; }
    public int FileCount { get; set; }
    public DateTimeOffset Created { get; set; }
    public string DisplaySize => ArtifactEntry.FormatBytes(TotalBytes);

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }
}

public class StoragePageViewModel : ViewModelBase
{
    private ConfigItem? _selectedConfig;
    private string _artifactsRoot = "";
    private string _statusMessage = "选择配置后自动加载产物目录。";
    private long _totalBytes;
    private int _totalFolders;
    private int _selectedCount;
    private bool _hasSelected;

    public ObservableCollection<ConfigItem> Configs { get; } = new();
    public ObservableCollection<RunFolder> RunFolders { get; } = new();

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

    public int SelectedCount
    {
        get => _selectedCount;
        set => Set(ref _selectedCount, value);
    }

    public bool HasSelected
    {
        get => _hasSelected;
        set => Set(ref _hasSelected, value);
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
            foreach (var entry in ConfigFileSelector.FindConfigFiles(DesktopPaths.DataRoot))
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
        TotalBytes = 0;
        TotalFolders = 0;
        UpdateSelectedCount();

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
                run.PropertyChanged += (_, _) => UpdateSelectedCount();
                RunFolders.Add(run);
            }

            TotalFolders = RunFolders.Count;
            TotalBytes = RunFolders.Sum(r => r.TotalBytes);
            Raise(nameof(TotalDisplay));
            UpdateSelectedCount();
            StatusMessage = $"找到 {TotalFolders} 个历史产物目录，共占用 {TotalDisplay}。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载产物目录失败: {ex.Message}";
        }
    }

    private void UpdateSelectedCount()
    {
        int count = RunFolders.Count(r => r.IsSelected);
        SelectedCount = count;
        HasSelected = count > 0;
    }

    public void ToggleSelectAll()
    {
        bool allSelected = RunFolders.Count > 0 && RunFolders.All(r => r.IsSelected);
        foreach (var folder in RunFolders)
            folder.IsSelected = !allSelected;
        UpdateSelectedCount();
    }

    public void DeleteFolder(RunFolder folder)
    {
        try
        {
            Directory.Delete(folder.FullPath, recursive: true);
            RunFolders.Remove(folder);
            TotalFolders = RunFolders.Count;
            TotalBytes = RunFolders.Sum(r => r.TotalBytes);
            Raise(nameof(TotalDisplay));
            UpdateSelectedCount();
            StatusMessage = $"已删除: {folder.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除失败: {ex.Message}";
        }
    }

    public void DeleteSelected()
    {
        var toDelete = RunFolders.Where(r => r.IsSelected).ToList();
        if (toDelete.Count == 0)
        {
            StatusMessage = "请先勾选要删除的目录。";
            return;
        }

        int deleted = 0;
        var errors = new List<string>();
        foreach (var folder in toDelete)
        {
            try
            {
                Directory.Delete(folder.FullPath, recursive: true);
                RunFolders.Remove(folder);
                deleted++;
            }
            catch (Exception ex)
            {
                errors.Add($"{folder.Name}: {ex.Message}");
            }
        }
        TotalFolders = RunFolders.Count;
        TotalBytes = RunFolders.Sum(r => r.TotalBytes);
        Raise(nameof(TotalDisplay));
        UpdateSelectedCount();
        StatusMessage = errors.Count > 0
            ? $"已删除 {deleted} 个目录，{errors.Count} 个失败：{string.Join("; ", errors)}"
            : $"已批量删除 {deleted} 个目录。";
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
