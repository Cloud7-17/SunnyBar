using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SunnyBar.App.ViewModels;

namespace SunnyBar.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsEditorViewModel editor;
    public event EventHandler? PreviewRequested;
    public event EventHandler? ScheduleRequested;
    public SettingsWindow(SettingsEditorViewModel editor)
    {
        this.editor = editor;
        InitializeComponent();
        DataContext = editor;
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
        if (dialog.ShowDialog(this) == true) { editor.ImagePath = dialog.FileName; editor.Notify(); }
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
        owner.AssignHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
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
    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
