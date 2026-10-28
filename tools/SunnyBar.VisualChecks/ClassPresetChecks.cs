using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunnyBar.App.ViewModels;
using SunnyBar.App.Views;
using SunnyBar.Core;

internal static class ClassPresetChecks
{
    internal static void Run(string output)
    {
        var store = new ScheduleStore(Path.Combine(output, "class-data", Guid.NewGuid().ToString("N")));
        var model = new TimelineViewModel(new Clock());
        var settings = new SettingsService(Path.GetDirectoryName(store.FilePath)!);
        model.ApplySettings(new(), settings);
        using var editor = new ScheduleEditorViewModel(model, store, zone: TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai"));
        var window = new ScheduleWindow(editor);
        Program.Render(window, 1000, 790, Path.Combine(output, "class-presets.png"));
        var root = window.Editor;
        var date = Descendants(root).OfType<DatePicker>().Single(control => control.GetBindingExpression(DatePicker.SelectedDateProperty)?.ParentBinding.Path.Path == "PresetDate");
        var times = Descendants(root).OfType<ComboBox>().Single(control => control.GetBindingExpression(ComboBox.SelectedItemProperty)?.ParentBinding.Path.Path == "SelectedClassPreset");
        var add = Descendants(root).OfType<Button>().Single(control => Equals(control.Content, "添加上课"));
        if (times.Items.Count != 7) throw new Exception("Missing class slots");
        var selectedDate = new DateTime(2026, 11, 5);
        date.SetCurrentValue(DatePicker.SelectedDateProperty, selectedDate); date.GetBindingExpression(DatePicker.SelectedDateProperty)!.UpdateSource();
        foreach (var preset in SchedulePresets.Classes)
        {
            times.SetCurrentValue(ComboBox.SelectedItemProperty, preset); times.GetBindingExpression(ComboBox.SelectedItemProperty)!.UpdateSource();
            add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var item = editor.Selected!.Event;
            if (item.Title != "上课" || item.StartAt.Date != selectedDate || TimeOnly.FromDateTime(item.StartAt.DateTime) != preset.Start || TimeOnly.FromDateTime(item.EndAt.DateTime) != preset.End)
                throw new Exception("Class date/time selection binding failed");
        }
        var loaded = store.Load().Document.Events;
        if (loaded.Count != 7 || loaded.Select(item => item.Color).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 7) throw new Exception("Class creation did not persist unique colors");
        var oldColors = loaded.Select(item => item.Color).ToHashSet(StringComparer.OrdinalIgnoreCase);
        editor.New();
        if (editor.Events.Count != 8 || oldColors.Contains(editor.Selected!.Event.Color)) throw new Exception("Ordinary new event reused an existing color");
        var originalColor = editor.Selected!.Event.Color;
        editor.Title = "修改标题";
        if (!editor.Save() || editor.Selected!.Event.Color != originalColor) throw new Exception("Editing unexpectedly recolored event");
        var before = File.ReadAllText(store.FilePath);
        editor.PresetDate = null;
        if (editor.NewClass() || File.ReadAllText(store.FilePath) != before || editor.Events.Count != 8) throw new Exception("Invalid class date changed events");
        editor.PresetDate = selectedDate;
        Program.Render(window, 1000, 790, Path.Combine(output, "class-presets.png"));
        Program.Render(window, 860, 760, Path.Combine(output, "class-small.png"));
        window.Close();
        var future = new ScheduleStore(Path.Combine(Path.GetDirectoryName(store.FilePath)!, "future"));
        Directory.CreateDirectory(Path.GetDirectoryName(future.FilePath)!);
        File.WriteAllText(future.FilePath, "{\"schemaVersion\":99,\"events\":[]}");
        using var readOnly = new ScheduleEditorViewModel(model, future);
        if (readOnly.NewClass() || !File.ReadAllText(future.FilePath).Contains("99")) throw new Exception("Preset wrote protected file");
        File.WriteAllText(Path.Combine(output, "class-result.txt"), "PASS: all seven class preset/date/slot bindings, actual add button, immediate persistence, unique colors for presets and ordinary new, edit color preservation, invalid date and read-only protection. All data isolated.");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var item in Descendants(child)) yield return item; }
    }
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 28, 8, 0, 0, TimeSpan.Zero); }
}
