using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Forms;
using Application = System.Windows.Application;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Orientation = System.Windows.Controls.Orientation;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using MessageBox = System.Windows.MessageBox;

namespace Jotter;

public partial class App : Application
{
    private Mutex? _mutex;
    private MainWindow? _main;
    private NotifyIcon? _tray;
    private ViewerWindow? _viewer;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, @"Local\Jotter.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Jotter 已在后台运行，看右下角托盘图标即可。", "Jotter",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        base.OnStartup(e);

        // 未捕获异常落盘，别无声死掉（极简：一行一异常，覆盖写）
        DispatcherUnhandledException += (s, e) =>
        {
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "_crash.log"),
                    e.Exception.ToString());
            }
            catch { /* 日志写不进去就算了 */ }
            e.Handled = true; // 常驻托盘的应用崩掉比出错更糟
        };

        _main = new MainWindow();
        new WindowInteropHelper(_main).EnsureHandle(); // 后台常驻：不显示窗口但热键立即生效
        CreateTray();
    }

    private void CreateTray()
    {
        _tray = new NotifyIcon
        {
            Icon = MakeIcon(),
            Text = "Jotter 速记（Ctrl+Alt+N）",
            Visible = true,
        };
        _tray.DoubleClick += (s, e) => _main?.ShowPopup();
        _tray.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Right) ShowTrayMenu();
        };
    }

    /// <summary>托盘右键：每次弹出都重建菜单（自启动状态、事件都新鲜），样式走 WPF 深色主题。</summary>
    private void ShowTrayMenu()
    {
        var menu = BuildTrayMenuWpf();
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private ContextMenu BuildTrayMenuWpf()
    {
        var menu = new ContextMenu { Style = (Style)FindResource("DarkMenu") };

        var open = new MenuItem { Header = "记一条" };
        open.Click += (s, e) => _main?.ShowPopup();
        var view = new MenuItem { Header = "查看笔记" };
        view.Click += (s, e) => OpenViewer();
        var folder = new MenuItem { Header = "打开笔记文件夹" };
        folder.Click += (s, e) => OpenNotesFolder();

        // 自启动用圆点指示，避免引回 WinForms 的勾选列
        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 6,
            Height = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        var autoPanel = new StackPanel { Orientation = Orientation.Horizontal };
        autoPanel.Children.Add(new TextBlock { Text = "开机自启动" });
        autoPanel.Children.Add(dot);
        var auto = new MenuItem { Header = autoPanel };
        if (AutoStart.IsEnabled())
            dot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 201, 201, 201)); // #C9C9C9 实心
        else
            dot.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 85, 85, 85));  // #555555 空心
        auto.Click += (s, e) => AutoStart.Set(!AutoStart.IsEnabled());

        var quit = new MenuItem { Header = "退出" };
        quit.Click += (s, e) =>
        {
            _main?.RealClose();
            Shutdown();
        };

        menu.Items.Add(open);
        menu.Items.Add(view);
        menu.Items.Add(folder);
        menu.Items.Add(auto);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);
        return menu;
    }

    /// <summary>
    /// 软件内查看笔记：单例窗口，已开着就刷新并激活，不重复开；
    /// 主弹窗如果正在显示先收起（保草稿），免得 Topmost 压住查看窗。
    /// （托盘菜单和主弹窗顶栏共用此入口）
    /// </summary>
    public void OpenViewer()
    {
        if (_main?.IsVisible == true) _main.HidePopup(keepDraft: true);

        if (_viewer == null)
        {
            _viewer = new ViewerWindow(JotterConfig.LoadOrDefault());
            _viewer.Closed += (s, e) => _viewer = null;
            _viewer.Show();
        }
        else
        {
            _viewer.Reload();
            if (!_viewer.IsVisible) _viewer.Show();
            _viewer.Activate();
        }
    }

    /// <summary>explorer 重启后（TaskbarCreated 广播）重建托盘图标，不然图标永久消失。</summary>
    public void RestoreTray()
    {
        if (_tray == null) return;
        _tray.Visible = false;
        _tray.Icon = MakeIcon();
        _tray.Visible = true;
    }

    private void OpenNotesFolder()
    {
        // LoadOrDefault 而非 new：用户在 settings.json 自定义过 NotesFolder 时要打开对的地方
        var folder = JotterConfig.LoadOrDefault().EffectiveNotesFolder;
        System.IO.Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true,
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    internal static Icon MakeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var bg = new SolidBrush(Color.FromArgb(46, 94, 254)))
                g.FillRectangle(bg, 3, 2, 26, 28);
            using var pen = new Pen(Color.White, 2.2f);
            g.DrawLine(pen, 9, 9, 23, 9);
            g.DrawLine(pen, 9, 16, 23, 16);
            g.DrawLine(pen, 9, 23, 17, 23);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
