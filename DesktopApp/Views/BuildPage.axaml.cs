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
    private async void StartBuild_Click(object? sender, RoutedEventArgs e)
    {
        await VM.StartBuildAsync();
        AutoScrollLog();
    }
    private void CancelBuild_Click(object? sender, RoutedEventArgs e) => VM.CancelBuild();
    private void OpenArtifacts_Click(object? sender, RoutedEventArgs e) => VM.OpenArtifacts();
    private void ClearLog_Click(object? sender, RoutedEventArgs e) => VM.ClearLog();

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.Name == nameof(DataContext) && DataContext is BuildPageViewModel vm)
        {
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(BuildPageViewModel.LogText))
                    AutoScrollLog();
            };
        }
    }

    private void AutoScrollLog()
    {
        if (this.FindControl<TextBox>("logTextBox") is { } tb)
        {
            tb.SelectionStart = tb.Text?.Length ?? 0;
            tb.SelectionEnd = tb.SelectionStart;
            tb.CaretIndex = tb.SelectionStart;
        }
    }
}
