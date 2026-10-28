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
    private readonly ScheduleStore scheduleStore;
    private string scheduleMessage;
    private EditorWindow? editorWindow;
    private string settingsMessage;
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
        scheduleStore = new ScheduleStore(settingsStore.DirectoryPath);
        var schedules = scheduleStore.Load(); scheduleMessage = schedules.Message;
        viewModel.ApplySchedule(schedules.Document);
        bar = new BarWindow(viewModel);
        tray = new TrayService(ToggleVisibility, OpenSettings, application.Shutdown, OpenSchedule);
        viewModel.PropertyChanged += OnTimelineChanged;
        OnTimelineChanged(this, new System.ComponentModel.PropertyChangedEventArgs(""));
        refresh = new TimelineRefreshService(viewModel, application.Dispatcher);
        hover = new OverlayHoverService(bar, viewModel);
        bar.EditRequested += OnEditRequested;
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
        if (scheduleMessage.Length > 0) OpenSchedule();
    }

    private void OnTimelineChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => tray.UpdateSchedule(viewModel.CurrentScheduleText, viewModel.NextScheduleText);
    public void OpenSchedule() => OpenEditor(false);
    public void OpenSettings() => OpenEditor(true);
    private void OnEditRequested(object? sender, EventArgs e) => OpenSchedule();
    private void OpenEditor(bool markers)
    {
        if (disposed) return;
        if (editorWindow is null)
        {
            editorWindow = new EditorWindow(new(viewModel, scheduleStore, scheduleMessage),
                new(viewModel, settingsStore, startup, settingsMessage + viewModel.IndicatorMessage, loaded.CanSave));
            scheduleMessage = "";
            editorWindow.Closed += OnEditorClosed;
            editorWindow.PreviewRequested += OnPreviewRequested;
        }
        if (markers) editorWindow.SelectMarkers(); else editorWindow.SelectSchedule();
        editorWindow.Show();
        if (editorWindow.WindowState == WindowState.Minimized) editorWindow.WindowState = WindowState.Normal;
        editorWindow.Activate();
    }
    private void OnEditorClosed(object? sender, EventArgs e)
    {
        if (editorWindow is not null)
        {
            editorWindow.Closed -= OnEditorClosed;
            editorWindow.PreviewRequested -= OnPreviewRequested;
        }
        editorWindow = null;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        settingsMessage = "";
        bar.Reposition();
    }
    private void OnPreviewRequested(object? sender, EventArgs e) => OpenPreview();
    private void OnSettingsRequested(object? sender, EventArgs e) => OpenSettings();
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
        viewModel.PropertyChanged -= OnTimelineChanged;
        if (editorWindow is not null)
        {
            editorWindow.Closed -= OnEditorClosed;
            editorWindow.PreviewRequested -= OnPreviewRequested;
            editorWindow.Close(); editorWindow = null;
        }
        bar.EditRequested -= OnEditRequested;
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
