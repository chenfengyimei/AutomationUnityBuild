using DesktopApp.ViewModels;

namespace DesktopApp.Models;

public class ProjectProfile : ViewModelBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    private string _name = "";
    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    private string _repositoryUrl = "";
    public string RepositoryUrl
    {
        get => _repositoryUrl;
        set => Set(ref _repositoryUrl, value);
    }

    private string _branch = "main";
    public string Branch
    {
        get => _branch;
        set => Set(ref _branch, value);
    }

    private string _projectDirectoryName = "";
    public string ProjectDirectoryName
    {
        get => _projectDirectoryName;
        set => Set(ref _projectDirectoryName, value);
    }

    private string _unityProjectRelativePath = ".";
    public string UnityProjectRelativePath
    {
        get => _unityProjectRelativePath;
        set => Set(ref _unityProjectRelativePath, value);
    }

    private string _unityVersion = "";
    public string UnityVersion
    {
        get => _unityVersion;
        set => Set(ref _unityVersion, value);
    }

    private string _unityExecutablePath = "";
    public string UnityExecutablePath
    {
        get => _unityExecutablePath;
        set => Set(ref _unityExecutablePath, value);
    }

    private string _unityBuildMethod = "";
    public string UnityBuildMethod
    {
        get => _unityBuildMethod;
        set => Set(ref _unityBuildMethod, value);
    }

    private string _workspaceRoot = "~/UnityBuildWorkspace";
    public string WorkspaceRoot
    {
        get => _workspaceRoot;
        set => Set(ref _workspaceRoot, value);
    }

    private string _artifactsRoot = "~/UnityBuildArtifacts";
    public string ArtifactsRoot
    {
        get => _artifactsRoot;
        set => Set(ref _artifactsRoot, value);
    }

    private string _productName = "";
    public string ProductName
    {
        get => _productName;
        set => Set(ref _productName, value);
    }

    private string _bundleIdentifier = "";
    public string BundleIdentifier
    {
        get => _bundleIdentifier;
        set => Set(ref _bundleIdentifier, value);
    }

    public ProjectProfile Clone()
    {
        return new ProjectProfile
        {
            Id = Id,
            Name = Name,
            RepositoryUrl = RepositoryUrl,
            Branch = Branch,
            ProjectDirectoryName = ProjectDirectoryName,
            UnityProjectRelativePath = UnityProjectRelativePath,
            UnityVersion = UnityVersion,
            UnityExecutablePath = UnityExecutablePath,
            UnityBuildMethod = UnityBuildMethod,
            WorkspaceRoot = WorkspaceRoot,
            ArtifactsRoot = ArtifactsRoot,
            ProductName = ProductName,
            BundleIdentifier = BundleIdentifier
        };
    }

    public override string ToString() => string.IsNullOrEmpty(Name) ? "(未命名项目)" : Name;
}
