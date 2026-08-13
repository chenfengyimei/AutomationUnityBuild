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
        if (VM.SelectedFile is not null && !VM.SelectedFile.IsDirectory)
            VM.OpenFile(VM.SelectedFile.FullPath);
    }
    private void FileItem_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (VM.SelectedFile is not null)
        {
            if (VM.SelectedFile.IsDirectory)
                VM.OpenFolder(VM.SelectedFile.FullPath);
            else
                VM.OpenFile(VM.SelectedFile.FullPath);
        }
    }
}
