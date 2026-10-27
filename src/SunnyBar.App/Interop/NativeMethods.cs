using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SunnyBar.App.Interop;

internal static class NativeMethods
{
    internal const int ExtendedStyleIndex = -20;
    internal const long Transparent = 0x20, ToolWindow = 0x80, AppWindow = 0x40000,
        Layered = 0x80000, NoActivate = 0x08000000;
    internal const uint NoActivatePosition = 0x10, NoSize = 0x1, NoZOrder = 0x4, FrameChanged = 0x20;
    internal static readonly nint TopMost = new(-1);
    internal static readonly nint Broadcast = new(0xffff);
    internal const string ShowMessageName = "SunnyBar.Desktop.Show.v1";

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct MonitorInfo
    {
        public int Size;
        public Rect Monitor, Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll")]
    internal static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint window, int index, nint value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint window, int index, int value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint icon);

    internal static long GetExtendedStyle(nint window) => nint.Size == 8
        ? GetWindowLongPtr64(window, ExtendedStyleIndex).ToInt64() : GetWindowLong32(window, ExtendedStyleIndex);

    internal static void SetExtendedStyle(nint window, long style)
    {
        Marshal.SetLastPInvokeError(0);
        var previous = nint.Size == 8
            ? SetWindowLongPtr64(window, ExtendedStyleIndex, new nint(style))
            : new nint(SetWindowLong32(window, ExtendedStyleIndex, (int)style));
        if (previous == 0 && Marshal.GetLastPInvokeError() != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "无法设置悬浮窗口样式。");
    }

    internal static void Require(bool success, string message)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastPInvokeError(), message);
    }
}
