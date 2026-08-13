using System.Collections.ObjectModel;
using DesktopApp.Models;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class UnityProfilePageViewModel : ViewModelBase
{
    private UnityProfile? _selected;
    private string _statusMessage = "";
    private bool _isEditing, _isNew;

    public ObservableCollection<UnityProfile> Profiles { get; } = new();
    private UnityProfile _edit = new();
    public UnityProfile EditProfile { get => _edit; set => Set(ref _edit, value); }
    public UnityProfile? SelectedProfile { get => _selected; set => Set(ref _selected, value); }
    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
    public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }
    public bool IsNew { get => _isNew; set => Set(ref _isNew, value); }

    public UnityProfilePageViewModel() => Refresh();

    public void Refresh()
    {
        Profiles.Clear();
        foreach (var p in ProfileStore.LoadUnityProfiles()) Profiles.Add(p);
        StatusMessage = $"共 {Profiles.Count} 个工程模板。";
    }

    public void StartNew()
    {
        EditProfile = new UnityProfile();
        IsEditing = true; IsNew = true;
        StatusMessage = "正在创建新工程模板。";
    }

    public void StartEdit()
    {
        if (SelectedProfile is null) { StatusMessage = "请先选择一个工程模板。"; return; }
        EditProfile = SelectedProfile.Clone();
        IsEditing = true; IsNew = false;
        StatusMessage = "正在编辑工程模板。";
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(EditProfile.Name)) { StatusMessage = "❌ 名称不能为空。"; return; }
        var list = ProfileStore.LoadUnityProfiles();
        if (IsNew) list.Add(EditProfile);
        else { int i = list.FindIndex(x => x.Id == EditProfile.Id); if (i >= 0) list[i] = EditProfile; else list.Add(EditProfile); }
        ProfileStore.SaveUnityProfiles(list);
        StatusMessage = $"✅ 已保存: {EditProfile.Name}";
        IsEditing = false; IsNew = false; Refresh();
    }

    public void CancelEdit() { IsEditing = false; IsNew = false; StatusMessage = "已取消。"; }

    public void Delete(UnityProfile item)
    {
        var list = ProfileStore.LoadUnityProfiles();
        list.RemoveAll(x => x.Id == item.Id);
        ProfileStore.SaveUnityProfiles(list);
        StatusMessage = $"已删除: {item.Name}";
        Refresh();
    }
}
