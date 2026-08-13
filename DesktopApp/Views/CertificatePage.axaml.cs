using Avalonia.Controls;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class CertificatePage : UserControl
{
    public CertificatePage()
    {
        InitializeComponent();
        DataContext = new CertificatePageViewModel();
    }

    private CertificatePageViewModel VM => (CertificatePageViewModel)DataContext!;

    private void Refresh_Click(object? sender, RoutedEventArgs e) => VM.Refresh();
    private void Create_Click(object? sender, RoutedEventArgs e) => VM.StartNew();
    private void Edit_Click(object? sender, RoutedEventArgs e) => VM.StartEdit();
    private void Save_Click(object? sender, RoutedEventArgs e) => VM.Save();
    private void Cancel_Click(object? sender, RoutedEventArgs e) => VM.CancelEdit();
    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (VM.SelectedProfile is not null) VM.Delete(VM.SelectedProfile);
    }
}
