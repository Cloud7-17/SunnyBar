using System.Windows;
using SunnyBar.App.ViewModels;

namespace SunnyBar.App.Views;

public partial class ScheduleWindow : Window
{
    private readonly ScheduleEditorViewModel editor;
    public ScheduleWindow(ScheduleEditorViewModel editor) { this.editor = editor; InitializeComponent(); DataContext = editor; }
    private void OnNew(object sender, RoutedEventArgs e) => editor.New();
    private void OnSave(object sender, RoutedEventArgs e) => editor.Save();
    private void OnDelete(object sender, RoutedEventArgs e) => editor.Delete();
    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnChooseColor(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(editor.Color) };
        var owner = new System.Windows.Forms.NativeWindow(); owner.AssignHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        try { if (dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK) editor.Color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"; }
        finally { owner.ReleaseHandle(); }
    }
    protected override void OnClosed(EventArgs e) { editor.Dispose(); base.OnClosed(e); }
}
