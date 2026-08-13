using Avalonia.Controls;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class SyncPage : UserControl
{
    public SyncPage()
    {
        InitializeComponent();
        DataContext = new SyncPageViewModel();
    }

    private SyncPageViewModel VM => (SyncPageViewModel)DataContext!;

    private async void Login_Click(object? sender, RoutedEventArgs e) => await VM.LoginAsync();
    private async void PullProjects_Click(object? sender, RoutedEventArgs e) => await VM.PullProjectsAsync();
    private async void PushProjects_Click(object? sender, RoutedEventArgs e) => await VM.PushProjectsAsync();
    private async void PullCerts_Click(object? sender, RoutedEventArgs e) => await VM.PullCertsAsync();
    private async void PushCerts_Click(object? sender, RoutedEventArgs e) => await VM.PushCertsAsync();
    private async void RefreshConfigs_Click(object? sender, RoutedEventArgs e) => await VM.RefreshServerConfigsAsync();
    private async void DownloadConfig_Click(object? sender, RoutedEventArgs e) => await VM.DownloadSelectedConfigAsync();
}
