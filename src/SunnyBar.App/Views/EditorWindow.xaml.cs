using System.Windows;
using System.Windows.Controls;
using SunnyBar.App.ViewModels;

namespace SunnyBar.App.Views;

public partial class EditorWindow : Window
{
    public ScheduleEditorView ScheduleEditor { get; }
    public SettingsEditorView SettingsEditor { get; }
    public event EventHandler? PreviewRequested;
    public EditorWindow(ScheduleEditorViewModel schedules, SettingsEditorViewModel settings)
    {
        InitializeComponent();
        ScheduleContent.Content = ScheduleEditor = new(schedules);
        SettingsContent.Content = SettingsEditor = new(settings);
        ScheduleEditor.CloseRequested += OnCloseRequested;
        SettingsEditor.CloseRequested += OnCloseRequested;
        SettingsEditor.ScheduleRequested += (_, _) => SelectSchedule();
        SettingsEditor.PreviewRequested += (_, _) => PreviewRequested?.Invoke(this, EventArgs.Empty);
        SelectSchedule();
    }
    public void SelectSchedule() => Navigation.SelectedIndex = 0;
    public void SelectMarkers() => Navigation.SelectedIndex = 1;
    private void OnNavigationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != Navigation || Navigation.SelectedItem is not ListBoxItem { Tag: string tag } || SettingsEditor is null) return;
        var schedule = tag == "schedule";
        ScheduleContent.Visibility = schedule ? Visibility.Visible : Visibility.Collapsed;
        SettingsContent.Visibility = schedule ? Visibility.Collapsed : Visibility.Visible;
        if (!schedule) SettingsEditor.SelectSection(int.Parse(tag));
    }
    private void OnCloseRequested(object? sender, EventArgs e) => Close();
    protected override void OnClosed(EventArgs e) { ScheduleEditor.Dispose(); base.OnClosed(e); }
}
