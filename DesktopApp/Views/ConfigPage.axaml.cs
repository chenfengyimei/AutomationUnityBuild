using Avalonia.Controls;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class ConfigPage : UserControl
{
    public ConfigPage()
    {
        InitializeComponent();
        DataContext = new ConfigPageViewModel();
    }

    private ConfigPageViewModel VM => (ConfigPageViewModel)DataContext!;

    private void Refresh_Click(object? sender, RoutedEventArgs e) => VM.RefreshConfigs();
    private void CreateIos_Click(object? sender, RoutedEventArgs e) => VM.StartNew("ios");
    private void CreateAndroid_Click(object? sender, RoutedEventArgs e) => VM.StartNew("android");
    private void CreateTiktok_Click(object? sender, RoutedEventArgs e) => VM.StartNew("tiktok");
    private void OpenDir_Click(object? sender, RoutedEventArgs e) => VM.OpenConfigDirectory();
    private void Edit_Click(object? sender, RoutedEventArgs e) => VM.StartEdit();
    private void Save_Click(object? sender, RoutedEventArgs e) => VM.SaveConfig();
    private void CancelEdit_Click(object? sender, RoutedEventArgs e) => VM.CancelEdit();
    private void OpenNotepad_Click(object? sender, RoutedEventArgs e) { if (VM.SelectedConfig is not null) VM.OpenInEditor(VM.SelectedConfig); }
    private void Delete_Click(object? sender, RoutedEventArgs e) { if (VM.SelectedConfig is not null) VM.DeleteConfig(VM.SelectedConfig); }
}