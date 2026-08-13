using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class DataPage : UserControl
{
    public DataPage()
    {
        InitializeComponent();
        DataContext = new DataPageViewModel();
    }

    private DataPageViewModel VM => (DataPageViewModel)DataContext!;

    private void SelectAll_Click(object? sender, RoutedEventArgs e) => VM.SelectAll();
    private void DeselectAll_Click(object? sender, RoutedEventArgs e) => VM.DeselectAll();

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出数据",
            DefaultExtension = "json",
            SuggestedFileName = $"desktopapp-export-{DateTime.Now:yyyyMMdd-HHmmss}.json"
        });

        if (file is null) return;
        await VM.ExportAsync(file.Path.LocalPath);
    }

    private async void Import_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择导入文件",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("JSON 文件") { Patterns = new List<string> { "*.json" } }
            }
        });

        if (files is null || files.Count == 0) return;
        await VM.ImportAsync(files[0].Path.LocalPath);
    }
}
