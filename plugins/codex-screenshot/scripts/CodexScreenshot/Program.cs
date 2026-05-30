using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using DrawingRectangle = System.Drawing.Rectangle;
using IoPath = System.IO.Path;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfPanel = System.Windows.Controls.Panel;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace CodexScreenshot;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var existing = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName)
            .FirstOrDefault(p => p.Id != Environment.ProcessId);
        if (existing is not null)
        {
            return;
        }

        var app = new WpfApplication { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var settings = AppSettings.Load();
        using var tray = new TrayApp(app, settings);
        app.Run();
    }
}

internal sealed class TrayApp : IDisposable
{
    private readonly WpfApplication _app;
    private readonly AppSettings _settings;
    private readonly HotKeyWindow _hotKeyWindow;
    private readonly Forms.NotifyIcon _notifyIcon;

    public TrayApp(WpfApplication app, AppSettings settings)
    {
        _app = app;
        _settings = settings;
        _hotKeyWindow = new HotKeyWindow(settings, Capture, BindCurrentWindow);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("截图", null, (_, _) => Capture());
        menu.Items.Add("绑定当前 Codex 窗口", null, (_, _) => BindCurrentWindow());
        menu.Items.Add("打开截图目录", null, (_, _) => OpenCapturesFolder());
        menu.Items.Add("设置", null, (_, _) => OpenSettings());
        menu.Items.Add("退出", null, (_, _) => _app.Shutdown());

        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "Codex 截图标注",
            Icon = TrayIconFactory.Create(),
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => Capture();

        if (!_hotKeyWindow.Register())
        {
            _notifyIcon.ShowBalloonTip(4000, "Codex 截图标注", $"无法注册快捷键 {_settings.Hotkey}，请使用托盘菜单截图。", Forms.ToolTipIcon.Warning);
        }
    }

