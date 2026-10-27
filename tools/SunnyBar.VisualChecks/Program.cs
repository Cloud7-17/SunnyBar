using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunnyBar.App.Controls;
using SunnyBar.App.Services;
using SunnyBar.App.ViewModels;
using SunnyBar.App.Views;
using SunnyBar.Core;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var interactive = args.Contains("--interactive");
        var application = interactive ? new Application { ShutdownMode = ShutdownMode.OnLastWindowClose } : null;
        var output = Path.GetFullPath(args.FirstOrDefault(arg => arg != "--interactive") ?? "artifacts/visual-checks");
        Directory.CreateDirectory(output);
        var store = new SettingsService(Path.Combine(output, "test-data", Guid.NewGuid().ToString("N")));
        var model = new TimelineViewModel(new FixedClock());
        model.ApplySettings(new AppSettings { DailyMarkers = DailyMarkerPresets.All.ToList() }, store);
        model.Refresh(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"));
        var editor = new SettingsEditorViewModel(model, store, new NoStartup(), "演示数据 · 不修改用户配置", true);
        if (editor.City != 1) throw new Exception("Expected Beijing preset.");
        var window = new SettingsWindow(editor);
        var tabs = (TabControl)window.FindName("SettingsTabs");
        if (interactive)
        {
            tabs.SelectedIndex = 3;
            window.Title = "Sunny Bar — 发布交互测试";
            application!.Run(window);
            return;
        }
        Render(window, 820, 680, Path.Combine(output, "settings-location.png"));
        tabs.SelectedIndex = 3;
        Render(window, 820, 680, Path.Combine(output, "settings-markers.png"));
        var grid = (DataGrid)window.FindName("MarkersGrid");
        var combos = Descendants(grid).OfType<ComboBox>().ToArray();
        var hour = combos.First(control => control.GetBindingExpression(ComboBox.TextProperty)?.ParentBinding.Path.Path == "Hour");
        var minute = combos.First(control => control.GetBindingExpression(ComboBox.TextProperty)?.ParentBinding.Path.Path == "Minute");
        hour.SetCurrentValue(ComboBox.TextProperty, "9"); hour.GetBindingExpression(ComboBox.TextProperty)!.UpdateSource();
        minute.SetCurrentValue(ComboBox.TextProperty, "5"); minute.GetBindingExpression(ComboBox.TextProperty)!.UpdateSource();
        var palette = combos.First(control => control.GetBindingExpression(ComboBox.SelectedItemProperty)?.ParentBinding.Path.Path == "Color");
        palette.SetCurrentValue(ComboBox.SelectedItemProperty, "#E86677"); palette.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
        if (!editor.Apply()) throw new Exception(editor.Status);
        var restored = store.Load().Settings;
        if (restored.Location?.Latitude != 39.9042 || restored.Location.Longitude != 116.4074)
            throw new Exception("Opening and saving the editor changed coordinate precision.");
        if (restored.DailyMarkers[0].Time != "09:05" || restored.DailyMarkers[0].Color != "#E86677") throw new Exception("WPF bindings did not save.");
        var before = File.ReadAllText(store.FilePath);
        hour.SetCurrentValue(ComboBox.TextProperty, "24"); hour.GetBindingExpression(ComboBox.TextProperty)!.UpdateSource();
        if (editor.Apply() || File.ReadAllText(store.FilePath) != before) throw new Exception("Invalid hour overwrote configuration.");
        model.ApplySettings(new AppSettings { DailyMarkers = DailyMarkerPresets.All.ToList() }, store);
        model.Refresh(TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"));
        if (model.NextMarker?.Label != "晚饭" || model.RemainingText != "还有 2 小时") throw new Exception("Next marker/countdown wrong.");
        var timeline = new TimelineControl { Snapshot = model.Snapshot };
        timeline.Measure(new Size(1000, 140)); timeline.Arrange(new Rect(0, 0, 1000, 140)); timeline.UpdateLayout();
        Save(timeline, Brushes.WhiteSmoke, 1000, 140, Path.Combine(output, "timeline.png"));
        var hover = new HoverInfoWindow(model) { Left = -10000, Top = -10000 };
        hover.Show(); hover.UpdateLayout();
        var content = (FrameworkElement)hover.Content;
        Save(content, Brushes.Transparent, (int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), Path.Combine(output, "hover.png"));
        hover.Close(); window.Close();
        File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: Beijing default, real WPF hour/minute/palette bindings, save/reload, invalid-hour preservation, next marker, rendering. No user settings or real startup registration changed.");
        Console.WriteLine("PASS: isolated WPF binding and render checks. Output: " + output);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Render(Window window, int width, int height, string path)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
        Save(content, window.Background, width, height, path);
    }
    private static void Save(Visual content, Brush background, int width, int height, string path)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var backdrop = new DrawingVisual();
        using (var drawing = backdrop.RenderOpen()) drawing.DrawRectangle(background, null, new Rect(0, 0, width, height));
        bitmap.Render(backdrop); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero); }
    private sealed class NoStartup : IStartupRegistration { public void Apply(bool enabled, Action persistSettings) => persistSettings(); }
}
