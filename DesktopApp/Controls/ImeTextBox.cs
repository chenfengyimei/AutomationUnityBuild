using Avalonia.Controls;
using Avalonia.Input;

namespace DesktopApp.Controls;

/// <summary>
/// 修复 Avalonia Win32 IME 中文输入重复末尾字符的 bug (GitHub Issue #20036)。
/// 当 IME 提交多字符文本后，框架会错误地再发送一个单字符 TextInput 事件（末尾字符），
/// 此控件检测并抑制这个重复的输入。
/// </summary>
public class ImeTextBox : TextBox
{
    private string? _lastTextInput;
    private DateTime _lastTextInputTime;
    private static readonly TimeSpan s_duplicationWindow = TimeSpan.FromMilliseconds(500);

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text))
        {
            var now = DateTime.Now;

            // 检测 IME 重复：单字符输入紧跟在多字符输入之后，且字符匹配末尾
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

        base.OnTextInput(e);
    }
}