    private void Capture()
    {
        var sourceWindow = _settings.TargetWindowHandle != 0
            ? new IntPtr(_settings.TargetWindowHandle)
            : WindowPaster.GetForegroundWindowHandle();
        _app.Dispatcher.Invoke(() =>
        {
            using var fullScreen = ScreenCapture.CaptureVirtualScreen();
            var selection = new SelectionWindow(fullScreen.BitmapSource, fullScreen.Bounds);
            if (selection.ShowDialog() != true || selection.SelectedBounds is null)
            {
                return;
            }

            using var cropped = ScreenCapture.Crop(fullScreen.Bitmap, selection.SelectedBounds.Value);
            var editor = new AnnotationWindow(cropped);
            if (editor.ShowDialog() != true || editor.ResultImage is null)
            {
                return;
            }

            var result = editor.ResultImage;
            Directory.CreateDirectory(AppSettings.CapturesDirectory);
            var path = IoPath.Combine(AppSettings.CapturesDirectory, $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            SaveBitmapSource(result, path);
            ClipboardWriter.SetScreenshot(result, path);

            if (_settings.SaveCopies)
            {
                File.WriteAllText(IoPath.Combine(AppSettings.CapturesDirectory, "latest.txt"), path);
            }

            if (string.Equals(_settings.PasteMode, "autoPaste", StringComparison.OrdinalIgnoreCase))
            {
                if (WindowPaster.PasteIntoBestWindow(_settings.TargetWindowTitleContains, sourceWindow))
                {
                    _notifyIcon.ShowBalloonTip(1000, "Codex 截图标注", "已粘贴到 Codex。", Forms.ToolTipIcon.Info);
                }
                else
                {
                    _notifyIcon.ShowBalloonTip(4000, "Codex 截图标注", "已复制截图，但未找到 Codex 窗口，请手动按 Ctrl+V。", Forms.ToolTipIcon.Warning);
                }
            }
            else
            {
                _notifyIcon.ShowBalloonTip(2000, "Codex 截图标注", "截图已复制到剪贴板。", Forms.ToolTipIcon.Info);
            }
        });
    }

    private static void SaveBitmapSource(BitmapSource source, string path)
    {
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(stream);
    }

    private static void OpenCapturesFolder()
    {
        Directory.CreateDirectory(AppSettings.CapturesDirectory);
        Process.Start(new ProcessStartInfo(AppSettings.CapturesDirectory) { UseShellExecute = true });
    }

    private static void OpenSettings()
    {
        AppSettings.EnsureFile();
        Process.Start(new ProcessStartInfo("notepad.exe", AppSettings.SettingsPath) { UseShellExecute = true });
    }

    private void BindCurrentWindow()
    {
        var hWnd = WindowPaster.GetForegroundWindowHandle();
        if (hWnd == IntPtr.Zero)
        {
            _notifyIcon.ShowBalloonTip(3000, "Codex 截图标注", "没有找到前台窗口。", Forms.ToolTipIcon.Warning);
            return;
        }

        _settings.TargetWindowHandle = hWnd.ToInt64();
        _settings.TargetWindowTitle = WindowPaster.GetWindowTitleForDisplay(hWnd);
        _settings.Save();
        _notifyIcon.ShowBalloonTip(3000, "Codex 截图标注", $"已绑定目标窗口：{_settings.TargetWindowTitle}", Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _hotKeyWindow.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

internal static class CaptureWorkflow
{
    public static void Run(WpfApplication app, AppSettings settings, Action<string>? showInfo, Action<string>? showWarning)
    {
        var sourceWindow = settings.TargetWindowHandle != 0
            ? new IntPtr(settings.TargetWindowHandle)
            : WindowPaster.GetForegroundWindowHandle();

        app.Dispatcher.Invoke(() =>
        {
            using var fullScreen = ScreenCapture.CaptureVirtualScreen();
            var selection = new SelectionWindow(fullScreen.BitmapSource, fullScreen.Bounds);
            if (selection.ShowDialog() != true || selection.SelectedBounds is null)
            {
                return;
            }

            using var cropped = ScreenCapture.Crop(fullScreen.Bitmap, selection.SelectedBounds.Value);
            var editor = new AnnotationWindow(cropped);
            if (editor.ShowDialog() != true || editor.ResultImage is null)
            {
                return;
            }

            var result = editor.ResultImage;
            Directory.CreateDirectory(AppSettings.CapturesDirectory);
            var path = IoPath.Combine(AppSettings.CapturesDirectory, $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            SaveBitmapSource(result, path);
            ClipboardWriter.SetScreenshot(result, path);

            if (settings.SaveCopies)
            {
                File.WriteAllText(IoPath.Combine(AppSettings.CapturesDirectory, "latest.txt"), path);
            }

            if (string.Equals(settings.PasteMode, "autoPaste", StringComparison.OrdinalIgnoreCase))
            {
                if (WindowPaster.PasteIntoBestWindow(settings.TargetWindowTitleContains, sourceWindow))
                {
                    showInfo?.Invoke("已粘贴到 Codex。");
                }
                else
                {
                    showWarning?.Invoke("已复制截图，但未找到 Codex 窗口，请手动按 Ctrl+V。");
                }
            }
            else
            {
                showInfo?.Invoke("截图已复制到剪贴板。");
            }
        });
    }

    private static void SaveBitmapSource(BitmapSource source, string path)
    {
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(stream);
    }
}

internal sealed class AppSettings
{
    public string Hotkey { get; set; } = "Ctrl+Shift+S";
    public string PasteMode { get; set; } = "autoPaste";
    public string TargetWindowTitleContains { get; set; } = "Codex";
    public long TargetWindowHandle { get; set; }
    public string TargetWindowTitle { get; set; } = "";
    public bool SaveCopies { get; set; } = true;

    public static string DataDirectory { get; } = IoPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexScreenshot");
    public static string CapturesDirectory { get; } = IoPath.Combine(DataDirectory, "captures");
    public static string SettingsPath { get; } = IoPath.Combine(DataDirectory, "settings.json");

    public static AppSettings Load()
    {
        EnsureFile();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions()) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void EnsureFile()
    {
        Directory.CreateDirectory(DataDirectory);
        if (!File.Exists(SettingsPath))
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new AppSettings(), JsonOptions()));
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions()));
    }

    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}

internal static class TrayIconFactory
{
    public static Icon Create()
    {
        var bitmap = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(System.Drawing.Color.Transparent);

        using var background = new SolidBrush(System.Drawing.Color.FromArgb(37, 99, 235));
        graphics.FillEllipse(background, 1, 1, 30, 30);

        using var whitePen = new System.Drawing.Pen(System.Drawing.Color.White, 2.2f);
        using var faintPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(180, 219, 234, 254), 1.2f);
        graphics.DrawRectangle(whitePen, 8, 9, 16, 13);
        graphics.DrawLine(faintPen, 16, 5, 16, 12);
        graphics.DrawLine(faintPen, 16, 19, 16, 27);
        graphics.DrawLine(faintPen, 5, 16, 12, 16);
        graphics.DrawLine(faintPen, 20, 16, 27, 16);

