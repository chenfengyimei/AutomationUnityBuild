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
}