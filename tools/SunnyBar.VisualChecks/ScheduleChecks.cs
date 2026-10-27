using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunnyBar.App.Controls;
using SunnyBar.App.ViewModels;
using SunnyBar.App.Views;
using SunnyBar.Core;

internal static class ScheduleChecks
{
    internal static void Run(TimelineViewModel model, SettingsService settings, string output)
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
        model.Refresh(zone);
        var store=new ScheduleStore(settings.DirectoryPath);
        using var editor=new ScheduleEditorViewModel(model,store,zone:zone);
        editor.Title="写论文"; editor.StartDate=new(2026,10,27); editor.EndDate=new(2026,10,27);
        editor.StartHour="14"; editor.StartMinute="00"; editor.EndHour="17"; editor.EndMinute="00";
        if(!editor.Save()) throw new System.Exception(editor.Status);
        var firstId=editor.Selected!.Event.Id;
        var window=new ScheduleWindow(editor);
        Program.Render(window,1000,790,Path.Combine(output,"schedule-editor.png"));
        var title=Descendants((DependencyObject)window.Content).OfType<TextBox>().First(control=>control.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path=="Title");
        title.SetCurrentValue(TextBox.TextProperty,"论文写作"); title.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        if(!editor.Save() || store.Load().Document.Events.Single().Title!="论文写作" || editor.Selected!.Event.Id!=firstId) throw new System.Exception("Schedule edit binding or identity preservation failed");
        var previous=File.ReadAllText(store.FilePath);
        editor.EndHour="13";
        if(editor.Save() || File.ReadAllText(store.FilePath)!=previous) throw new System.Exception("Invalid interval changed schedule file");
        editor.New(); editor.Title="会谈"; editor.Color="#AF8FDD"; editor.StartDate=new(2026,10,27); editor.EndDate=new(2026,10,27);
        editor.StartHour="15"; editor.EndHour="16"; editor.StartMinute="30"; editor.EndMinute="30";
        if(!editor.Save()) throw new System.Exception(editor.Status);
        var meetingId=editor.Selected!.Event.Id;
        editor.New(); editor.Title="晚间运动"; editor.Color="#44AA88"; editor.StartDate=new(2026,10,27); editor.EndDate=new(2026,10,27);
        editor.StartHour="18"; editor.StartMinute="00"; editor.EndHour="19"; editor.EndMinute="00";
        if(!editor.Save()) throw new System.Exception(editor.Status);
        editor.New(); editor.Title="跨日整理"; editor.Color="#EE8844"; editor.StartDate=new(2026,10,28); editor.EndDate=new(2026,10,28);
        editor.StartHour="03"; editor.StartMinute="00"; editor.EndHour="05"; editor.EndMinute="00";
        if(!editor.Save()) throw new System.Exception(editor.Status);
        model.Refresh(zone);
        if(model.Snapshot!.Schedule!.Current.Count!=2 || model.Snapshot.Schedule.Segments.Max(item=>item.Lane)!=1 || model.Snapshot.Schedule.Next!.Title!="晚间运动") throw new System.Exception("Current/next/overlap integration failed");
        var reloaded=new TimelineViewModel(new FixedClock());
        reloaded.ApplySettings(model.Settings,settings); reloaded.ApplySchedule(store.Load().Document); reloaded.Refresh(zone);
        if(reloaded.Schedule.Events.Count!=4 || reloaded.Snapshot!.Schedule!.Current.Count!=2) throw new System.Exception("Schedule restart reload failed");
        Program.Render(window,1000,790,Path.Combine(output,"schedule-editor.png"));
        var control=new TimelineControl{Snapshot=model.Snapshot}; control.Measure(new Size(1000,140)); control.Arrange(new Rect(0,0,1000,140)); control.UpdateLayout();
        Program.Save(control,Brushes.WhiteSmoke,1000,140,Path.Combine(output,"schedule-timeline.png"));
        var compact=new TimelineControl{Snapshot=model.Snapshot,IsCompact=true}; compact.Measure(new Size(1000,32)); compact.Arrange(new Rect(0,0,1000,32)); compact.UpdateLayout();
        Program.Save(compact,Brushes.WhiteSmoke,1000,32,Path.Combine(output,"schedule-compact.png"));
        Program.Render(window,860,700,Path.Combine(output,"schedule-small.png"));
        var grid=(DataGrid)window.FindName("EventsGrid");
        grid.SetCurrentValue(DataGrid.SelectedItemProperty,editor.Events.First(row=>row.Event.Id==meetingId)); grid.GetBindingExpression(DataGrid.SelectedItemProperty)!.UpdateSource();
        if(editor.Title!="会谈" || !editor.Delete() || store.Load().Document.Events.Any(item=>item.Id==meetingId)) throw new System.Exception("Schedule row selection/delete failed");
        if(model.Snapshot!.Schedule!.Current.Count!=1) throw new System.Exception("Delete did not update timeline");
        var preciseStore=new ScheduleStore(Path.Combine(settings.DirectoryPath,"precision"));
        var precise=new ScheduleEvent{Title="精度",StartAt=new System.DateTimeOffset(2026,11,1,1,30,35,System.TimeSpan.FromHours(-5)),EndAt=new System.DateTimeOffset(2026,11,1,2,30,45,System.TimeSpan.FromHours(-5))};
        preciseStore.Save(new(){Events=[precise]});
        using var precisionEditor=new ScheduleEditorViewModel(model,preciseStore,zone:System.TimeZoneInfo.Utc);
        precisionEditor.Selected=precisionEditor.Events.Single(); precisionEditor.Title="只改标题";
        if(!precisionEditor.Save()) throw new System.Exception(precisionEditor.Status);
        var preserved=preciseStore.Load().Document.Events.Single();
        if(preserved.StartAt.Ticks!=precise.StartAt.Ticks || preserved.StartAt.Offset!=precise.StartAt.Offset || preserved.EndAt.Ticks!=precise.EndAt.Ticks) throw new System.Exception("Editing in a different zone lost original offset/seconds");
        File.WriteAllText(Path.Combine(output,"schedule-result.txt"),"PASS: isolated WPF schedule creation, title binding edit, unchanged identity, invalid interval protection, overlapping lanes, current/next, cross-04 clipping, reload, row-selection and deletion. No user events changed.");
        window.Close();
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested; }
    }
    private sealed class FixedClock : IClock { public System.DateTimeOffset UtcNow=>new(2026,10,27,8,0,0,System.TimeSpan.Zero); }
}