        using var dotBrush = new SolidBrush(System.Drawing.Color.FromArgb(96, 165, 250));
        graphics.FillEllipse(dotBrush, 13, 13, 6, 6);

        var handle = bitmap.GetHicon();
        return (Icon)Icon.FromHandle(handle).Clone();
    }
}

internal static class ClipboardWriter
{
    public static void SetScreenshot(BitmapSource image, string pngPath)
    {
        var data = new System.Windows.DataObject();
        data.SetImage(image);
        data.SetData(System.Windows.DataFormats.Bitmap, image);
        data.SetData("PNG", BitmapSourceToPngStream(image));
        data.SetFileDropList(new StringCollection { pngPath });
        System.Windows.Clipboard.SetDataObject(data, copy: true);
    }

    private static MemoryStream BitmapSourceToPngStream(BitmapSource source)
    {
        var stream = new MemoryStream();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(stream);
        stream.Position = 0;
        return stream;
    }
}

internal sealed class HotKeyWindow : Forms.NativeWindow, IDisposable
{
    private const int WmHotKey = 0x0312;
    private readonly AppSettings _settings;
    private readonly Action _onHotKey;
    private readonly Action _onBind;
    private int _captureId;
    private int _bindId;

    public HotKeyWindow(AppSettings settings, Action onHotKey, Action onBind)
    {
        _settings = settings;
        _onHotKey = onHotKey;
        _onBind = onBind;
        CreateHandle(new Forms.CreateParams());
    }

    public bool Register()
    {
        var capture = HotKey.Parse(_settings.Hotkey);
        var bind = HotKey.Parse("Ctrl+Shift+D");
        _captureId = GetHashCode();
        _bindId = _captureId + 1;
        var captureRegistered = RegisterHotKey(Handle, _captureId, capture.Modifiers, capture.Key);
        var bindRegistered = RegisterHotKey(Handle, _bindId, bind.Modifiers, bind.Key);
        return captureRegistered && bindRegistered;
    }

    protected override void WndProc(ref Forms.Message m)
    {
        if (m.Msg == WmHotKey)
        {
            if (m.WParam.ToInt32() == _bindId)
            {
                _onBind();
            }
            else
            {
                _onHotKey();
            }
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_captureId != 0)
        {
            UnregisterHotKey(Handle, _captureId);
        }
        if (_bindId != 0)
        {
            UnregisterHotKey(Handle, _bindId);
        }
        DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

internal readonly record struct HotKey(uint Modifiers, uint Key)
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    public static HotKey Parse(string value)
    {
        uint modifiers = 0;
        uint key = 0;
        foreach (var raw in value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var part = raw.ToUpperInvariant();
            modifiers |= part switch
            {
                "CTRL" or "CONTROL" => ModControl,
                "SHIFT" => ModShift,
                "ALT" => ModAlt,
                "WIN" or "WINDOWS" => ModWin,
                _ => 0
            };
            if (part is not ("CTRL" or "CONTROL" or "SHIFT" or "ALT" or "WIN" or "WINDOWS"))
            {
                key = part.Length == 1 ? part[0] : (uint)Enum.Parse<Forms.Keys>(part, ignoreCase: true);
            }
        }
        return key == 0 ? new HotKey(ModControl | ModShift, (uint)Forms.Keys.S) : new HotKey(modifiers, key);
    }
}

internal sealed class ScreenCaptureResult : IDisposable
{
    public required Bitmap Bitmap { get; init; }
    public required BitmapSource BitmapSource { get; init; }
    public required Int32Rect Bounds { get; init; }

