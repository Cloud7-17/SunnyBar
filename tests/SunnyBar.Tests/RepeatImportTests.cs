using SunnyBar.Core;
using Xunit;
namespace SunnyBar.Tests;
public sealed class RepeatImportTests
{
    [Fact] public void Workdays_and_overnight_occurrences_are_projected_without_mutating_series()
    {
        var start=new DateTimeOffset(2026,10,26,23,0,0,TimeSpan.Zero);
        var item=new ScheduleEvent{Title="夜间",StartAt=start,EndAt=start.AddHours(3),Repeat=new(){Days=[1,2,3,4,5],TimeZoneId="UTC"}};
        var window=new DisplayDayService(TimeZoneInfo.Utc).GetWindow(new(2026,10,28,1,0,0,TimeSpan.Zero));
        var expanded=ScheduleExpansion.Expand([item],window);
        var state=new ScheduleService().Capture(expanded,window,new(2026,10,28,1,0,0,TimeSpan.Zero));
        Assert.Single(state.Current);Assert.Equal(27,state.Current[0].StartAt.Day);Assert.Equal(start,item.StartAt);
        Assert.DoesNotContain(expanded,e=>e.StartAt.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
    }
    [Fact] public void Future_series_still_has_next_occurrence()
    {
        var start=new DateTimeOffset(2027,1,1,8,0,0,TimeSpan.Zero);
        var item=new ScheduleEvent{Title="未来",StartAt=start,EndAt=start.AddHours(1),Repeat=new(){Days=[5],TimeZoneId="UTC"}};
        var window=new DisplayDayService(TimeZoneInfo.Utc).GetWindow(start.AddMonths(-2));
        Assert.Equal(start,ScheduleExpansion.Expand([item],window)[0].StartAt);
    }
    [Fact] public void Import_is_strict_and_cross_midnight_is_explicit()
    {
        var text="""{"version":1,"timeZone":"Asia/Shanghai","events":[{"title":"夜班","date":"2026-11-02","start":"23:00","end":"01:00","days":[1,3,5]}]}""";
        var item=Assert.Single(ScheduleTextImport.Parse(text,[]));Assert.Equal(3,item.EndAt.Day);Assert.Equal(3,item.Repeat!.Days.Count);
        Assert.ThrowsAny<Exception>(()=>ScheduleTextImport.Parse(text.Replace("[1,3,5]","[0,9]"),[]));
        Assert.ThrowsAny<Exception>(()=>ScheduleTextImport.Parse(text.Replace("\"title\"","\"titel\""),[]));
    }
    [Fact] public void Old_document_reads_and_v2_series_round_trips()
    {
        var store=new ScheduleStore(Path.Combine(AppContext.BaseDirectory,"repeat",Guid.NewGuid().ToString("N")));
        var items=ScheduleTextImport.Parse("""{"version":1,"timeZone":"UTC","events":[{"title":"每天","date":"2026-11-02","start":"08:00","end":"09:00","days":[1,2,3,4,5,6,7]}]}""",[]);
        store.Save(new(){SchemaVersion=2,Events=items.ToList()});Assert.Equal(7,store.Load().Document.Events[0].Repeat!.Days.Count);
    }
}
