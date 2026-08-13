using Avalonia.Controls;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class StoragePage : UserControl
{
    public StoragePage()
    {
        InitializeComponent();
        DataContext = new StoragePageViewModel();
    }

    private StoragePageViewModel VM => (StoragePageViewModel)DataContext!;
    private void Refresh_Click(object? sender, RoutedEventArgs e) => VM.RefreshConfigs();
    private void DeleteSelected_Click(object? sender, RoutedEventArgs e) => VM.DeleteSelected();
    private void SelectAll_Click(object? sender, RoutedEventArgs e) => VM.ToggleSelectAll();
    private void DeleteSingle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RunFolder folder)
            VM.DeleteFolder(folder);
    }
    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RunFolder folder)
            VM.OpenFolder(folder.FullPath);
    }
}