    public void Dispose() => Bitmap.Dispose();
}

internal static class ScreenCapture
{
    public static ScreenCaptureResult CaptureVirtualScreen()
    {
        var bounds = Forms.SystemInformation.VirtualScreen;
        var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        return new ScreenCaptureResult
        {
            Bitmap = bitmap,
            BitmapSource = ToBitmapSource(bitmap),
            Bounds = new Int32Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height)
        };
    }

    public static Bitmap Crop(Bitmap source, Int32Rect absoluteSelection)
    {
        var virtualScreen = Forms.SystemInformation.VirtualScreen;
        var local = new DrawingRectangle(
            absoluteSelection.X - virtualScreen.Left,
            absoluteSelection.Y - virtualScreen.Top,
            absoluteSelection.Width,
            absoluteSelection.Height);
        return source.Clone(local, source.PixelFormat);
    }

    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

internal sealed class SelectionWindow : Window
{
    private readonly Int32Rect _virtualBounds;
    private readonly Canvas _canvas = new();
    private readonly System.Windows.Shapes.Rectangle _selection = new()
    {
        Stroke = System.Windows.Media.Brushes.DeepSkyBlue,
        StrokeThickness = 2,
        Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 14, 165, 233))
    };
    private Point? _start;

    public Int32Rect? SelectedBounds { get; private set; }

    public SelectionWindow(BitmapSource screenshot, Int32Rect virtualBounds)
    {
        _virtualBounds = virtualBounds;
        Left = virtualBounds.X;
        Top = virtualBounds.Y;
        Width = virtualBounds.Width;
        Height = virtualBounds.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        Cursor = System.Windows.Input.Cursors.Cross;
        Background = new ImageBrush(screenshot) { Stretch = Stretch.Fill, Opacity = 0.72 };
        Content = _canvas;
        _canvas.Children.Add(_selection);
        _selection.Visibility = Visibility.Collapsed;

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Loaded += (_, _) => Activate();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(_canvas);
        _selection.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelection(_start.Value, _start.Value);
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_start is Point start)
        {
            UpdateSelection(start, e.GetPosition(_canvas));
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_start is not Point start)
        {
            return;
        }

        ReleaseMouseCapture();
        var end = e.GetPosition(_canvas);
        var x = (int)Math.Round(Math.Min(start.X, end.X));
        var y = (int)Math.Round(Math.Min(start.Y, end.Y));
        var width = (int)Math.Round(Math.Abs(start.X - end.X));
        var height = (int)Math.Round(Math.Abs(start.Y - end.Y));
        if (width > 4 && height > 4)
        {
            SelectedBounds = new Int32Rect(_virtualBounds.X + x, _virtualBounds.Y + y, width, height);
            DialogResult = true;
        }
        Close();
    }

    private void UpdateSelection(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        Canvas.SetLeft(_selection, x);
        Canvas.SetTop(_selection, y);
        _selection.Width = Math.Abs(a.X - b.X);
        _selection.Height = Math.Abs(a.Y - b.Y);
    }
}

internal enum ToolKind
{
    Rectangle,
    Arrow,
    Pen,
    Text
}

