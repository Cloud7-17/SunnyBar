using System.Windows;
using System.Windows.Interop;
using SunnyBar.App.Interop;
using SunnyBar.App.ViewModels;

namespace SunnyBar.App.Views;

public partial class HoverInfoWindow : Window
{
    public HoverInfoWindow(TimelineViewModel viewModel)
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
    }
}
