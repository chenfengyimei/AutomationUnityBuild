using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class BuildServerPage : UserControl
{
    public BuildServerPage()
    {
        InitializeComponent();
        DataContext = new BuildServerPageViewModel();
    }

    private BuildServerPageViewModel VM => (BuildServerPageViewModel)DataContext!;

    private void Refresh_Click(object? sender, RoutedEventArgs e) => VM.RefreshStatus();
    private void Start_Click(object? sender, RoutedEventArgs e) => VM.StartBuildServer();
    private void Stop_Click(object? sender, RoutedEventArgs e) => VM.StopBuildServer();
    private void OpenBrowser_Click(object? sender, RoutedEventArgs e) => VM.OpenInBrowser();
    private void ClearPath_Click(object? sender, RoutedEventArgs e) => VM.ClearCustomPath();

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 BuildServer 可执行文件",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("可执行文件") { Patterns = new List<string> { "*.exe", "*.dll" } },
                new("所有文件") { Patterns = new List<string> { "*" } }
            }
        });

        if (files is null || files.Count == 0) return;
        VM.SetCustomPath(files[0].Path.LocalPath);
    }
}
