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

        menu.Items.Add("记一条", null, (s, e) => _main?.ShowPopup());
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

    private void OpenNotesFolder()
    {
        var folder = new JotterConfig().EffectiveNotesFolder;
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

    private static Icon MakeIcon()
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
