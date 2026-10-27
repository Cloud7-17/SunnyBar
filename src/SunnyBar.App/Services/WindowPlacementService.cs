using System.Runtime.InteropServices;
using System.Windows;
using SunnyBar.App.Interop;
using SunnyBar.Core;
using SunnyBar.App.ViewModels;

namespace SunnyBar.App.Services;

public sealed class WindowPlacementService(Window window, nint handle)
{
    public PixelRect Apply()
    {
        // The primary monitor includes virtual-screen origin (0,0).
        var monitor = NativeMethods.MonitorFromPoint(default, 1);
        var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
        NativeMethods.Require(NativeMethods.GetMonitorInfo(monitor, ref info), "无法读取主显示器工作区。");
        if (NativeMethods.MonitorFromWindow(handle, 1) != monitor)
            NativeMethods.Require(NativeMethods.SetWindowPos(handle, NativeMethods.TopMost, info.Work.Left,
                info.Work.Top, 0, 0, NativeMethods.NoSize | NativeMethods.NoActivatePosition), "无法移动到主显示器。");
        var dpi = NativeMethods.GetDpiForWindow(handle);
        if (dpi == 0) throw new InvalidOperationException("无法读取悬浮窗口 DPI。");
        var appearance = (window.DataContext as TimelineViewModel)?.Settings.Appearance;
        var bounds = OverlayLayoutCalculator.Calculate(new(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom), (int)dpi, appearance);
        window.Width = bounds.Width * 96.0 / dpi;
        window.Height = bounds.Height * 96.0 / dpi;
        NativeMethods.Require(NativeMethods.SetWindowPos(handle, NativeMethods.TopMost,
            bounds.Left, bounds.Top, bounds.Width, bounds.Height, NativeMethods.NoActivatePosition), "无法定位悬浮条。");
        return bounds;
    }
}
