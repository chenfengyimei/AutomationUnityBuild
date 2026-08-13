using System.ComponentModel;
using DesktopApp.Views;

namespace DesktopApp.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private object _currentPage = null!;
    public object CurrentPage
    {
        get => _currentPage;
        set { _currentPage = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPage))); }
    }

    public string VersionText => "v1.0.0";
    public bool ShowBuildPage => true;

    private readonly ConfigPage _configPage = new();
    private readonly ProjectPage _projectPage = new();
    private readonly CertificatePage _certificatePage = new();
    private readonly BuildPage _buildPage = new();
    private readonly DoctorPage _doctorPage = new();
    private readonly ArtifactsPage _artifactsPage = new();
    private readonly StoragePage _storagePage = new();
    private readonly EmailSettingsPage _emailPage = new();
    private readonly HelpPage _helpPage = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NavigateTo(string tag)
    {
        CurrentPage = tag switch
        {
            "build" => _buildPage,
            "config" => _configPage,
            "project" => _projectPage,
            "certificate" => _certificatePage,
            "doctor" => _doctorPage,
            "artifacts" => _artifactsPage,
            "storage" => _storagePage,
            "email" => _emailPage,
            "help" => _helpPage,
            _ => _configPage
        };
    }
}
