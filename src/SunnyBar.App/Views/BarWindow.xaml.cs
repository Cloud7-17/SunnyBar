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
    private readonly uint showMessage = NativeMethods.RegisterWindowMessage(NativeMethods.ShowMessageName);
    public event EventHandler<bool>? ShowRequested;
    public event EventHandler<string>? PlacementFailed;

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
        NativeMethods.SetExtendedStyle(handle, (style | NativeMethods.Layered | NativeMethods.Transparent
            | NativeMethods.ToolWindow | NativeMethods.NoActivate) & ~NativeMethods.AppWindow);
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
            case 0x0084: // WM_NCHITTEST; layered+transparent style handles other processes too.
                handled = true;
                return new nint(-1);
            case 0x0021: // WM_MOUSEACTIVATE
                handled = true;
                return new nint(3);
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