internal sealed class AnnotationWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly Stack<UIElement> _annotations = new();
    private ToolKind _tool = ToolKind.Rectangle;
    private Point? _start;
    private UIElement? _activeShape;
    private System.Windows.Shapes.Polyline? _activeLine;

    public BitmapSource? ResultImage { get; private set; }

    public AnnotationWindow(Bitmap bitmap)
    {
        var source = ScreenCapture.ToBitmapSource(bitmap);
        Title = "Codex 截图标注";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Width = Math.Min(source.PixelWidth + 56, SystemParameters.WorkArea.Width * 0.92);
        Height = Math.Min(source.PixelHeight + 72, SystemParameters.WorkArea.Height * 0.92);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        KeyDown += OnEditorKeyDown;

        _canvas.Width = source.PixelWidth;
        _canvas.Height = source.PixelHeight;
        _canvas.Background = new ImageBrush(source) { Stretch = Stretch.Fill };
        _canvas.MouseDown += OnCanvasMouseDown;
        _canvas.MouseMove += OnCanvasMouseMove;
        _canvas.MouseUp += OnCanvasMouseUp;

        var toolbar = CreateToolbar();

        var imageFrame = new Border
        {
            Child = _canvas,
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(9, 14, 24)),
            SnapsToDevicePixels = true
        };

        var scroll = new ScrollViewer
        {
            Content = imageFrame,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 12, 18, 28)),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(14)
        };
        var root = new Grid();
        root.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0));
        root.Children.Add(scroll);
        root.Children.Add(toolbar);
        Content = root;
    }

    private Border CreateToolbar()
    {
        var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        panel.Children.Add(new TextBlock
        {
            Text = "⋮⋮",
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 185, 185)),
            FontSize = 20,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 10, 0)
        });

        AddIconButton(panel, "□", "矩形", () => _tool = ToolKind.Rectangle);
        AddIconButton(panel, "↗", "箭头", () => _tool = ToolKind.Arrow);
        AddIconButton(panel, "✎", "画笔", () => _tool = ToolKind.Pen);
        AddIconButton(panel, "T", "文字", () => _tool = ToolKind.Text);
        AddSeparator(panel);
        AddIconButton(panel, "↶", "撤销", Undo);
        AddIconButton(panel, "⇩", "保存 PNG", SavePng);
        AddIconButton(panel, "⧉", "复制并粘贴", Confirm);
        AddIconButton(panel, "×", "关闭", Close);

        return new Border
        {
            Child = panel,
            Background = System.Windows.Media.Brushes.White,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(9, 7, 9, 7),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 18),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 2,
                Opacity = 0.24
            }
        };
    }

    private static void AddIconButton(WpfPanel panel, string icon, string tooltip, Action action)
    {
        var button = new WpfButton
        {
            Content = icon,
            ToolTip = tooltip,
            Width = 34,
            Height = 28,
            Margin = new Thickness(1, 0, 1, 0),
            Padding = new Thickness(0),
            FontSize = icon == "T" ? 18 : 20,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"),
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32)),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderBrush = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Focusable = false
        };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    private static void AddSeparator(WpfPanel panel)
    {
        panel.Children.Add(new Border
        {
            Width = 1,
            Height = 22,
            Margin = new Thickness(8, 3, 8, 3),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(222, 222, 222))
        });
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(_canvas);
        if (_tool == ToolKind.Text)
        {
            AddText(point);
            return;
        }

        _start = point;
        if (_tool == ToolKind.Pen)
        {
            _activeLine = new System.Windows.Shapes.Polyline { Stroke = System.Windows.Media.Brushes.Red, StrokeThickness = 3, StrokeLineJoin = PenLineJoin.Round };
            _activeLine.Points.Add(point);
            AddAnnotation(_activeLine);
        }
        else
        {
            _activeShape = _tool == ToolKind.Rectangle ? NewRectangle() : NewArrow(point, point);
            AddAnnotation(_activeShape);
        }
        _canvas.CaptureMouse();
    }

    private void OnCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_start is not Point start)
        {
            return;
        }

        var point = e.GetPosition(_canvas);
        if (_activeLine is not null)
        {
            _activeLine.Points.Add(point);
        }
        else if (_activeShape is System.Windows.Shapes.Rectangle rect)
        {
            Canvas.SetLeft(rect, Math.Min(start.X, point.X));
            Canvas.SetTop(rect, Math.Min(start.Y, point.Y));
            rect.Width = Math.Abs(start.X - point.X);
            rect.Height = Math.Abs(start.Y - point.Y);
        }
        else if (_activeShape is Canvas arrow)
        {
            UpdateArrow(arrow, start, point);
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        _canvas.ReleaseMouseCapture();
        _start = null;
        _activeShape = null;
        _activeLine = null;
    }

    private static System.Windows.Shapes.Rectangle NewRectangle() => new()
    {
        Stroke = System.Windows.Media.Brushes.Red,
        StrokeThickness = 3,
        Fill = System.Windows.Media.Brushes.Transparent
    };

    private static Canvas NewArrow(Point from, Point to)
    {
        var canvas = new Canvas();
        canvas.Children.Add(new System.Windows.Shapes.Line { Stroke = System.Windows.Media.Brushes.Red, StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        canvas.Children.Add(new System.Windows.Shapes.Polygon { Fill = System.Windows.Media.Brushes.Red });
        UpdateArrow(canvas, from, to);
        return canvas;
    }

    private static void UpdateArrow(Canvas canvas, Point from, Point to)
    {
        if (canvas.Children[0] is System.Windows.Shapes.Line line)
        {
            line.X1 = from.X;
            line.Y1 = from.Y;
            line.X2 = to.X;
            line.Y2 = to.Y;
        }
        if (canvas.Children[1] is System.Windows.Shapes.Polygon head)
        {
            var angle = Math.Atan2(to.Y - from.Y, to.X - from.X);
            const double length = 16;
            var p1 = new Point(to.X - length * Math.Cos(angle - Math.PI / 6), to.Y - length * Math.Sin(angle - Math.PI / 6));
            var p2 = new Point(to.X - length * Math.Cos(angle + Math.PI / 6), to.Y - length * Math.Sin(angle + Math.PI / 6));
            head.Points = new PointCollection { to, p1, p2 };
        }
    }

    private void AddText(Point point)
    {
        var input = new TextInputWindow { Owner = this };
        if (input.ShowDialog() != true || string.IsNullOrWhiteSpace(input.Value))
        {
            return;
        }

        var text = new TextBlock
        {
            Text = input.Value,
            Foreground = System.Windows.Media.Brushes.Red,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 255, 255, 255)),
            FontSize = 28,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(4)
        };
        Canvas.SetLeft(text, point.X);
        Canvas.SetTop(text, point.Y);
        AddAnnotation(text);
    }

    private void AddAnnotation(UIElement element)
    {
        _canvas.Children.Add(element);
        _annotations.Push(element);
    }

    private void Undo()
    {
        if (_annotations.TryPop(out var element))
        {
            _canvas.Children.Remove(element);
        }
    }

    private void SavePng()
    {
        Directory.CreateDirectory(AppSettings.CapturesDirectory);
        var path = IoPath.Combine(AppSettings.CapturesDirectory, $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        SaveRendered(path);
        Forms.MessageBox.Show($"已保存：\n{path}", "Codex 截图标注", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
    }

    private void Confirm()
    {
        ResultImage = RenderCanvas();
        DialogResult = true;
        Close();
    }

    private void OnEditorKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space)
        {
            Confirm();
            e.Handled = true;
        }
    }

    private void SaveRendered(string path)
    {
        var rendered = RenderCanvas();
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rendered));
        encoder.Save(stream);
    }

    private BitmapSource RenderCanvas()
    {
        _canvas.Measure(new Size(_canvas.Width, _canvas.Height));
        _canvas.Arrange(new Rect(new Size(_canvas.Width, _canvas.Height)));
        var target = new RenderTargetBitmap((int)_canvas.Width, (int)_canvas.Height, 96, 96, PixelFormats.Pbgra32);
        target.Render(_canvas);
        target.Freeze();
        return target;
    }
}

