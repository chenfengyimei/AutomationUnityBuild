using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using AutomationUnityBuildIOS;

namespace DesktopApp.ViewModels;

public class ArtifactEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public bool IsDirectory { get; set; }
    public string DisplaySize => IsDirectory ? "<DIR>" : FormatBytes(SizeBytes);
    public string Icon => IsDirectory ? "📁" : GetFileIcon(Name);

    private static string GetFileIcon(string name)
    {
        string ext = Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".ipa" => "📱",
            ".apk" => "📱",
            ".aab" => "📱",
            ".log" => "📄",
            ".json" => "📄",
            ".zip" => "📦",
            _ => "📄"
        };
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:F1} MB";
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
    }
}

public class ArtifactsPageViewModel : ViewModelBase
{
    private ConfigItem? _selectedConfig;
    private string _artifactsRoot = "";
    private string _statusMessage = "选择配置后自动加载产物目录。";
    private RunFolder? _selectedRunFolder;
    private ArtifactEntry? _selectedFile;
    private string _logPreview = "";

    public ObservableCollection<ConfigItem> Configs { get; } = new();
    public ObservableCollection<RunFolder> RunFolders { get; } = new();
    public ObservableCollection<ArtifactEntry> CurrentFiles { get; } = new();

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

    public RunFolder? SelectedRunFolder
    {
        get => _selectedRunFolder;
        set
        {
            Set(ref _selectedRunFolder, value);
            LoadRunFolderContents(value);
        }
    }

    public ArtifactEntry? SelectedFile
    {
        get => _selectedFile;
        set
        {
            Set(ref _selectedFile, value);
            Raise(nameof(HasSelectedFile));
            if (value is not null && !value.IsDirectory)
                LoadFilePreview(value);
        }
    }

    public bool HasSelectedFile => _selectedFile is not null;

    public string LogPreview
    {
        get => _logPreview;
        set => Set(ref _logPreview, value);
    }

    public ArtifactsPageViewModel()
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
        CurrentFiles.Clear();
        LogPreview = "";

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
                .Take(100);

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

            StatusMessage = $"找到 {RunFolders.Count} 个历史产物目录。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载产物目录失败: {ex.Message}";
        }
    }

    private void LoadRunFolderContents(RunFolder? folder)
    {
        CurrentFiles.Clear();
        SelectedFile = null;
        LogPreview = "";

        if (folder is null) return;

        try
        {
            var entries = Directory.GetFileSystemEntries(folder.FullPath)
                .Select(p =>
                {
                    var info = new FileInfo(p);
                    var dirInfo = new DirectoryInfo(p);
                    bool isDir = dirInfo.Exists;
                    return new ArtifactEntry
                    {
                        Name = Path.GetFileName(p),
                        FullPath = p,
                        IsDirectory = isDir,
                        SizeBytes = isDir ? dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : info.Length
                    };
                })
                .OrderByDescending(e => e.IsDirectory)
                .ThenBy(e => e.Name);

            foreach (var entry in entries)
            {
                CurrentFiles.Add(entry);
            }

            var logFile = entries.FirstOrDefault(e => e.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase));
            if (logFile is not null)
            {
                try { LogPreview = File.ReadAllText(logFile.FullPath); }
                catch { }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载内容失败: {ex.Message}";
        }
    }

    private void LoadFilePreview(ArtifactEntry file)
    {
        if (file.IsDirectory) return;
        try
        {
            string ext = Path.GetExtension(file.Name).ToLowerInvariant();
            if (ext is ".log" or ".json" or ".txt" or ".xml" or ".plist")
            {
                LogPreview = File.ReadAllText(file.FullPath);
            }
        }
        catch { }
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

    public void OpenFile(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "open" });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", $"\"{path}\"");
            else
                Process.Start("xdg-open", $"\"{path}\"");
        }
        catch { }
    }

    public void RefreshArtifacts()
    {
        if (!string.IsNullOrEmpty(ArtifactsRoot))
        {
            LoadRunFolders();
        }
    }
}
