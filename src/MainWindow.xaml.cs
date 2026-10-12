using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Jotter;

public partial class MainWindow : Window
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const uint SWP_NOZORDER = 0x4;
    private const uint SWP_NOACTIVATE = 0x10;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern uint RegisterWindowMessage(string lpString);

    // explorer 重启时系统广播这个消息，托盘图标必须重建，否则永久消失
    private static readonly uint WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int L, T, R, B; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("dwmapi.dll")]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private readonly JotterConfig _config;
    private IntPtr _hwnd;
    private int _hotkeyId = 0x4000;
    private IntPtr _prevForeground;
    private bool _reallyClose;

    public MainWindow()
    {
        InitializeComponent();
        _config = JotterConfig.LoadOrDefault();
        Width = Math.Clamp(_config.WindowWidth, 280, 1600);
        Height = Math.Clamp(_config.WindowHeight, 160, 1200);

        SourceInitialized += (s, e) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
            var pref = DWMWCP_ROUND;
            DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            RegisterHotkey();
        };
        Closed += (s, e) => UnregisterHotKey(_hwnd, _hotkeyId);

        var clock = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15),
        };
        clock.Tick += (s, e) => UpdateTimestamp();
        clock.Start();
        UpdateTimestamp();

        // 拖标题栏到屏幕顶会触发 Aero Snap 最大化，对这种无边框小窗是破相的，拦回来
        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                PositionBottomRight();
            }
        };
    }

    private void UpdateTimestamp()
    {
        TimestampText.Text = DateTime.Now.ToString("yyyy-MM-dd ddd HH:mm", new System.Globalization.CultureInfo("zh-CN"));
    }

    private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        CountNum.Text = InputBox.Text.Length.ToString();
        CounterText.Opacity = InputBox.Text.Length == 0 ? 0.35 : 1.0;
    }

    private void RegisterHotkey()
    {
        var parsed = HotkeyParser.Parse(_config.Hotkey) ?? HotkeyParser.Parse("Ctrl+Alt+N");
        var (mods, vk) = parsed!.Value;
        if (!RegisterHotKey(_hwnd, _hotkeyId, mods, vk))
        {
            // 延迟到启动完成后提示：现在就弹会阻塞构造流程，托盘图标就创建不出来了
            Dispatcher.BeginInvoke(() => MessageBox.Show(
                $"热键 {_config.Hotkey} 注册失败，可能被其他程序占用。\n" +
                "可修改程序目录下 settings.json 里的 Hotkey 后重新启动。",
                "Jotter", MessageBoxButton.OK, MessageBoxImage.Warning),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
        {
            if (IsVisible) HidePopup(keepDraft: true);
            else ShowPopup();
            handled = true;
        }
        else if (WM_TASKBARCREATED != 0 && msg == (int)WM_TASKBARCREATED)
        {
            (Application.Current as App)?.RestoreTray();
        }
        return IntPtr.Zero;
    }

    public void ShowPopup()
    {
        var fg = GetForegroundWindow();
        if (fg != _hwnd) _prevForeground = fg;
        // 不留记忆：每次唤出都回到默认尺寸，位置由 PositionBottomRight 统一归到右下角
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        Width = Math.Clamp(_config.WindowWidth, 280, 1600);
        Height = Math.Clamp(_config.WindowHeight, 160, 1200);
        UpdateTimestamp(); // 弹出瞬间时间戳必须新鲜，不等下一次计时
        Show();
        Activate();
        PositionBottomRight(); // Show 之后窗口 DPI 才确定，按物理像素定位
        InputBox.Focus();
        InputBox.CaretIndex = InputBox.Text.Length;
    }

    /// <summary>
    /// 全程用物理像素计算（GetMonitorInfo/GetDpiForWindow/SetWindowPos），
    /// 不经过 WPF 的 SystemParameters/Left/Top——它们随窗口 DPI 上下文变化，不可靠。
    /// </summary>
    private void PositionBottomRight()
    {
        if (_hwnd == IntPtr.Zero) return;
        var mon = MonitorFromWindow(_hwnd, MONITOR_DEFAULTTONEAREST);
        var mi = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(mon, ref mi)) return;

        uint dpi = GetDpiForWindow(_hwnd);
        if (dpi == 0) dpi = 96;
        double scale = dpi / 96.0;

        int wPhys = (int)Math.Round(Width * scale);
        int hPhys = (int)Math.Round(Height * scale);
        int marginPhys = (int)Math.Round(14 * scale);

        SetWindowPos(_hwnd, IntPtr.Zero,
            mi.rcWork.R - wPhys - marginPhys,
            mi.rcWork.B - hPhys - marginPhys,
            wPhys, hPhys,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (IsVisible) PositionBottomRight(); // DPI 上下文切换后按新比例重新归位
    }

    // 托盘"查看笔记"和顶栏入口都要用：收起弹窗但草稿留着
    public void HidePopup(bool keepDraft)
    {
        if (!keepDraft) InputBox.Clear();
        Hide();
        if (_prevForeground != IntPtr.Zero && _prevForeground != _hwnd)
            SetForegroundWindow(_prevForeground);
    }

    private void Send()
    {
        var text = InputBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            HidePopup(keepDraft: false);
            return;
        }
        try
        {
            NoteStore.Append(_config, text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "写入失败，内容已保留在输入框：\n" + ex.Message,
                "Jotter", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        HidePopup(keepDraft: false);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 输入法处理过的按键要取 ImeProcessedKey，否则候选框激活时收不到真正的 Enter
        var key = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
        if (key == Key.Enter)
        {
            var mod = Keyboard.Modifiers;
            if ((mod & (ModifierKeys.Shift | ModifierKeys.Control)) != ModifierKeys.None)
            {
                e.Handled = true;
                Send();
            }
        }
        else if (key == Key.Escape)
        {
            e.Handled = true;
            HidePopup(keepDraft: false);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    // 顶栏"查看"入口：e.Handled 防止按下被顶栏当拖拽吞掉
    private void ViewNotes_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (Application.Current is App app) app.OpenViewer();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    public void RealClose()
    {
        _reallyClose = true;
        Close();
    }
}