internal sealed class TextInputWindow : Window
{
    private readonly WpfTextBox _textBox = new();
    public string Value => _textBox.Text;

    public TextInputWindow()
    {
        Title = "输入标注文字";
        Width = 360;
        Height = 130;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var ok = new WpfButton { Content = "确定", Width = 80, Margin = new Thickness(6), IsDefault = true };
        ok.Click += (_, _) => { DialogResult = true; Close(); };
        var cancel = new WpfButton { Content = "取消", Width = 80, Margin = new Thickness(6), IsCancel = true };

        var buttons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var root = new DockPanel { Margin = new Thickness(10) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(_textBox);
        Content = root;
        Loaded += (_, _) => _textBox.Focus();
    }
}

internal static class WindowPaster
{
    private const uint InputKeyboard = 1;
    private const uint InputMouse = 0;
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint MouseEventFLeftDown = 0x0002;
    private const uint MouseEventFLeftUp = 0x0004;
    private const int SwRestore = 9;

    public static IntPtr GetForegroundWindowHandle() => GetForegroundWindow();

    public static string GetWindowTitleForDisplay(IntPtr hWnd)
    {
        var title = GetWindowTitle(hWnd);
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        GetWindowThreadProcessId(hWnd, out var pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return $"{process.ProcessName} ({pid})";
        }
        catch
        {
            return $"0x{hWnd.ToInt64():X}";
        }
    }

