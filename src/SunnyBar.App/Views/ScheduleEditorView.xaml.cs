using System.Windows;
using SunnyBar.App.ViewModels;

namespace SunnyBar.App.Views;

public partial class ScheduleEditorView : System.Windows.Controls.UserControl, IDisposable
{
    public event EventHandler? CloseRequested;
    private readonly ScheduleEditorViewModel editor;
    private readonly Func<string, bool> confirmClear;
    public ScheduleEditorView(ScheduleEditorViewModel editor, Func<string, bool>? confirmClear = null)
    {
        this.editor = editor; InitializeComponent(); DataContext = editor;
        this.confirmClear = confirmClear ?? (message => MessageBox.Show(Window.GetWindow(this), message,
            "确认清除日程", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
    }
    private void OnClearAll(object sender, RoutedEventArgs e)
    {
        if (editor.CanClearAll && confirmClear($"清除全部 {editor.Events.Count} 条日程？包括过去和未来的日程。\n确认后立即保存，不能撤销；每日标记不受影响。")) editor.ClearAll();
    }
    private void OnNew(object sender, RoutedEventArgs e) => editor.New();
    private void OnRepeatPreset(object sender,RoutedEventArgs e)
    {
        if(sender is System.Windows.Controls.Button {Tag:string mode})
        {
            editor.SetRepeat(mode);
            // Refresh the lightweight weekday rows after a preset selection.
            System.Windows.Data.CollectionViewSource.GetDefaultView(editor.RepeatDays).Refresh();
        }
    }
    private void OnImport(object sender,RoutedEventArgs e) => new ScheduleImportWindow(editor){Owner=Window.GetWindow(this)}.ShowDialog();
    private void OnAddClass(object sender, RoutedEventArgs e) => editor.NewClass();
    private void OnSave(object sender, RoutedEventArgs e) => editor.Save();
    private void OnDelete(object sender, RoutedEventArgs e) => editor.Delete();
    private void OnClose(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private void OnChooseColor(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(editor.Color) };
        var owner = new System.Windows.Forms.NativeWindow(); owner.AssignHandle(new System.Windows.Interop.WindowInteropHelper(Window.GetWindow(this)).Handle);
        try { if (dialog.ShowDialog(owner) == System.Windows.Forms.DialogResult.OK) editor.Color = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}"; }
        finally { owner.ReleaseHandle(); }
    }
    public void Dispose() => editor.Dispose();
}
