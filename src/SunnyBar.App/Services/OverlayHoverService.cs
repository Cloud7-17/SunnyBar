using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SunnyBar.App.Interop;
using SunnyBar.App.ViewModels;
using SunnyBar.App.Views;

namespace SunnyBar.App.Services;

/// <summary>Observes hover without input hooks; the information popup remains click-through.</summary>
public sealed class OverlayHoverService : IDisposable
{
    private readonly BarWindow bar;
    private readonly TimelineViewModel viewModel;
    private readonly DispatcherTimer timer;
    private readonly Stopwatch dwell = new();
    private readonly Stopwatch freshness = new();
    private HoverInfoWindow? info;
    private bool disposed;

    public OverlayHoverService(BarWindow bar, TimelineViewModel viewModel)
    {
        this.bar = bar;
        this.viewModel = viewModel;
        timer = new DispatcherTimer(DispatcherPriority.Background, bar.Dispatcher) { Interval = TimeSpan.FromMilliseconds(120) };
        timer.Tick += OnTick;
        bar.IsVisibleChanged += OnVisibilityChanged;
        if (bar.IsVisible) timer.Start();
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (disposed) return;
        if (bar.IsVisible) timer.Start();
        else { timer.Stop(); HideInfo(); }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(bar).Handle;
        if (disposed || !bar.IsVisible || handle == 0 || !NativeMethods.GetCursorPos(out var cursor)
            || !NativeMethods.GetWindowRect(handle, out var bounds)
            || cursor.X < bounds.Left || cursor.X >= bounds.Right || cursor.Y < bounds.Top || cursor.Y >= bounds.Bottom)
        {
            HideInfo();
            return;
        }
        if (!dwell.IsRunning) dwell.Start();
        if (dwell.ElapsedMilliseconds < 450) return;
        if (!freshness.IsRunning || freshness.ElapsedMilliseconds >= 1000)
        {
            viewModel.Refresh(TimeZoneInfo.Local);
            freshness.Restart();
        }
        ShowInfo(handle, cursor, bounds);
    }

    private void ShowInfo(nint barHandle, NativeMethods.Point cursor, NativeMethods.Rect bounds)
    {
        var monitor = NativeMethods.MonitorFromWindow(barHandle, 1);
        var monitorInfo = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo)) { HideInfo(); return; }
        var dpi = NativeMethods.GetDpiForWindow(barHandle);
        if (dpi == 0) { HideInfo(); return; }
        var scale = dpi / 96.0;
        if (info is null)
        {
            info = new HoverInfoWindow(viewModel);
            info.Closed += OnInfoClosed;
        }
        info.MaxWidth = Math.Min(420, (monitorInfo.Work.Right - monitorInfo.Work.Left) / scale);
        if (!info.IsVisible)
        {
            info.Left = cursor.X / scale;
            info.Top = Math.Max(monitorInfo.Work.Top, bounds.Top - 180 * scale) / scale;
            info.Show();
            info.UpdateLayout();
        }
        var infoHandle = new WindowInteropHelper(info).Handle;
        if (!NativeMethods.GetWindowRect(infoHandle, out var rect)) { HideInfo(); return; }
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var x = Math.Clamp(cursor.X + (int)(12 * scale), monitorInfo.Work.Left,
            Math.Max(monitorInfo.Work.Left, monitorInfo.Work.Right - width));
        var y = Math.Max(monitorInfo.Work.Top, bounds.Top - height - (int)(8 * scale));
        if (!NativeMethods.SetWindowPos(infoHandle, NativeMethods.TopMost, x, y, width, height, NativeMethods.NoActivatePosition))
            HideInfo();
    }

    private void HideInfo()
    {
        dwell.Reset();
        freshness.Reset();
        info?.Hide();
    }

    private void OnInfoClosed(object? sender, EventArgs e) => info = null;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop();
        timer.Tick -= OnTick;
        bar.IsVisibleChanged -= OnVisibilityChanged;
        if (info is not null)
        {
            info.Closed -= OnInfoClosed;
            info.Close();
            info = null;
        }
    }
}
