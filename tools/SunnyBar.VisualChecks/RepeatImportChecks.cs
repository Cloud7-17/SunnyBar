using System.IO;
using System.Windows;
using SunnyBar.App.ViewModels;
using SunnyBar.App.Views;
using SunnyBar.Core;
internal static class RepeatImportChecks
{
    internal static void Run(string output)
    {
        var settings=new SettingsService(Path.Combine(output,"repeat-data",Guid.NewGuid().ToString("N")));
        var model=new TimelineViewModel(new Clock());model.ApplySettings(new(),settings);
        var store=new ScheduleStore(settings.DirectoryPath);
        using var editor=new ScheduleEditorViewModel(model,store);
        editor.New();editor.SetRepeat("workdays");
        if(!editor.Save() || store.Load().Document.Events[0].Repeat!.Days.Count!=5)throw new Exception("Weekday save failed");
        editor.Selected=null;editor.Selected=editor.Events[0];
        if(editor.RepeatDays.Count(day=>day.Enabled)!=5)throw new Exception("Weekday edit reload failed");
        var before=File.ReadAllText(store.FilePath);
        editor.PreviewImport(ScheduleImportWindow.FormatTemplate);
        if(File.ReadAllText(store.FilePath)!=before)throw new Exception("Preview wrote data");
        if(editor.Import("{}") || File.ReadAllText(store.FilePath)!=before)throw new Exception("Invalid import changed data");
        if(!editor.Import(ScheduleImportWindow.FormatTemplate) || store.Load().Document.Events.Count!=3)throw new Exception(editor.Status);
        var window=new ScheduleWindow(editor);Program.Render(window,1000,790,Path.Combine(output,"repeat-editor.png"));window.Close();
        var import=new ScheduleImportWindow(editor);Program.Render(import,780,620,Path.Combine(output,"import-editor.png"));import.Close();
        File.WriteAllText(Path.Combine(output,"repeat-import-result.txt"),"PASS: weekday save/reload, no-write preview, invalid import preservation, valid batch append/reload, editor and import rendering. Isolated data only.");
    }
    private sealed class Clock:IClock{public DateTimeOffset UtcNow=>new(2026,10,28,8,0,0,TimeSpan.Zero);}
}
