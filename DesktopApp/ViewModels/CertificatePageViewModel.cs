using System.Collections.ObjectModel;
using DesktopApp.Models;
using DesktopApp.Services;

namespace DesktopApp.ViewModels;

public class CertificatePageViewModel : ViewModelBase
{
    private CertificateProfile? _selectedProfile;
    private string _statusMessage = "";
    private bool _isEditing;
    private bool _isNew;

    public ObservableCollection<CertificateProfile> Profiles { get; } = new();

    private CertificateProfile _editProfile = new();
    public CertificateProfile EditProfile
    {
        get => _editProfile;
        set
        {
            if (_editProfile is not null)
                _editProfile.PropertyChanged -= OnEditProfilePropertyChanged;
            Set(ref _editProfile, value);
            if (_editProfile is not null)
                _editProfile.PropertyChanged += OnEditProfilePropertyChanged;
            RaisePlatformFlags();
        }
    }

    private void OnEditProfilePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CertificateProfile.Platform))
            RaisePlatformFlags();
    }

    private void RaisePlatformFlags()
    {
        Raise(nameof(IsEditIos));
        Raise(nameof(IsEditAndroid));
        Raise(nameof(IsEditTiktok));
    }

    public CertificateProfile? SelectedProfile
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

    public bool IsEditIos => string.Equals(EditProfile.Platform, "ios", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(EditProfile.Platform, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsEditAndroid => string.Equals(EditProfile.Platform, "android", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(EditProfile.Platform, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsEditTiktok => string.Equals(EditProfile.Platform, "tiktok", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(EditProfile.Platform, "all", StringComparison.OrdinalIgnoreCase);

    public CertificatePageViewModel()
    {
        Refresh();
    }

    public void Refresh()
    {
        Profiles.Clear();
        try
        {
            foreach (var c in ProfileStore.LoadCertificates())
                Profiles.Add(c);
            StatusMessage = $"共 {Profiles.Count} 个证书模板。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    public void StartNew()
    {
        EditProfile = new CertificateProfile();
        IsEditing = true;
        IsNew = true;
        StatusMessage = "正在创建新证书模板。";
    }

    public void StartEdit()
    {
        if (SelectedProfile is null)
        {
            StatusMessage = "请先选择一个证书模板。";
            return;
        }
        EditProfile = SelectedProfile.Clone();
        IsEditing = true;
        IsNew = false;
        StatusMessage = "正在编辑证书模板。";
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

            var list = ProfileStore.LoadCertificates();
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
            ProfileStore.SaveCertificates(list);
            StatusMessage = $"✅ 已保存证书模板: {EditProfile.Name}";
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

    public void Delete(CertificateProfile item)
    {
        try
        {
            var list = ProfileStore.LoadCertificates();
            list.RemoveAll(x => x.Id == item.Id);
            ProfileStore.SaveCertificates(list);
            StatusMessage = $"已删除: {item.Name}";
            Refresh();
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除失败: {ex.Message}";
        }
    }
}
