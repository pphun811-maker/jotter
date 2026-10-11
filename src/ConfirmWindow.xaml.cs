using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Jotter;

/// <summary>
/// 深色确认对话框，替代系统 MessageBox（无提示音、样式与主 UI 一致）。
/// 用法：ConfirmWindow.Ask(this, "标题", "详情", "提示", "确认按钮文字")
/// </summary>
public partial class ConfirmWindow : Window
{
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private ConfirmWindow()
    {
        InitializeComponent();
        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var pref = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        };
    }

    public static bool Ask(Window? owner, string title, string detail, string hint, string confirmLabel)
    {
        var w = new ConfirmWindow();
        w.TitleText.Text = title;
        w.DetailText.Text = detail;
        w.HintText.Text = hint;
        w.ConfirmBtn.Content = confirmLabel;
        if (owner != null) w.Owner = owner;
        w.ShowDialog();
        return w._confirmed;
    }

    private bool _confirmed;

    private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
    {
        _confirmed = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close(); // Esc = 取消（_confirmed 保持 false）
    }
}
