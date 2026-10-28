using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SunnyBar.App.ViewModels;

namespace SunnyBar.App.Views;

public partial class SettingsEditorView : System.Windows.Controls.UserControl
{
    public event EventHandler? CloseRequested;
    private readonly SettingsEditorViewModel editor;
    private readonly Func<string, bool> confirmClear;
    public event EventHandler? PreviewRequested;
    public event EventHandler? ScheduleRequested;
    public SettingsEditorView(SettingsEditorViewModel editor, Func<string, bool>? confirmClear = null)
    {
        this.editor = editor;
        InitializeComponent();
        DataContext = editor;
        this.confirmClear = confirmClear ?? (message => MessageBox.Show(Window.GetWindow(this), message,
            "确认清除标记", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
    }
    private void OnClearMarkers(object sender, RoutedEventArgs e)
    {
        if (!editor.CanClearMarkers) return;
        if (!confirmClear($"清除全部每日标记？当前列表 {editor.Markers.Count} 项，已保存 {editor.SavedMarkerCount} 项。\n确认后立即保存，不能撤销；日程不受影响。")) return;
        MarkersGrid.CancelEdit(DataGridEditingUnit.Cell);
        MarkersGrid.CancelEdit(DataGridEditingUnit.Row);
        editor.ClearMarkers();
    }
    public void SelectSection(int index)
    {
        SettingsTabs.Template = (ControlTemplate)Resources["SectionOnlyTemplate"];
        SettingsTabs.SelectedIndex = index;
        PageTitle.Text = index == 3 ? "每日标记" : ((TabItem)SettingsTabs.Items[index]).Header.ToString();
        ManageSchedulesButton.Visibility = Visibility.Collapsed;
    }
    private void OnSave(object sender, RoutedEventArgs e)
    {
        MarkersGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        MarkersGrid.CommitEdit(DataGridEditingUnit.Row, true);
        editor.Apply();
    }
    private void OnChoosePng(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "PNG 图片|*.png", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) { editor.ImagePath = dialog.FileName; editor.Notify(); }
    }
    private void OnAddMarker(object sender, RoutedEventArgs e) => editor.AddMarker();
    private void OnAddPreset(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string index } && int.TryParse(index, out var preset)) editor.AddPreset(preset);
    }
    private void OnChooseMarkerColor(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MarkerRow marker }) return;
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(marker.Color) };
        var owner = new System.Windows.Forms.NativeWindow();
        owner.AssignHandle(new System.Windows.Interop.WindowInteropHelper(Window.GetWindow(this)).Handle);
        try
        {
            if (dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK)
                marker.Color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
        finally { owner.ReleaseHandle(); }
    }
    private void OnRemoveMarker(object sender, RoutedEventArgs e) => editor.RemoveMarker();
    private void OnPreview(object sender, RoutedEventArgs e) => PreviewRequested?.Invoke(this, EventArgs.Empty);
    private void OnSchedule(object sender, RoutedEventArgs e) => ScheduleRequested?.Invoke(this, EventArgs.Empty);
    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
