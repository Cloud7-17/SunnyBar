using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunnyBar.App.Services;
using SunnyBar.App.ViewModels;
using SunnyBar.App.Views;
using SunnyBar.Core;

internal static class BulkClearChecks
{
    internal static void Run(string output)
    {
        var settings=new SettingsService(Path.Combine(output,"bulk-data",Guid.NewGuid().ToString("N")));
        var saved=new AppSettings { DailyMarkers=DailyMarkerPresets.All.Take(2).ToList(), StartWithWindows=true };
        settings.Save(saved);
        var model=new TimelineViewModel(new Clock()); model.ApplySettings(saved,settings);
        var store=new ScheduleStore(settings.DirectoryPath);
        var now=new Clock().UtcNow;
        store.Save(new(){ Events=[new(){Title="进行中",StartAt=now.AddHours(-1),EndAt=now.AddHours(1)},new(){Title="未来",StartAt=now.AddDays(1),EndAt=now.AddDays(1).AddHours(1)}] });
        using var schedules=new ScheduleEditorViewModel(model,store);
        var allow=false; var confirmations=0;
        var scheduleView=new ScheduleEditorView(schedules,_=>{confirmations++;return allow;});
        var scheduleWindow=new Window{Content=scheduleView};
        Program.Render(scheduleWindow,1000,790,Path.Combine(output,"bulk-schedules.png"));
        var clear=Find(scheduleView,"清除全部日程");
        var prior=File.ReadAllText(store.FilePath); var priorSettings=File.ReadAllText(settings.FilePath);
        clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(confirmations==1 && schedules.Events.Count==2 && File.ReadAllText(store.FilePath)==prior,"Cancelled schedule clear changed data");
        using(var locked=new FileStream(store.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            Require(!schedules.ClearAll() && schedules.Events.Count==2 && schedules.HasSelection,"Failed schedule write changed rows or selection");
        Require(File.ReadAllText(store.FilePath)==prior,"Failed clear changed events file");
        allow=true; clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(confirmations==2 && schedules.Events.Count==0 && !schedules.HasSelection && !schedules.CanClearAll,"Confirmed schedule clear did not reset editor");
        Require(store.Load().Document.Events.Count==0 && model.Schedule.Events.Count==0 && model.Snapshot!.Schedule!.Current.Count==0,"Cleared schedules survived reload or hover");
        Require(File.ReadAllText(settings.FilePath)==priorSettings,"Schedule clear changed markers or settings");
        Require(!schedules.ClearAll(),"Empty schedule clear should do nothing");

        var startup=new Startup();
        var editor=new SettingsEditorViewModel(model,settings,startup,"",true);
        editor.AddPreset(2); editor.Opacity="invalid draft";
        editor.SelectedMarker=editor.Markers[0];
        allow=false;
        var markersView=new SettingsEditorView(editor,_=>{confirmations++;return allow;}); markersView.SelectSection(3);
        var markersWindow=new Window{Content=markersView};
        Program.Render(markersWindow,1000,700,Path.Combine(output,"bulk-markers.png"));
        var clearMarkers=Find(markersView,"清除全部标记");
        clearMarkers.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(editor.Markers.Count==3 && File.ReadAllText(settings.FilePath)==priorSettings,"Cancelled marker clear changed data");
        using(var locked=new FileStream(settings.FilePath,FileMode.Open,FileAccess.Read,FileShare.Read))
            Require(!editor.ClearMarkers() && editor.Markers.Count==3 && editor.SelectedMarker is not null,"Failed marker write changed draft");
        Require(File.ReadAllText(settings.FilePath)==priorSettings,"Failed clear changed settings file");
        allow=true; clearMarkers.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var restored=settings.Load().Settings;
        Require(editor.Markers.Count==0 && editor.SelectedMarker is null && !editor.CanClearMarkers && restored.DailyMarkers.Count==0 && model.Settings.DailyMarkers.Count==0,"Confirmed marker clear did not persist/update");
        Require(restored.Appearance.Opacity==saved.Appearance.Opacity && restored.Location==saved.Location && restored.StartWithWindows && startup.Calls==0 && editor.Opacity=="invalid draft","Marker clear applied unrelated drafts or startup registration");
        Require(store.Load().Document.Events.Count==0 && !editor.ClearMarkers(),"Marker clear changed events or failed empty guard");

        // Removing all draft rows still allows clearing the saved marker list.
        editor.Opacity="0.92"; editor.AddPreset(0); Require(editor.Apply(),editor.Status);
        editor.Markers.Clear(); Require(editor.CanClearMarkers && editor.ClearMarkers(),"Saved markers could not be cleared after draft rows were removed");
        var readOnly=new SettingsEditorViewModel(model,settings,startup,"",false); readOnly.AddPreset(0);
        var unchanged=File.ReadAllText(settings.FilePath);
        Require(!readOnly.ClearMarkers() && File.ReadAllText(settings.FilePath)==unchanged,"Read-only marker clear wrote data");
        var future=new ScheduleStore(Path.Combine(settings.DirectoryPath,"future")); Directory.CreateDirectory(Path.GetDirectoryName(future.FilePath)!);
        File.WriteAllText(future.FilePath,"{\"schemaVersion\":99,\"events\":[]}");
        using var protectedEditor=new ScheduleEditorViewModel(model,future);
        Require(!protectedEditor.ClearAll() && File.ReadAllText(future.FilePath).Contains("99"),"Read-only schedule clear wrote data");
        scheduleWindow.Close(); markersWindow.Close();
        File.WriteAllText(Path.Combine(output,"bulk-result.txt"),"PASS: actual clear buttons with injected confirm/cancel, immediate save/reload/refresh, write-failure preservation, empty/read-only guards, unrelated drafts/settings/events and startup preservation. All data isolated.");
    }
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
    private static Button Find(DependencyObject root,string text)=>Descendants(root).OfType<Button>().Single(button=>Equals(button.Content,text));
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var descendant in Descendants(child))yield return descendant;}
    }
    private sealed class Clock:IClock { public DateTimeOffset UtcNow=>new(2026,10,28,8,0,0,TimeSpan.Zero); }
    private sealed class Startup:IStartupRegistration { public int Calls; public void Apply(bool enabled,Action persist){Calls++;persist();} }
}