    public static bool PasteIntoBestWindow(string titlePart, IntPtr preferredWindow)
    {
        var target = IsUsableWindow(preferredWindow)
            ? preferredWindow
            : FindFirstMatchingWindow(titlePart);

        if (target == IntPtr.Zero)
        {
            return false;
        }

        ShowWindow(target, SwRestore);
        SetForegroundWindow(target);
        Thread.Sleep(450);
        ClickLikelyComposer(target);
        Thread.Sleep(150);
        SendCtrlV();
        return true;
    }

    private static IntPtr FindFirstMatchingWindow(string titlePart)
    {
        var titleTarget = IntPtr.Zero;
        var processTarget = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            if (IsTitleCandidate(hWnd, titlePart))
            {
                titleTarget = hWnd;
                return false;
            }

            if (processTarget == IntPtr.Zero && IsCodexProcessWindow(hWnd))
            {
                processTarget = hWnd;
            }

            return true;
        }, IntPtr.Zero);
        return titleTarget != IntPtr.Zero ? titleTarget : processTarget != IntPtr.Zero ? processTarget : FindByProcessMainWindow();
    }

    private static bool IsUsableWindow(IntPtr hWnd)
    {
        return hWnd != IntPtr.Zero && IsWindow(hWnd);
    }

    private static bool IsCandidateWindow(IntPtr hWnd, string titlePart)
    {
        if (!IsUsableWindow(hWnd) || !IsWindowVisible(hWnd))
        {
            return false;
        }
        return IsTitleCandidate(hWnd, titlePart) || IsCodexProcessWindow(hWnd);
    }

    private static bool IsTitleCandidate(IntPtr hWnd, string titlePart)
    {
        var title = GetWindowTitle(hWnd);
        return title.Contains(titlePart, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCodexProcessWindow(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out var pid);
        if (pid == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)pid);
            var name = process.ProcessName;
            return (name.Equals("Codex", StringComparison.OrdinalIgnoreCase) || name.Equals("codex", StringComparison.OrdinalIgnoreCase))
                && !name.Equals("CodexScreenshot", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("command-runner", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static IntPtr FindByProcessMainWindow()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = process.ProcessName;
                if ((name.Equals("Codex", StringComparison.OrdinalIgnoreCase) || name.Equals("codex", StringComparison.OrdinalIgnoreCase))
                    && !name.Contains("command-runner", StringComparison.OrdinalIgnoreCase)
                    && process.MainWindowHandle != IntPtr.Zero)
                {
                    return process.MainWindowHandle;
                }
            }
            catch
            {
                // Ignore processes that exit or deny metadata access while scanning.
            }
        }

        return IntPtr.Zero;
    }

    private static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyboardInput(VkControl, 0),
            KeyboardInput(VkV, 0),
            KeyboardInput(VkV, KeyEventFKeyUp),
            KeyboardInput(VkControl, KeyEventFKeyUp)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void ClickLikelyComposer(IntPtr hWnd)
    {
        if (!GetWindowRect(hWnd, out var rect))
        {
            return;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var x = rect.Left + width / 2;
        var y = rect.Top + Math.Max(60, height - 95);
        SetCursorPos(x, y);
        Thread.Sleep(80);
        SendMouseClick();
    }

    private static void SendMouseClick()
    {
        var inputs = new[]
        {
            MouseInput(MouseEventFLeftDown),
            MouseInput(MouseEventFLeftUp)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var length = GetWindowTextLength(hWnd);
        if (length <= 0)
        {
            return string.Empty;
        }
        var buffer = new char[length + 1];
        GetWindowText(hWnd, buffer, buffer.Length);
        return new string(buffer).TrimEnd('\0');
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private static INPUT KeyboardInput(ushort key, uint flags) => new()
    {
        Type = InputKeyboard,
        U = new InputUnion
        {
            Ki = new KEYBDINPUT
            {
                Vk = key,
                Scan = 0,
                Flags = flags,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            }
        }
    };

    private static INPUT MouseInput(uint flags) => new()
    {
        Type = InputMouse,
        U = new InputUnion
        {
            Mi = new MOUSEINPUT
            {
                Dx = 0,
                Dy = 0,
                MouseData = 0,
                Flags = flags,
                Time = 0,
                ExtraInfo = IntPtr.Zero
            }
        }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mi;

        [FieldOffset(0)]
        public KEYBDINPUT Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
