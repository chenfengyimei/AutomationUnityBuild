using System.Collections.ObjectModel;
using DesktopApp.Models;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class ProjectPageViewModel : ViewModelBase
{
    private ProjectProfile? _selectedProfile;
    private string _statusMessage = "";
    private bool _isEditing;
    private bool _isNew;

    public ObservableCollection<ProjectProfile> Profiles { get; } = new();

    private ProjectProfile _editProfile = new();
    public ProjectProfile EditProfile
    {
        get => _editProfile;
        set => Set(ref _editProfile, value);
    }

    public ProjectProfile? SelectedProfile
    {
        get => _selectedProfile;
        set => Set(ref _selectedProfile, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => Set(ref _statusMessage, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    public bool IsNew
    {
        get => _isNew;
        set => Set(ref _isNew, value);
    }

    public ProjectPageViewModel()
    {
        Refresh();
    }

    public void Refresh()
    {
        Profiles.Clear();
        try
        {
            foreach (var p in ProfileStore.LoadProjects())
                Profiles.Add(p);
            StatusMessage = $"共 {Profiles.Count} 个项目模板。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    public void StartNew()
    {
        EditProfile = new ProjectProfile();
        IsEditing = true;
        IsNew = true;
        StatusMessage = "正在创建新项目模板。";
    }

    public void StartEdit()
    {
        if (SelectedProfile is null)
        {
            StatusMessage = "请先选择一个项目模板。";
            return;
        }
        EditProfile = SelectedProfile.Clone();
        IsEditing = true;
        IsNew = false;
        StatusMessage = "正在编辑项目模板。";
    }

    public void Save()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(EditProfile.Name))
            {
                StatusMessage = "❌ 模板名称不能为空。";
                return;
            }

            var list = ProfileStore.LoadProjects();
            if (IsNew)
            {
                list.Add(EditProfile);
            }
            else
            {
                int idx = list.FindIndex(x => x.Id == EditProfile.Id);
                if (idx >= 0)
                    list[idx] = EditProfile;
                else
                    list.Add(EditProfile);
            }
            ProfileStore.SaveProjects(list);
            StatusMessage = $"✅ 已保存项目模板: {EditProfile.Name}";
            IsEditing = false;
            IsNew = false;
            Refresh();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 保存失败: {ex.Message}";
        }
    }

    public void CancelEdit()
    {
        IsEditing = false;
        IsNew = false;
        StatusMessage = "已取消编辑。";
    }

    public void Delete(ProjectProfile item)
    {
        try
        {
            var list = ProfileStore.LoadProjects();
            list.RemoveAll(x => x.Id == item.Id);
            ProfileStore.SaveProjects(list);
            StatusMessage = $"已删除: {item.Name}";
            Refresh();
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除失败: {ex.Message}";
        }
    }
}
