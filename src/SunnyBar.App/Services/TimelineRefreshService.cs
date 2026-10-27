using System.Windows.Threading;
using Microsoft.Win32;
using SunnyBar.App.ViewModels;

namespace SunnyBar.App.Services;

public sealed class TimelineRefreshService : IDisposable
{
    private readonly TimelineViewModel viewModel;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer timer;
    private bool disposed;

    public TimelineRefreshService(TimelineViewModel viewModel, Dispatcher dispatcher)
    {
        this.viewModel = viewModel;
        this.dispatcher = dispatcher;
        timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(30) };
        timer.Tick += OnTick;
        SystemEvents.TimeChanged += OnTimeChanged;
        SystemEvents.PowerModeChanged += OnPowerChanged;
        Refresh();
        timer.Start();
    }

    public void Refresh()
    {
        if (disposed) return;
        TimeZoneInfo.ClearCachedData();
        viewModel.Refresh(TimeZoneInfo.Local);
    }

    private void QueueRefresh()
    {
        if (!disposed && !dispatcher.HasShutdownStarted)
            dispatcher.BeginInvoke(Refresh);
    }

    private void OnTick(object? sender, EventArgs e) => Refresh();
    private void OnTimeChanged(object? sender, EventArgs e) => QueueRefresh();
    private void OnPowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) QueueRefresh();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        timer.Stop();
        timer.Tick -= OnTick;
        SystemEvents.TimeChanged -= OnTimeChanged;
        SystemEvents.PowerModeChanged -= OnPowerChanged;
    }
}
