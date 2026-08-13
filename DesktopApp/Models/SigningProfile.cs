using DesktopApp.ViewModels;

namespace DesktopApp.Models;

public class SigningProfile : ViewModelBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _platform = "ios";
    public string Platform { get => _platform; set => Set(ref _platform, value); }

    // iOS
    private string _teamId = "";
    public string TeamId { get => _teamId; set => Set(ref _teamId, value); }

    private string _exportMethod = "development";
    public string ExportMethod { get => _exportMethod; set => Set(ref _exportMethod, value); }

    private string _signingStyle = "automatic";
    public string SigningStyle { get => _signingStyle; set => Set(ref _signingStyle, value); }

    private string _iosDeploymentTarget = "";
    public string IosDeploymentTarget { get => _iosDeploymentTarget; set => Set(ref _iosDeploymentTarget, value); }

    // Android
    private string _androidKeystoreName = "";
    public string AndroidKeystoreName { get => _androidKeystoreName; set => Set(ref _androidKeystoreName, value); }

    private string _androidKeystorePass = "";
    public string AndroidKeystorePass { get => _androidKeystorePass; set => Set(ref _androidKeystorePass, value); }

    private string _androidKeyaliasName = "";
    public string AndroidKeyaliasName { get => _androidKeyaliasName; set => Set(ref _androidKeyaliasName, value); }

    private string _androidKeyaliasPass = "";
    public string AndroidKeyaliasPass { get => _androidKeyaliasPass; set => Set(ref _androidKeyaliasPass, value); }

    public bool IsIos => string.Equals(Platform, "ios", StringComparison.OrdinalIgnoreCase)
                      || string.Equals(Platform, "all", StringComparison.OrdinalIgnoreCase);
    public bool IsAndroid => string.Equals(Platform, "android", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(Platform, "all", StringComparison.OrdinalIgnoreCase);

    public SigningProfile Clone() => new()
    {
        Id = Id, Name = Name, Platform = Platform,
        TeamId = TeamId, ExportMethod = ExportMethod, SigningStyle = SigningStyle,
        IosDeploymentTarget = IosDeploymentTarget,
        AndroidKeystoreName = AndroidKeystoreName, AndroidKeystorePass = AndroidKeystorePass,
        AndroidKeyaliasName = AndroidKeyaliasName, AndroidKeyaliasPass = AndroidKeyaliasPass
    };

    public override string ToString() => string.IsNullOrEmpty(Name) ? "(未命名)" : Name;
}
