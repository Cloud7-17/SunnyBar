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
        if(editor.Events.Count!=0 || File.Exists(store.FilePath) || editor.Save()) throw new System.Exception("Opening an empty editor must not create a schedule");
        var window=new ScheduleWindow(editor);
        Program.Render(window,1000,790,Path.Combine(output,"schedule-empty.png"));
        Descendants((DependencyObject)window.Content).OfType<Button>().Single(button=>Equals(button.Content,"新建日程")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if(store.Load().Document.Events.Count!=1 || editor.Selected?.Title!="新日程") throw new System.Exception("New must persist immediately and select the event");
        editor.Title="写论文"; editor.StartDate=new(2026,10,27); editor.EndDate=new(2026,10,27);
        editor.StartHour="14"; editor.StartMinute="00"; editor.EndHour="17"; editor.EndMinute="00";
        if(!editor.Save()) throw new System.Exception(editor.Status);
        var firstId=editor.Selected!.Event.Id;
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
        CheckBand(model.Snapshot!, 0.5);
        CheckBand(model.Snapshot!, 0.4);
        CheckBand(model.Snapshot!, 0.25);
        var hover=new HoverInfoWindow(model) { Left=-10000, Top=-10000 };
        hover.Show(); hover.UpdateLayout();
        var hoverContent=(FrameworkElement)hover.Content;
        Program.Save(hoverContent,Brushes.Transparent,(int)System.Math.Ceiling(hoverContent.ActualWidth),(int)System.Math.Ceiling(hoverContent.ActualHeight),Path.Combine(output,"schedule-hover.png"));
        if(!model.HoverCurrentScheduleText.Contains("距结束") || !model.HoverNextScheduleText.Contains("距开始 2 小时")) throw new System.Exception("Hover bindings missing schedule countdowns");
        hover.Close();
        Program.Render(window,860,700,Path.Combine(output,"schedule-small.png"));
        var grid=(DataGrid)window.Editor.FindName("EventsGrid");
        grid.SetCurrentValue(DataGrid.SelectedItemProperty,editor.Events.First(row=>row.Event.Id==meetingId)); grid.GetBindingExpression(DataGrid.SelectedItemProperty)!.UpdateSource();
        if(editor.Title!="会谈" || !editor.Delete() || store.Load().Document.Events.Any(item=>item.Id==meetingId)) throw new System.Exception("Schedule row selection/delete failed");
        if(model.Snapshot!.Schedule!.Current.Count!=1) throw new System.Exception("Delete did not update timeline");
        if(store.Load().Document.Events.Count!=3) throw new System.Exception("Delete created an unwanted replacement");
        while(editor.Events.Count>0) { editor.Selected=editor.Events[0]; if(!editor.Delete()) throw new System.Exception(editor.Status); }
        if(editor.HasSelection || store.Load().Document.Events.Count!=0 || editor.Save()) throw new System.Exception("Empty editor allowed saving a new event");
        var protectedStore=new ScheduleStore(Path.Combine(settings.DirectoryPath,"future"));
        Directory.CreateDirectory(Path.GetDirectoryName(protectedStore.FilePath)!);
        File.WriteAllText(protectedStore.FilePath,"{\"schemaVersion\":99,\"events\":[]}");
        using(var protectedEditor=new ScheduleEditorViewModel(model,protectedStore,zone:zone))
        {
            var unchanged=File.ReadAllText(protectedStore.FilePath);
            protectedEditor.New();
            if(protectedEditor.Events.Count!=0 || File.ReadAllText(protectedStore.FilePath)!=unchanged) throw new System.Exception("New overwrote protected file");
        }
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
    private static void CheckBand(TimelineSnapshot snapshot, double ratio)
    {
        snapshot=snapshot with { DailyMarkers=[], Appearance=(snapshot.Appearance ?? new()) with { ScheduleHeightRatio=ratio } };
        byte[] Pixels(TimelineSnapshot state)
        {
            var control=new TimelineControl { Snapshot=state, IsCompact=true };
            control.Measure(new Size(1000,40)); control.Arrange(new Rect(0,0,1000,40)); control.UpdateLayout();
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(1000,40,96,96,PixelFormats.Pbgra32);
            bitmap.Render(control); var pixels=new byte[1000*40*4]; bitmap.CopyPixels(pixels,4000,0); return pixels;
        }
        var actual=Pixels(snapshot);
        var baseline=Pixels(snapshot with { Schedule=new([],[],null) });
        var limit=16+(snapshot.Appearance!.BarHeightDip*ratio);
        var endpoints=ScheduleService.Boundaries(snapshot.Schedule!,snapshot.Window)
            .Select(item=>new TimeAxisMapper().Map(item.Instant,snapshot.Window)*1000).ToArray();
        var changed=0;
        for(var y=0;y<40;y++) for(var x=0;x<4000;x++)
        {
            var index=y*4000+x;
            if(actual[index]==baseline[index])continue;
            changed++;
            if(y>=System.Math.Ceiling(limit) && !endpoints.Any(position=>System.Math.Abs(x/4-position)<=2))
                throw new System.Exception("Schedule fill escaped top band away from endpoint lines");
            if(y>=16+snapshot.Appearance.BarHeightDip) throw new System.Exception("Endpoint escaped the bar bottom");
        }
        if(changed==0) throw new System.Exception("Schedule band did not render");
        foreach(var position in endpoints)
        {
            var column=System.Math.Clamp((int)position,0,999);
            var index=((int)(16+snapshot.Appearance.BarHeightDip)-1)*4000+column*4;
            if(actual.Skip(index).Take(4).SequenceEqual(baseline.Skip(index).Take(4))) throw new System.Exception("Endpoint line does not reach the bottom of the bar");
        }
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) { var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested; }
    }
    private sealed class FixedClock : IClock { public System.DateTimeOffset UtcNow=>new(2026,10,27,8,0,0,System.TimeSpan.Zero); }
}
