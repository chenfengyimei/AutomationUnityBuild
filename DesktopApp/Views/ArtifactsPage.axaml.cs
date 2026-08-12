using Avalonia.Controls;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class ArtifactsPage : UserControl
{
    public ArtifactsPage()
    {
        InitializeComponent();
        DataContext = new ArtifactsPageViewModel();
    }

    private ArtifactsPageViewModel VM => (ArtifactsPageViewModel)DataContext!;
    private void Refresh_Click(object? sender, RoutedEventArgs e) => VM.RefreshConfigs();
    private void OpenDir_Click(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedRunFolder is not null) VM.OpenFolder(VM.SelectedRunFolder.FullPath);
    }
    private void OpenFile_Click(object? sender, RoutedEventArgs e)
    {
        if (VM.CurrentFiles.Count > 0 && VM.CurrentFiles.Count > 0)
        {
            var item = VM.CurrentFiles[0];
            if (!item.IsDirectory) VM.OpenFile(item.FullPath);
        }
    }
}