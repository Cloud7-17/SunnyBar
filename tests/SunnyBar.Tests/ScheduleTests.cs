using System.Text.Json;
using SunnyBar.Core;
using Xunit;

namespace SunnyBar.Tests;

public sealed class ScheduleTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 27, 4, 0, 0, TimeSpan.Zero);
    private static ScheduleEvent Event(double start, double end, string title = "写论文") => new() { Title = title, StartAt = Day.AddHours(start), EndAt = Day.AddHours(end) };
    private static ScheduleStore Store() => new(Path.Combine(AppContext.BaseDirectory, "schedule-tests", Guid.NewGuid().ToString("N")));

    [Fact]
    public void Event_interval_and_current_state_share_time_mapping()
    {
        var item = Event(10, 12);
        var state = new ScheduleService().Capture([item], new DisplayDayService(TimeZoneInfo.Utc).GetWindow(Day), Day.AddHours(11));
        var segment = Assert.Single(state.Segments);
        Assert.Equal(10d / 24, segment.StartProgress); Assert.Equal(12d / 24, segment.EndProgress);
        Assert.True(segment.IsCurrent); Assert.Equal(item.Id, Assert.Single(state.Current).Id); Assert.Null(state.Next);
    }
    [Fact]
    public void Cross_four_event_is_clipped_on_both_display_days()
    {
        var item = Event(-1, 1);
        var days = new DisplayDayService(TimeZoneInfo.Utc);
        var before = Assert.Single(new ScheduleService().Capture([item], days.GetWindow(Day.AddHours(-1)), Day.AddHours(-0.5)).Segments);
        var after = Assert.Single(new ScheduleService().Capture([item], days.GetWindow(Day), Day.AddMinutes(30)).Segments);
        Assert.Equal(23d / 24, before.StartProgress); Assert.Equal(1, before.EndProgress);
        Assert.Equal(0, after.StartProgress); Assert.Equal(1d / 24, after.EndProgress);
    }
    [Fact]
    public void Half_open_boundaries_omit_zero_width_and_end_current_on_time()
    {
        var window = new DisplayDayService(TimeZoneInfo.Utc).GetWindow(Day);
        var result = new ScheduleService().Capture([Event(-2,0),Event(24,25),Event(1,2),Event(2,3)],window,Day.AddHours(2));
        Assert.Equal(2,result.Segments.Count);
        Assert.Equal(Day.AddHours(2),Assert.Single(result.Current).StartAt);
    }
    [Fact]
    public void Overlap_uses_distinct_lanes_and_touching_intervals_reuse_a_lane()
    {
        var result = new ScheduleService().Capture([Event(6,8),Event(7,9),Event(8,10)],new DisplayDayService(TimeZoneInfo.Utc).GetWindow(Day),Day.AddHours(7.5));
        Assert.Equal(new[] {0,1,0},result.Segments.Select(item=>item.Lane)); Assert.Equal(2,result.Current.Count);
        Assert.Equal(Day.AddHours(8),result.Next!.StartAt);
    }
    [Fact]
    public void Multi_day_event_spans_entire_window_and_next_can_be_beyond_it()
    {
        var future=Event(50,51);
        var result=new ScheduleService().Capture([Event(-24,48),future],new DisplayDayService(TimeZoneInfo.Utc).GetWindow(Day),Day);
        var segment=Assert.Single(result.Segments); Assert.Equal(0,segment.StartProgress); Assert.Equal(1,segment.EndProgress); Assert.Equal(future.Id,result.Next!.Id);
    }
    [Theory]
    [InlineData(3,7,23)]
    [InlineData(10,31,25)]
    public void DST_intervals_use_real_elapsed_time(int month,int day,int expectedHours)
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var start=ScheduleTimeResolver.Resolve(new DateTime(2026,month,day),"04","00",zone);
        var window=new DisplayDayService(zone).GetWindow(start);
        var eventDate=start.Date.AddDays(1);
        var first=ScheduleTimeResolver.Resolve(eventDate,"01","30",zone);
        var end=month==3 ? ScheduleTimeResolver.Resolve(eventDate,"03","30",zone) : ScheduleTimeResolver.Resolve(eventDate,"01","30",zone,TimeSpan.FromHours(-5));
        var segment=Assert.Single(new ScheduleService().Capture([new(){Title="DST",StartAt=first,EndAt=end}],window,first).Segments);
        Assert.Equal(expectedHours,(window.End-window.Start).TotalHours);
        Assert.Equal(1d/expectedHours,segment.EndProgress-segment.StartProgress,10);
    }
    [Fact]
    public void Nonexistent_time_is_rejected_and_ambiguous_offset_preserved()
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Assert.Throws<FormatException>(()=>ScheduleTimeResolver.Resolve(new(2026,3,8),"02","30",zone));
        Assert.Equal(TimeSpan.FromHours(-4),ScheduleTimeResolver.Resolve(new(2026,11,1),"01","30",zone).Offset);
        Assert.Equal(TimeSpan.FromHours(-5),ScheduleTimeResolver.Resolve(new(2026,11,1),"01","30",zone,TimeSpan.FromHours(-5)).Offset);
    }
    [Fact]
    public void Empty_schedule_has_no_segments_current_or_next()
    {
        var result=new ScheduleService().Capture([],new DisplayDayService(TimeZoneInfo.Utc).GetWindow(Day),Day);
        Assert.Empty(result.Segments); Assert.Empty(result.Current); Assert.Null(result.Next);
    }
    [Fact]
    public void Missing_file_does_not_create_data_and_CRUD_survives_reload()
    {
        var store=Store(); Assert.Empty(store.Load().Document.Events); Assert.False(File.Exists(store.FilePath));
        var item=Event(10,12);
        store.Save(new(){Events=[item]});
        Assert.Equal(item,Assert.Single(store.Load().Document.Events));
        var updated=item with{Title="修改后",Notes="备注",Color="#AF8FDD"};
        store.Save(store.Load().Document with {Events=[updated]});
        Assert.Equal(updated,Assert.Single(store.Load().Document.Events));
        store.Save(new()); Assert.Empty(store.Load().Document.Events);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!,"*.tmp-*"));
    }
    [Theory]
    [InlineData("bad json")]
    [InlineData("null")]
    [InlineData("{\"events\":null}")]
    [InlineData("{\"events\":[null]}")]
    [InlineData("{\"events\":[{\"title\":\"bad\",\"startAt\":\"bad-date\"}]}")]
    public void Corrupt_events_are_backed_up_before_empty_recovery(string original)
    {
        var store=Store(); Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!); File.WriteAllText(store.FilePath,original);
        var result=store.Load(); Assert.True(result.CanSave); Assert.Contains("备份",result.Message); Assert.Empty(result.Document.Events);
        Assert.Equal(original,File.ReadAllText(Assert.Single(Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!,"events.corrupt-*.json"))));
    }
    [Fact]
    public void Future_schema_is_protected()
    {
        var store=Store(); Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!); const string original="{\"schemaVersion\":9,\"events\":[]}";
        File.WriteAllText(store.FilePath,original); Assert.False(store.Load().CanSave); Assert.Throws<IOException>(()=>store.Save(new())); Assert.Equal(original,File.ReadAllText(store.FilePath));
    }
    [Fact]
    public void Unknown_fields_and_explicit_offsets_roundtrip()
    {
        var store=Store();
        var document=JsonSerializer.Deserialize<ScheduleDocument>("""
          {"futureRoot":3,"Events":[{"Title":"Test","StartAt":"2026-11-01T01:00:00-04:00","EndAt":"2026-11-01T01:00:00-05:00","futureItem":true}]}
          """)!;
        store.Save(document); var restored=store.Load().Document;
        Assert.Equal(3,restored.Extra!["futureRoot"].GetInt32()); Assert.True(Assert.Single(restored.Events).Extra!["futureItem"].GetBoolean());
        Assert.Equal(TimeSpan.FromHours(1),restored.Events[0].EndAt-restored.Events[0].StartAt);
    }
    [Theory]
    [InlineData(0,0,"Test","#112233")]
    [InlineData(2,1,"Test","#112233")]
    [InlineData(1,2,"","#112233")]
    [InlineData(1,2,"Test","red")]
    public void Invalid_save_leaves_original_unchanged(double start,double end,string title,string color)
    {
        var store=Store(); store.Save(new(){Events=[Event(1,2)]}); var before=File.ReadAllText(store.FilePath);
        Assert.Throws<SettingsValidationException>(()=>store.Save(new(){Events=[Event(start,end,title) with{Color=color}]})); Assert.Equal(before,File.ReadAllText(store.FilePath));
    }
    [Fact]
    public void Duplicate_ids_and_overlong_notes_are_rejected()
    {
        var item=Event(1,2); Assert.NotEmpty(ScheduleValidator.Validate(new(){Events=[item,item]}));
        Assert.NotEmpty(ScheduleValidator.Validate(new(){Events=[item with{Notes=new string('x',2001)}]}));
    }
    [Fact]
    public void Read_failure_protects_file_and_settings_are_independent()
    {
        var store=Store(); store.Save(new(){Events=[Event(1,2)]});
        using var locked=new FileStream(store.FilePath,FileMode.Open,FileAccess.Read,FileShare.None);
        Assert.False(store.Load().CanSave); Assert.Throws<IOException>(()=>store.Save(new()));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(store.FilePath)!,"settings.json")));
    }
}
