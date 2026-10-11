using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace Jotter;

public partial class ViewerWindow : Window
{
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private sealed record NoteDoc(string Path, string Name);

    private static readonly SolidColorBrush StampBrush = MakeBrush("#FFC9C9C9");
    private static readonly SolidColorBrush BodyBrush = MakeBrush("#FFD6D6D6");
    private static readonly SolidColorBrush HeaderBrush = MakeBrush("#FF8A8A8A");
    private static readonly SolidColorBrush ErrorBrush = MakeBrush("#FFC97A7A");

    private static SolidColorBrush MakeBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private readonly JotterConfig _config;

    public ViewerWindow(JotterConfig config)
    {
        InitializeComponent();
        _config = config;
        using var icon = App.MakeIcon();
        Icon = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

        SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var pref = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        };

        // 同主弹窗：拖到屏幕顶触发 Aero Snap 最大化对无边框窗是破相的，拦回来
        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
        };

        Loaded += (s, e) => Reload();
    }

    /// <summary>每次打开都重扫重渲染——笔记可能刚写完。</summary>
    public void Reload()
    {
        var files = NoteStore.ListNoteFiles(_config.EffectiveNotesFolder);
        var hasNotes = files.Count > 0;
        FileList.Visibility = hasNotes ? Visibility.Visible : Visibility.Collapsed;
        ListSplitter.Visibility = hasNotes ? Visibility.Visible : Visibility.Collapsed;
        DocView.Visibility = hasNotes ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = hasNotes ? Visibility.Collapsed : Visibility.Visible;
        if (!hasNotes) return;

        FileList.ItemsSource = files.Select(f => new NoteDoc(f, DisplayName(f))).ToList();
        FileList.SelectedIndex = 0; // 默认总文档，它是全量汇总
        Render(files[0]); // 选中项没变时 SelectionChanged 不触发，这里兜一次保证新鲜
    }

    private static string DisplayName(string path)
    {
        var name = Path.GetFileName(path);
        return name == "Jotter.md" ? "总文档" : Path.GetFileNameWithoutExtension(path);
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is NoteDoc doc) Render(doc.Path);
    }

    /// <summary>
    /// 轻量逐行渲染：# 行是周标题，**...** 整行是条目时间戳，其余为正文；
    /// 其他 Markdown 语法原样保留，文本可选中复制。
    /// </summary>
    private void Render(string path)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(22, 16, 22, 22) };
        try
        {
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.TrimEnd();
                if (line.Length == 0)
                {
                    // 空行留个紧凑间隔（条目间已有时间戳的上边距，别叠太开）
                    doc.Blocks.Add(new Paragraph { FontSize = 5, Margin = new Thickness(0) });
                }
                else if (line.StartsWith("# "))
                {
                    doc.Blocks.Add(new Paragraph(new Run(line[2..]))
                    {
                        FontSize = 15,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = HeaderBrush,
                        Margin = new Thickness(0, 6, 0, 4),
                    });
                }
                else if (line.StartsWith("**") && line.EndsWith("**") && line.Length > 4)
                {
                    doc.Blocks.Add(new Paragraph(new Run(line[2..^2]))
                    {
                        FontSize = 12.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = StampBrush,
                        Margin = new Thickness(0, 12, 0, 2),
                    });
                }
                else
                {
                    doc.Blocks.Add(new Paragraph(new Run(line))
                    {
                        FontSize = 12.5,
                        Foreground = BodyBrush,
                        LineHeight = 22,
                        Margin = new Thickness(0),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            doc.Blocks.Add(new Paragraph(new Run("读取失败：" + ex.Message))
            {
                Foreground = ErrorBrush,
                Margin = new Thickness(0, 12, 0, 0),
            });
        }
        // 每次都是新 FlowDocument 赋值，滚动位置自然归顶
        DocView.Document = doc;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
