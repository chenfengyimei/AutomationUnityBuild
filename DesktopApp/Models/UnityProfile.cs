using DesktopApp.ViewModels;

namespace DesktopApp.Models;

public class UnityProfile : ViewModelBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = "";
    public string Name { get => _name; set => Set(ref _name, value); }

    private string _unityProjectRelativePath = ".";
    public string UnityProjectRelativePath { get => _unityProjectRelativePath; set => Set(ref _unityProjectRelativePath, value); }

    private string _unityVersion = "";
    public string UnityVersion { get => _unityVersion; set => Set(ref _unityVersion, value); }

    private string _unityExecutablePath = "";
    public string UnityExecutablePath { get => _unityExecutablePath; set => Set(ref _unityExecutablePath, value); }

    private string _unityBuildMethod = "";
    public string UnityBuildMethod { get => _unityBuildMethod; set => Set(ref _unityBuildMethod, value); }

    private string _productName = "";
    public string ProductName { get => _productName; set => Set(ref _productName, value); }

    private string _bundleIdentifier = "";
    public string BundleIdentifier { get => _bundleIdentifier; set => Set(ref _bundleIdentifier, value); }

    public UnityProfile Clone() => new()
    {
        Id = Id, Name = Name,
        UnityProjectRelativePath = UnityProjectRelativePath,
        UnityVersion = UnityVersion,
        UnityExecutablePath = UnityExecutablePath,
        UnityBuildMethod = UnityBuildMethod,
        ProductName = ProductName,
        BundleIdentifier = BundleIdentifier
    };

    public override string ToString() => string.IsNullOrEmpty(Name) ? "(未命名)" : Name;
}
