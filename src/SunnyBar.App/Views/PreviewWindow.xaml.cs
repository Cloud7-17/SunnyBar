using System.Windows;
using System.Windows.Threading;
using SunnyBar.App.ViewModels;
using SunnyBar.Core;

namespace SunnyBar.App.Views;

public partial class PreviewWindow : Window
{
    public event EventHandler? SettingsRequested;
    private readonly TimelineViewModel viewModel;
    private readonly DispatcherTimer? timer;

    public PreviewWindow() : this(new TimelineViewModel(new SystemClock()), manageRefresh: true) { }

    public PreviewWindow(TimelineViewModel viewModel, bool manageRefresh)
    {
        this.viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        if (manageRefresh)
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            timer.Tick += OnTick;
        }
        Loaded += OnLoaded;
        Activated += OnActivated;
        Closed += OnClosed;
    }

    private void Refresh()
    {
        // Re-read the OS zone so a system zone change is reflected on the next refresh.
        TimeZoneInfo.ClearCachedData();
        viewModel.Refresh(TimeZoneInfo.Local);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Refresh();
        timer?.Start();
    }

    private void OnTick(object? sender, EventArgs e) => Refresh();
    private void OnOpenSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    private void OnActivated(object? sender, EventArgs e) => Refresh();

    private void OnClosed(object? sender, EventArgs e)
    {
        timer?.Stop();
        if (timer is not null) timer.Tick -= OnTick;
        Loaded -= OnLoaded;
        Activated -= OnActivated;
        Closed -= OnClosed;
    }
}
