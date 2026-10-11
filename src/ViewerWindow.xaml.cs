using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Clipboard = System.Windows.Clipboard;
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
    private sealed record EntryVM(string Stamp, string Body);

    private static readonly SolidColorBrush TitleBrush = MakeBrush("#FF8A8A8A");
    private static readonly SolidColorBrush ErrorBrush = MakeBrush("#FFC97A7A");

    private static SolidColorBrush MakeBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private readonly JotterConfig _config;
    private string? _currentFile;          // 正在查看的文件
    private string? _editStamp, _editBody; // 正在编辑的条目（原值，用于文件内定位）

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

    /// <summary>每次打开都重扫重渲染——笔记可能刚写完。尽量保住当前查看的文件。</summary>
    public void Reload()
    {
        var files = NoteStore.ListNoteFiles(_config.EffectiveNotesFolder);
        var hasNotes = files.Count > 0;
        FileList.Visibility = hasNotes ? Visibility.Visible : Visibility.Collapsed;
        ListSplitter.Visibility = hasNotes ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = hasNotes ? Visibility.Collapsed : Visibility.Visible;
        if (!hasNotes)
        {
            DocTitle.Visibility = Visibility.Collapsed;
            EntryList.ItemsSource = null;
            return;
        }

        var keep = _currentFile;
        FileList.ItemsSource = files.Select(f => new NoteDoc(f, DisplayName(f))).ToList();
        var idx = 0;
        for (int i = 0; i < files.Count; i++)
        {
            if (string.Equals(files[i], keep, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
        }
        FileList.SelectedIndex = idx;   // 与上次相同不会触发 SelectionChanged
        LoadFile(files[idx]);           // 所以这里兜一次，保证内容新鲜
    }

    private static string DisplayName(string path)
    {
        var name = Path.GetFileName(path);
        return name == "Jotter.md" ? "总文档" : Path.GetFileNameWithoutExtension(path);
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileList.SelectedItem is NoteDoc doc) LoadFile(doc.Path);
    }

    /// <summary>把文件解析成条目卡片流。</summary>
    private void LoadFile(string path)
    {
        _currentFile = path;
        try
        {
            var (title, entries) = NoteStore.ParseDocument(File.ReadAllLines(path));
            // 总文档内部混有各周 H1，解析到的 title 不可用；只有周文档标题在文件顶部才可信
            var isMaster = DisplayName(path) == "总文档";
            DocTitle.Text = isMaster ? "总文档" : (title ?? DisplayName(path));
            DocTitle.Visibility = Visibility.Visible;
            EntryList.ItemsSource = entries.Select(en => new EntryVM(en.Stamp, en.Body)).ToList();
        }
        catch (Exception ex)
        {
            DocTitle.Text = DisplayName(path);
            DocTitle.Visibility = Visibility.Visible;
            EntryList.ItemsSource = new List<EntryVM> { new("读取失败", ex.Message) };
        }
    }

    private EntryVM? SelectedEntry => EntryList.SelectedItem as EntryVM;

    private void CopyEntry_Click(object sender, RoutedEventArgs e)
    {
        var vm = SelectedEntry;
        if (vm == null) return;
        try { Clipboard.SetText(vm.Body.Length > 0 ? vm.Body : vm.Stamp); }
        catch { /* 剪贴板被占用时静默，不值得打扰 */ }
    }

    private void EditEntry_Click(object sender, RoutedEventArgs e)
    {
        var vm = SelectedEntry;
        if (vm == null) return;
        _editStamp = vm.Stamp;
        _editBody = vm.Body;
        EditStamp.Text = "正在修改 " + vm.Stamp;
        EditBox.Text = vm.Body;
        EditBar.Visibility = Visibility.Visible;
        EditBox.Focus();
        EditBox.CaretIndex = EditBox.Text.Length;
    }

    private void EditSave_Click(object sender, RoutedEventArgs e)
    {
        if (_editStamp == null) return;
        try
        {
            NoteStore.UpdateEntryEverywhere(_config.EffectiveNotesFolder, _editStamp, _editBody!, EditBox.Text);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存失败，内容还在编辑框里：\n" + ex.Message,
                "Jotter", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        CloseEditBar();
        Reload();
    }

    private void EditCancel_Click(object sender, RoutedEventArgs e) => CloseEditBar();

    private void CloseEditBar()
    {
        _editStamp = _editBody = null;
        EditBar.Visibility = Visibility.Collapsed;
        EditBox.Clear();
    }

    private void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        var vm = SelectedEntry;
        if (vm == null) return;
        var preview = vm.Body.Length > 42 ? vm.Body[..42] + "…" : vm.Body;
        var ok = ConfirmWindow.Ask(this,
            "删除这条速记？",
            vm.Stamp + (preview.Length > 0 ? "\n" + preview : "\n（无正文）"),
            "会同时从周文档和总文档移除，不可恢复。",
            "删除");
        if (!ok) return;

        try
        {
            NoteStore.DeleteEntryEverywhere(_config.EffectiveNotesFolder, vm.Stamp, vm.Body);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "删除失败：\n" + ex.Message,
                "Jotter", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        Reload();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        // 编辑中先取消编辑，再按一次才关窗
        if (EditBar.Visibility == Visibility.Visible) CloseEditBar();
        else Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
