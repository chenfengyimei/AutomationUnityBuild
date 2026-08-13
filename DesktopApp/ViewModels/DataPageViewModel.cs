using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using DesktopApp.Models;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class DataPageViewModel : ViewModelBase
{
    private string _statusMessage = "";
    private bool _isBusy;

    public ObservableCollection<ExportCategory> Categories { get; } = new()
    {
        new("projects", "项目模板（ProjectProfile）"),
        new("unityProfiles", "工程模板（UnityProfile）"),
        new("signingProfiles", "签名模板（SigningProfile）"),
        new("certificateProfiles", "证书模板（CertificateProfile）"),
        new("configs", "配置文件（configs/ 目录）"),
        new("serverSettings", "服务器连接设置")
    };

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    public DataPageViewModel() { }

    public void SelectAll()
    {
        foreach (var c in Categories) c.Selected = true;
    }

    public void DeselectAll()
    {
        foreach (var c in Categories) c.Selected = false;
    }

    public async Task ExportAsync(string exportPath)
    {
        IsBusy = true;
        StatusMessage = "正在导出...";
        try
        {
            var data = new JsonObject();
            foreach (var cat in Categories.Where(c => c.Selected))
            {
                switch (cat.Key)
                {
                    case "projects":
                        data["projects"] = JsonSerializer.SerializeToNode(ProfileStore.LoadProjects())!;
                        break;
                    case "unityProfiles":
                        data["unityProfiles"] = JsonSerializer.SerializeToNode(ProfileStore.LoadUnityProfiles())!;
                        break;
                    case "signingProfiles":
                        data["signingProfiles"] = JsonSerializer.SerializeToNode(ProfileStore.LoadSigningProfiles())!;
                        break;
                    case "certificateProfiles":
                        data["certificateProfiles"] = JsonSerializer.SerializeToNode(ProfileStore.LoadCertificates())!;
                        break;
                    case "serverSettings":
                        var path = Path.Combine(Environment.CurrentDirectory, "profiles", "server-settings.json");
                        if (File.Exists(path))
                            data["serverSettings"] = JsonNode.Parse(File.ReadAllText(path))!;
                        break;
                    case "configs":
                        var configsDir = Path.Combine(Environment.CurrentDirectory, "configs");
                        var configsNode = new JsonObject();
                        if (Directory.Exists(configsDir))
                        {
                            foreach (var f in Directory.GetFiles(configsDir, "*.json"))
                            {
                                configsNode[Path.GetFileName(f)] = JsonNode.Parse(File.ReadAllText(f))!;
                            }
                        }
                        data["configs"] = configsNode;
                        break;
                }
            }

            string json = data.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(exportPath, json + Environment.NewLine);
            StatusMessage = $"✅ 已导出到: {exportPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 导出失败: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    public async Task ImportAsync(string importPath)
    {
        IsBusy = true;
        StatusMessage = "正在导入...";
        try
        {
            string json = await File.ReadAllTextAsync(importPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int imported = 0;

            if (root.TryGetProperty("projects", out var pEl))
            {
                var items = JsonSerializer.Deserialize<List<ProjectProfile>>(pEl.GetRawText()) ?? new();
                var existing = ProfileStore.LoadProjects();
                foreach (var item in items)
                {
                    if (!existing.Any(x => x.Id == item.Id)) { existing.Add(item); imported++; }
                }
                ProfileStore.SaveProjects(existing);
            }

            if (root.TryGetProperty("unityProfiles", out var uEl))
            {
                var items = JsonSerializer.Deserialize<List<UnityProfile>>(uEl.GetRawText()) ?? new();
                var existing = ProfileStore.LoadUnityProfiles();
                foreach (var item in items)
                {
                    if (!existing.Any(x => x.Id == item.Id)) { existing.Add(item); imported++; }
                }
                ProfileStore.SaveUnityProfiles(existing);
            }

            if (root.TryGetProperty("signingProfiles", out var sEl))
            {
                var items = JsonSerializer.Deserialize<List<SigningProfile>>(sEl.GetRawText()) ?? new();
                var existing = ProfileStore.LoadSigningProfiles();
                foreach (var item in items)
                {
                    if (!existing.Any(x => x.Id == item.Id)) { existing.Add(item); imported++; }
                }
                ProfileStore.SaveSigningProfiles(existing);
            }

            if (root.TryGetProperty("certificateProfiles", out var cEl))
            {
                var items = JsonSerializer.Deserialize<List<CertificateProfile>>(cEl.GetRawText()) ?? new();
                var existing = ProfileStore.LoadCertificates();
                foreach (var item in items)
                {
                    if (!existing.Any(x => x.Id == item.Id)) { existing.Add(item); imported++; }
                }
                ProfileStore.SaveCertificates(existing);
            }

            if (root.TryGetProperty("serverSettings", out var ssEl))
            {
                var dir = Path.Combine(Environment.CurrentDirectory, "profiles");
                Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(Path.Combine(dir, "server-settings.json"),
                    JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(ssEl.GetRawText()),
                    new JsonSerializerOptions { WriteIndented = true }));
                imported++;
            }

            if (root.TryGetProperty("configs", out var cfgEl) && cfgEl.ValueKind == JsonValueKind.Object)
            {
                var configsDir = Path.Combine(Environment.CurrentDirectory, "configs");
                Directory.CreateDirectory(configsDir);
                foreach (var prop in cfgEl.EnumerateObject())
                {
                    string filePath = Path.Combine(configsDir, prop.Name);
                    await File.WriteAllTextAsync(filePath, prop.Value.GetRawText());
                    imported++;
                }
            }

            StatusMessage = $"✅ 导入完成: 新增 {imported} 条记录（已存在的按 ID 跳过）。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 导入失败: {ex.Message}";
        }
        finally { IsBusy = false; }
    }
}

public class ExportCategory : ViewModelBase
{
    public string Key { get; }
    public string Label { get; }

    private bool _selected = true;
    public bool Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public ExportCategory(string key, string label)
    {
        Key = key;
        Label = label;
    }
}
