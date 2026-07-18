using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CatiaAiPanel.Core;

namespace CatiaAiPanel.App;

internal sealed class CatiaDockingService(Window panel) : IDisposable
{
    private const int SwHide = 0;
    private const int SwRestore = 9;
    private const int SwShowNoActivate = 4;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private IntPtr _catiaHandle;
    private WindowPlacement _originalPlacement;
    private bool _hasOriginalPlacement;
    private bool _panelHiddenWithCatia;

    public void Start()
    {
        _timer.Tick += Tick;
        _timer.Start();
        Tick(null, EventArgs.Empty);
    }

    private void Tick(object? sender, EventArgs e)
    {
        var handle = FindCatiaWindow();
        if (handle == IntPtr.Zero) return;
        if (handle != _catiaHandle) AttachTo(handle);

        var panelHandle = new WindowInteropHelper(panel).Handle;
        if (NativeMethods.IsIconic(handle))
        {
            if (!_panelHiddenWithCatia && panelHandle != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(panelHandle, SwHide);
                _panelHiddenWithCatia = true;
            }
            return;
        }
        if (_panelHiddenWithCatia && panelHandle != IntPtr.Zero)
        {
            NativeMethods.ShowWindow(panelHandle, SwShowNoActivate);
            _panelHiddenWithCatia = false;
        }

        var monitor = NativeMethods.MonitorFromWindow(handle, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;

        if (NativeMethods.IsZoomed(handle)) NativeMethods.ShowWindow(handle, SwRestore);

        var source = PresentationSource.FromVisual(panel);
        var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var toDevice = source?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        var desiredWidthDip = Math.Clamp(panel.ActualWidth > 0 ? panel.ActualWidth : panel.Width, panel.MinWidth, panel.MaxWidth);
        var desiredWidthPx = Math.Max(1, (int)Math.Round(toDevice.Transform(new Vector(desiredWidthDip, 0)).X));
        var minimumPanelPx = Math.Max(1, (int)Math.Round(toDevice.Transform(new Vector(panel.MinWidth, 0)).X));
        var maximumPanelPx = Math.Max(minimumPanelPx, (int)Math.Round(toDevice.Transform(new Vector(panel.MaxWidth, 0)).X));
        var minimumCatiaPx = Math.Max(1, (int)Math.Round(toDevice.Transform(new Vector(720, 0)).X));
        var layout = SideBySideLayout.Calculate(
            new(info.WorkArea.Left, info.WorkArea.Top, info.WorkArea.Right, info.WorkArea.Bottom),
            desiredWidthPx,
            minimumPanelPx,
            maximumPanelPx,
            minimumCatiaPx);

        if (!NativeMethods.GetWindowRect(handle, out var current) || !ApproximatelyEquals(current, layout.Catia))
        {
            NativeMethods.SetWindowPos(handle, IntPtr.Zero,
                layout.Catia.Left, layout.Catia.Top, layout.Catia.Width, layout.Catia.Height,
                SwpNoZOrder | SwpNoActivate);
        }

        var panelTopLeft = fromDevice.Transform(new Point(layout.Panel.Left, layout.Panel.Top));
        var panelBottomRight = fromDevice.Transform(new Point(layout.Panel.Right, layout.Panel.Bottom));
        panel.Left = panelTopLeft.X;
        panel.Top = panelTopLeft.Y;
        panel.Width = Math.Clamp(panelBottomRight.X - panelTopLeft.X, panel.MinWidth, panel.MaxWidth);
        panel.Height = Math.Max(panel.MinHeight, panelBottomRight.Y - panelTopLeft.Y);
    }

    private void AttachTo(IntPtr handle)
    {
        RestoreOriginalCatiaWindow();
        _catiaHandle = handle;
        _originalPlacement = new WindowPlacement { Length = Marshal.SizeOf<WindowPlacement>() };
        _hasOriginalPlacement = NativeMethods.GetWindowPlacement(handle, ref _originalPlacement);
    }

    private static bool ApproximatelyEquals(NativeRect current, PixelRectangle target) =>
        Math.Abs(current.Left - target.Left) <= 2 &&
        Math.Abs(current.Top - target.Top) <= 2 &&
        Math.Abs(current.Right - target.Right) <= 2 &&
        Math.Abs(current.Bottom - target.Bottom) <= 2;

    private static IntPtr FindCatiaWindow()
    {
        try
        {
            return Process.GetProcessesByName("CNEXT")
                .Select(process => process.MainWindowHandle)
                .FirstOrDefault(handle => handle != IntPtr.Zero);
        }
        catch { return IntPtr.Zero; }
    }

    private void RestoreOriginalCatiaWindow()
    {
        if (_catiaHandle == IntPtr.Zero || !_hasOriginalPlacement || !NativeMethods.IsWindow(_catiaHandle)) return;
        _originalPlacement.Length = Marshal.SizeOf<WindowPlacement>();
        NativeMethods.SetWindowPlacement(_catiaHandle, ref _originalPlacement);
        _hasOriginalPlacement = false;
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Tick;
        RestoreOriginalCatiaWindow();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public int ShowCommand;
        public NativePoint MinimumPosition;
        public NativePoint MaximumPosition;
        public NativeRect NormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hwnd, int command);
        [DllImport("user32.dll")] internal static extern bool GetWindowPlacement(IntPtr hwnd, ref WindowPlacement placement);
        [DllImport("user32.dll")] internal static extern bool SetWindowPlacement(IntPtr hwnd, ref WindowPlacement placement);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(
            IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    }
}
