using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Forms;
using Application = System.Windows.Application;
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
            ContextMenuStrip = BuildTrayMenu(),
        };
        _tray.DoubleClick += (s, e) => _main?.ShowPopup();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        // 原生白底菜单和整体灰阶 UI 严重打架，整套换深色
        menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
        menu.Font = new Font("Segoe UI", 9.5f);
        menu.ForeColor = Color.FromArgb(214, 214, 214);
        // 注意别关 ShowImageMargin——"开机自启动"的勾选标记画在 margin 区，关了就看不见了

        menu.Items.Add("记一条", null, (s, e) => _main?.ShowPopup());
        menu.Items.Add("查看笔记", null, (s, e) => OpenViewer());
        menu.Items.Add("打开笔记文件夹", null, (s, e) => OpenNotesFolder());

        var auto = new ToolStripMenuItem("开机自启动") { CheckOnClick = true, Checked = AutoStart.IsEnabled() };
        auto.CheckedChanged += (s, e) => AutoStart.Set(auto.Checked);
        menu.Items.Add(auto);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (s, e) =>
        {
            _main?.RealClose();
            Shutdown();
        });
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

    /// <summary>托盘菜单的深色配色：与主 UI 同一套灰阶。</summary>
    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        private static Color Bg => Color.FromArgb(15, 15, 15);      // 0F0F0F
        private static Color Hover => Color.FromArgb(38, 38, 38);   // 262626
        private static Color Border => Color.FromArgb(46, 46, 46);  // 2E2E2E
        private static Color Sep => Color.FromArgb(29, 29, 29);     // 1D1D1D

        public override Color ToolStripDropDownBackground => Bg;
        public override Color ImageMarginGradientBegin => Bg;
        public override Color ImageMarginGradientMiddle => Bg;
        public override Color ImageMarginGradientEnd => Bg;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Hover;
        public override Color MenuItemSelected => Hover;
        public override Color MenuItemSelectedGradientBegin => Hover;
        public override Color MenuItemSelectedGradientEnd => Hover;
        public override Color MenuItemPressedGradientBegin => Hover;
        public override Color MenuItemPressedGradientEnd => Hover;
        public override Color CheckBackground => Hover;
        public override Color CheckSelectedBackground => Hover;
        public override Color CheckPressedBackground => Hover;
        public override Color SeparatorDark => Sep;
        public override Color SeparatorLight => Sep;
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
