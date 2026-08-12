using Avalonia.Controls;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class DoctorPage : UserControl
{
    public DoctorPage()
    {
        InitializeComponent();
        DataContext = new DoctorPageViewModel();
    }

    private DoctorPageViewModel VM => (DoctorPageViewModel)DataContext!;
    private void Refresh_Click(object? sender, RoutedEventArgs e) => VM.RefreshConfigs();
    private async void RunDoctor_Click(object? sender, RoutedEventArgs e) => await VM.RunDoctorAsync();
}