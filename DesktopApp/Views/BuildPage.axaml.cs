using Avalonia.Controls;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class BuildPage : UserControl
{
    public BuildPage()
    {
        InitializeComponent();
        DataContext = new BuildPageViewModel();
    }

    private BuildPageViewModel VM => (BuildPageViewModel)DataContext!;
    private void Refresh_Click(object? sender, RoutedEventArgs e) => VM.RefreshConfigs();
    private async void StartBuild_Click(object? sender, RoutedEventArgs e) => await VM.StartBuildAsync();
    private void CancelBuild_Click(object? sender, RoutedEventArgs e) => VM.CancelBuild();
    private void OpenArtifacts_Click(object? sender, RoutedEventArgs e) => VM.OpenArtifacts();
}