using Avalonia.Controls;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class SigningProfilePage : UserControl
{
    public SigningProfilePage() { InitializeComponent(); DataContext = new SigningProfilePageViewModel(); }
    private SigningProfilePageViewModel VM => (SigningProfilePageViewModel)DataContext!;
    private void Refresh_Click(object? s, RoutedEventArgs e) => VM.Refresh();
    private void Create_Click(object? s, RoutedEventArgs e) => VM.StartNew();
    private void Edit_Click(object? s, RoutedEventArgs e) => VM.StartEdit();
    private void Save_Click(object? s, RoutedEventArgs e) => VM.Save();
    private void Cancel_Click(object? s, RoutedEventArgs e) => VM.CancelEdit();
    private void Delete_Click(object? s, RoutedEventArgs e) { if (VM.SelectedProfile is not null) VM.Delete(VM.SelectedProfile); }
}
