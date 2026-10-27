using System.Windows;
using SunnyBar.App.ViewModels;
using SunnyBar.App.Views;
using SunnyBar.Core;

namespace SunnyBar.App.Services;

public sealed class DesktopRuntime : IDisposable
{
    private readonly Application application;
    private readonly TimelineViewModel viewModel = new(new SystemClock());
    private readonly BarWindow bar;
    private readonly TrayService tray;
    private readonly TimelineRefreshService refresh;
    private readonly OverlayHoverService hover;
    private readonly SettingsService settingsStore;
    private readonly IStartupRegistration startup;
    private readonly SettingsLoadResult loaded;
    private string settingsMessage;
    private SettingsWindow? settingsWindow;
    private PreviewWindow? preview;
    private bool disposed;
    private bool barClosed;

    public DesktopRuntime(Application application, SettingsService? store = null, IStartupRegistration? startupRegistration = null)
    {
        this.application = application;
        settingsStore = store ?? new SettingsService();
        loaded = settingsStore.Load();
        settingsMessage = loaded.Message;
        startup = startupRegistration ?? new StartupRegistrationService(settingsStore);
        viewModel.ApplySettings(loaded.Settings, settingsStore);
        bar = new BarWindow(viewModel);
        tray = new TrayService(ToggleVisibility, OpenSettings, application.Shutdown);
        refresh = new TimelineRefreshService(viewModel, application.Dispatcher);
        hover = new OverlayHoverService(bar, viewModel);
        bar.ShowRequested += OnShowRequested;
        bar.PlacementFailed += OnPlacementFailed;
        bar.IsVisibleChanged += OnVisibilityChanged;
        bar.Closed += OnBarClosed;
        viewModel.SettingsChanged += OnSettingsChanged;
        application.MainWindow = bar;
    }

    public void Start(bool showPreview = false)
    {
        bar.Show();
        bar.Reposition();
        if (showPreview) OpenPreview();
        else if (loaded.IsFirstLaunch || settingsMessage.Length > 0 || viewModel.IndicatorMessage.Length > 0) OpenSettings();
    }

    public void OpenSettings()
    {
        if (disposed) return;
        if (settingsWindow is null)
        {
            settingsWindow = new SettingsWindow(new SettingsEditorViewModel(viewModel, settingsStore, startup,
                settingsMessage + viewModel.IndicatorMessage, loaded.CanSave));
            settingsWindow.Closed += OnSettingsClosed;
            settingsWindow.PreviewRequested += OnPreviewRequested;
        }
        settingsWindow.Show();
        if (settingsWindow.WindowState == WindowState.Minimized) settingsWindow.WindowState = WindowState.Normal;
        settingsWindow.Activate();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        settingsMessage = "";
        bar.Reposition();
    }
    private void OnPreviewRequested(object? sender, EventArgs e) => OpenPreview();
    private void OnSettingsRequested(object? sender, EventArgs e) => OpenSettings();
    private void OnSettingsClosed(object? sender, EventArgs e)
    {
        if (settingsWindow is not null)
        {
            settingsWindow.Closed -= OnSettingsClosed;
            settingsWindow.PreviewRequested -= OnPreviewRequested;
        }
        settingsWindow = null;
    }

    public void OpenPreview()
    {
        if (disposed) return;
        if (preview is null)
        {
            preview = new PreviewWindow(viewModel, manageRefresh: false);
            preview.Closed += OnPreviewClosed;
            preview.SettingsRequested += OnSettingsRequested;
        }
        preview.Show();
        if (preview.WindowState == WindowState.Minimized) preview.WindowState = WindowState.Normal;
        preview.Activate();
    }

    public void ShowBar()
    {
        if (disposed) return;
        refresh.Refresh();
        bar.Show();
        bar.Reposition();
    }

    private void ToggleVisibility()
    {
        if (bar.IsVisible) bar.Hide();
        else ShowBar();
    }

    private void OnShowRequested(object? sender, bool openPreview)
    {
        ShowBar();
        if (openPreview) OpenPreview();
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => tray.UpdateVisibility(bar.IsVisible);
    private void OnPlacementFailed(object? sender, string message) => tray.ShowPlacementError(message);
    private void OnBarClosed(object? sender, EventArgs e)
    {
        barClosed = true;
        if (!disposed) application.Shutdown();
    }

    private void OnPreviewClosed(object? sender, EventArgs e)
    {
        if (preview is not null)
        {
            preview.Closed -= OnPreviewClosed;
            preview.SettingsRequested -= OnSettingsRequested;
        }
        preview = null;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        hover.Dispose();
        refresh.Dispose();
        tray.Dispose();
        viewModel.SettingsChanged -= OnSettingsChanged;
        if (settingsWindow is not null)
        {
            settingsWindow.Closed -= OnSettingsClosed;
            settingsWindow.PreviewRequested -= OnPreviewRequested;
            settingsWindow.Close();
            settingsWindow = null;
        }
        bar.ShowRequested -= OnShowRequested;
        bar.PlacementFailed -= OnPlacementFailed;
        bar.IsVisibleChanged -= OnVisibilityChanged;
        bar.Closed -= OnBarClosed;
        if (preview is not null)
        {
            preview.Closed -= OnPreviewClosed;
            preview.SettingsRequested -= OnSettingsRequested;
            preview.Close();
            preview = null;
        }
        if (!barClosed) bar.Close();
    }
}
