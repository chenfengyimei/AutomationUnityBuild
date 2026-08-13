using System.Collections.ObjectModel;
using DesktopApp.Models;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class SigningProfilePageViewModel : ViewModelBase
{
    private SigningProfile? _selected;
    private string _statusMessage = "";
    private bool _isEditing, _isNew;

    public ObservableCollection<SigningProfile> Profiles { get; } = new();
    private SigningProfile _edit = new();
    public SigningProfile EditProfile
    {
        get => _edit;
        set { if (_edit is not null) _edit.PropertyChanged -= OnPC; Set(ref _edit, value); if (_edit is not null) _edit.PropertyChanged += OnPC; RaiseFlags(); }
    }
    void OnPC(object? s, System.ComponentModel.PropertyChangedEventArgs e) { if (e.PropertyName == nameof(SigningProfile.Platform)) RaiseFlags(); }
    void RaiseFlags() { Raise(nameof(IsEditIos)); Raise(nameof(IsEditAndroid)); }
    public SigningProfile? SelectedProfile { get => _selected; set => Set(ref _selected, value); }
    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
    public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }
    public bool IsNew { get => _isNew; set => Set(ref _isNew, value); }
    public bool IsEditIos => EditProfile.IsIos;
    public bool IsEditAndroid => EditProfile.IsAndroid;

    public SigningProfilePageViewModel() => Refresh();

    public void Refresh()
    {
        Profiles.Clear();
        foreach (var p in ProfileStore.LoadSigningProfiles()) Profiles.Add(p);
        StatusMessage = $"共 {Profiles.Count} 个签名模板。";
    }

    public void StartNew()
    {
        EditProfile = new SigningProfile();
        IsEditing = true; IsNew = true;
        StatusMessage = "正在创建新签名模板。";
    }

    public void StartEdit()
    {
        if (SelectedProfile is null) { StatusMessage = "请先选择一个签名模板。"; return; }
        EditProfile = SelectedProfile.Clone();
        IsEditing = true; IsNew = false;
        StatusMessage = "正在编辑签名模板。";
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(EditProfile.Name)) { StatusMessage = "❌ 名称不能为空。"; return; }
        var list = ProfileStore.LoadSigningProfiles();
        if (IsNew) list.Add(EditProfile);
        else { int i = list.FindIndex(x => x.Id == EditProfile.Id); if (i >= 0) list[i] = EditProfile; else list.Add(EditProfile); }
        ProfileStore.SaveSigningProfiles(list);
        StatusMessage = $"✅ 已保存: {EditProfile.Name}";
        IsEditing = false; IsNew = false; Refresh();
    }

    public void CancelEdit() { IsEditing = false; IsNew = false; StatusMessage = "已取消。"; }

    public void Delete(SigningProfile item)
    {
        var list = ProfileStore.LoadSigningProfiles();
        list.RemoveAll(x => x.Id == item.Id);
        ProfileStore.SaveSigningProfiles(list);
        StatusMessage = $"已删除: {item.Name}";
        Refresh();
    }
}
