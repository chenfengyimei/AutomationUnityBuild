using Avalonia.Controls;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class EmailSettingsPage : UserControl
{
    public EmailSettingsPage()
    {
        InitializeComponent();
        DataContext = new EmailSettingsPageViewModel();
    }

    private EmailSettingsPageViewModel VM => (EmailSettingsPageViewModel)DataContext!;

    private void Save_Click(object? sender, RoutedEventArgs e) => VM.SaveSettings();

    private async void TestEmail_Click(object? sender, RoutedEventArgs e) => await VM.SendTestEmailAsync();

    private void AddContact_Click(object? sender, RoutedEventArgs e)
    {
        var title = this.FindControl<TextBox>("contactTitle")?.Text ?? "";
        var email = this.FindControl<TextBox>("contactEmail")?.Text ?? "";
        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(email))
        {
            VM.AddContact(title, email);
            if (this.FindControl<TextBox>("contactTitle") is { } tb) tb.Text = "";
            if (this.FindControl<TextBox>("contactEmail") is { } eb) eb.Text = "";
        }
    }

    private void RemoveContact_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NotificationContact contact)
        {
            VM.RemoveContact(contact);
        }
    }
}