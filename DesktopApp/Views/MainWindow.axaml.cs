using Avalonia.Controls;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnNavClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && DataContext is MainWindowViewModel vm)
        {
            vm.NavigateTo(tag);
            UpdateActiveNav(tag);
        }
    }

    private void UpdateActiveNav(string activeTag)
    {
        string[] navNames = ["navBuild", "navConfig", "navProject", "navCertificate", "navDoctor", "navArtifacts", "navStorage", "navEmail", "navHelp"];
        foreach (string name in navNames)
        {
            Control? ctrl = this.FindControl<Button>(name);
            if (ctrl is Button btn)
            {
                bool isActive = btn.Tag as string == activeTag;
                if (isActive) btn.Classes.Add("active");
                else btn.Classes.Remove("active");
            }
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.NavigateTo("config");
            UpdateActiveNav("config");
        }
    }
}
