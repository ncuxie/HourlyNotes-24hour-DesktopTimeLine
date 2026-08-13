using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace HourlyNotes;

public partial class MainWindow : Window
{
    private const double RowHeight = 48;

    // 数据保存到用户数据目录（%LOCALAPPDATA%\HourlyNotes\schedule.json）
    // —— 这样在 MSIX 打包 / 商店上架后依然可写（安装目录是只读的）
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HourlyNotes");
    private static readonly string FullSavePath = Path.Combine(DataDir, "schedule.json");

    private readonly ObservableCollection<HourEntry> _entries = new();
    private ScrollViewer? _scroll;
    private double _wheelAccum;
    private DispatcherTimer? _timer;

    private HourEntry? _editingEntry;
    private TextBox? _editingBox;
    private TextBlock? _editingDisplay;
    private bool _cancelEdit;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += Window_Loaded;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadData();
        _scroll = FindVisualChild<ScrollViewer>(HourList);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => RefreshCurrentFlag();
        _timer.Start();
        RefreshCurrentFlag();

        // 启动时定位到当前小时
        Dispatcher.BeginInvoke(new Action(() => ScrollToHour(DateTime.Now.Hour)));
    }

    // ---------- 数据 ----------
    private void LoadData()
    {
        for (int i = 0; i < 24; i++) _entries.Add(new HourEntry { Hour = i });

        try
        {
            if (File.Exists(FullSavePath))
            {
                var list = JsonSerializer.Deserialize<List<HourEntry>>(File.ReadAllText(FullSavePath));
                if (list != null)
                    foreach (var item in list)
                        if (item.Hour is >= 0 and < 24)
                            _entries[item.Hour].Text = item.Text ?? "";
            }
        }
        catch { /* 文件损坏时忽略，从空白开始 */ }

        HourList.ItemsSource = _entries;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            File.WriteAllText(FullSavePath,
                JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    // ---------- 滚动（吸附，每格 1 小时） ----------
    private void HourList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_scroll == null) return;
        e.Handled = true;
        _wheelAccum += e.Delta;
        const double step = 120;
        while (_wheelAccum >= step) { _wheelAccum -= step; ScrollBy(-1); }
        while (_wheelAccum <= -step) { _wheelAccum += step; ScrollBy(1); }
    }

    private void ScrollBy(int rows)
    {
        if (_scroll == null) return;
        double target = _scroll.VerticalOffset + rows * RowHeight;
        target = Math.Clamp(target, 0, _scroll.ScrollableHeight);
        _scroll.ScrollToVerticalOffset(target);
    }

    private void ScrollToHour(int hour)
    {
        if (_scroll == null) return;
        _scroll.ScrollToVerticalOffset(Math.Min(hour * RowHeight, _scroll.ScrollableHeight));
    }

    // ---------- 编辑 ----------
    private void Cell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2) return;
        if (sender is FrameworkElement fe && fe.DataContext is HourEntry entry)
        {
            if (_editingEntry == entry && _editingBox?.IsVisible == true) return;
            OpenEditor(entry, fe);
            e.Handled = true;
        }
    }

    private void OpenEditor(HourEntry entry, DependencyObject root)
    {
        var display = FindVisualChildByName<TextBlock>(root, "DisplayText");
        var edit = FindVisualChildByName<TextBox>(root, "EditBox");
        if (display == null || edit == null) return;

        _editingEntry = entry;
        _editingDisplay = display;
        _editingBox = edit;

        // edit.Text = "";                     // 双击后默认置空
        display.Visibility = Visibility.Collapsed;
        edit.Visibility = Visibility.Visible;
        edit.Focus();
        edit.CaretIndex = edit.Text.Length;   // 光标移到文本末尾
    }

    private void EditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)      { CommitEdit(); e.Handled = true; }
        else if (e.Key == Key.Escape) { _cancelEdit = true; CloseEditor(); e.Handled = true; }
    }

    private void EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_cancelEdit) { _cancelEdit = false; return; }
        CommitEdit();
    }

    private void CommitEdit()
    {
        if (_editingBox == null || _editingEntry == null) return;
        _editingEntry.Text = _editingBox.Text;   // INPC 自动刷新界面
        CloseEditor();
        Save();
    }

    private void CloseEditor()
    {
        if (_editingBox != null) _editingBox.Visibility = Visibility.Collapsed;
        if (_editingDisplay != null) _editingDisplay.Visibility = Visibility.Visible;
        _editingBox = null;
        _editingDisplay = null;
        _editingEntry = null;
    }

    // ---------- 顶栏 ----------
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1) DragMove();
    }

    private void BtnNow_Click(object sender, RoutedEventArgs e)
    {
        RefreshCurrentFlag();
        ScrollToHour(DateTime.Now.Hour);
    }

    private void BtnGear_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Opacity = e.NewValue;
        if (OpacityValue != null) OpacityValue.Text = $"{e.NewValue * 100:0}%";
    }

    // ---------- 当前小时高亮 ----------
    private void RefreshCurrentFlag()
    {
        int h = DateTime.Now.Hour;
        foreach (var entry in _entries) entry.IsCurrent = (entry.Hour == h);
    }

    // ---------- 视觉树工具 ----------
    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private static T? FindVisualChildByName<T>(DependencyObject parent, string name)
        where T : FrameworkElement
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == name) return t;
            var found = FindVisualChildByName<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
