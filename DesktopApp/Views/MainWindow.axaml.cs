using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DesktopApp.ViewModels;

namespace DesktopApp.Views;

public partial class MainWindow : Window
{
    private string? _lastTextInput;
    private DateTime _lastTextInputTime;
    private static readonly TimeSpan s_duplicationWindow = TimeSpan.FromMilliseconds(500);

    public MainWindow()
    {
        InitializeComponent();
        // 全局拦截 IME 中文输入重复末尾字符 (Avalonia Issue #20036)
        AddHandler(InputElement.TextInputEvent, OnGlobalTextInput, RoutingStrategies.Tunnel);
    }

    private void OnGlobalTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;

        var now = DateTime.Now;

        // IME 提交多字符后，框架错误地再发送一个末尾单字符
        if (e.Text.Length == 1
            && !string.IsNullOrEmpty(_lastTextInput)
            && _lastTextInput.Length > 1
            && now - _lastTextInputTime < s_duplicationWindow
            && _lastTextInput[^1] == e.Text[0])
        {
            e.Handled = true;
            _lastTextInput = null;
            return;
        }

        _lastTextInput = e.Text;
        _lastTextInputTime = now;
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
        string[] navNames = ["navBuild", "navConfig", "navProject", "navUnityProfile", "navSigningProfile", "navCertificate", "navSync", "navDoctor", "navArtifacts", "navStorage", "navData", "navEmail", "navHelp", "navBuildServer"];
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
