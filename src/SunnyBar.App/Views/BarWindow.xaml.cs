using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using SunnyBar.App.Interop;
using SunnyBar.App.Services;
using SunnyBar.App.ViewModels;

namespace SunnyBar.App.Views;

public partial class BarWindow : Window
{
    private HwndSource? source;
    private WindowPlacementService? placement;
    private bool placementQueued;
    private bool closed;
    private readonly SunnyBar.Core.BarDoubleClickTracker clicks = new();
    private readonly uint showMessage = NativeMethods.RegisterWindowMessage(NativeMethods.ShowMessageName);
    public event EventHandler<bool>? ShowRequested;
    public event EventHandler<string>? PlacementFailed;
    public event EventHandler? EditRequested;

    public BarWindow(TimelineViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetExtendedStyle(handle);
        NativeMethods.SetExtendedStyle(handle, (style | NativeMethods.Layered
            | NativeMethods.ToolWindow | NativeMethods.NoActivate) & ~(NativeMethods.AppWindow | NativeMethods.Transparent));
        source = HwndSource.FromHwnd(handle);
        source?.AddHook(WindowMessage);
        placement = new WindowPlacementService(this, handle);
        Reposition();
    }

    public void Reposition()
    {
        if (closed || placement is null) return;
        try { placement.Apply(); }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or ArgumentException)
        {
            Hide();
            PlacementFailed?.Invoke(this, error.Message);
        }
    }

    private void QueuePlacement()
    {
        if (closed || placementQueued) return;
        placementQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            placementQueued = false;
            Reposition();
        });
    }

    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)message == showMessage && showMessage != 0)
        {
            ShowRequested?.Invoke(this, wParam != 0);
            handled = true;
        }
        switch (message)
        {
            case 0x0084: // Only the visible bar accepts input; the icon/empty area stays transparent.
                handled = true;
                if (NativeMethods.GetWindowRect(hwnd, out var bounds))
                {
                    var y = (short)((lParam.ToInt64() >> 16) & 0xffff);
                    var scale = NativeMethods.GetDpiForWindow(hwnd) / 96.0;
                    var height = ((TimelineViewModel)DataContext).Settings.Appearance.BarHeightDip;
                    if (y >= bounds.Top + 16 * scale && y < bounds.Top + (16 + height) * scale) return new nint(1);
                }
                return new nint(-1);
            case 0x0021: // WM_MOUSEACTIVATE
                handled = true;
                return new nint(3); // MA_NOACTIVATE: double-click explicitly activates the editor.
            case 0x0201: // WM_LBUTTONDOWN
            case 0x0203: // WM_LBUTTONDBLCLK, depending on the registered window class.
                var size = System.Windows.Forms.SystemInformation.DoubleClickSize;
                if (clicks.Register(Environment.TickCount64, (short)(lParam.ToInt64() & 0xffff),
                    (short)((lParam.ToInt64() >> 16) & 0xffff), System.Windows.Forms.SystemInformation.DoubleClickTime, size.Width, size.Height))
                    EditRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
                break;
            case 0x007e: // WM_DISPLAYCHANGE
            case 0x001a: // WM_SETTINGCHANGE (includes work-area/taskbar changes)
            case 0x02e0: // WM_DPICHANGED; let WPF process DPI, then anchor again.
                QueuePlacement();
                break;
        }
        return 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        closed = true;
        source?.RemoveHook(WindowMessage);
        source = null;
        base.OnClosed(e);
    }
}
