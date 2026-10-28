using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace SunnyBar.Core;

public sealed record WeeklyRepeat
{
    public List<int> Days { get; init; } = []; // Monday=1 ... Sunday=7
    public string TimeZoneId { get; init; } = TimeZoneInfo.Local.Id;
}
public static class ScheduleExpansion
{
    public static IReadOnlyList<ScheduleEvent> Expand(IReadOnlyList<ScheduleEvent> source, DisplayDayWindow window)
    {
        var result = new List<ScheduleEvent>();
        foreach(var item in source)
        {
            if(item.Repeat is not {} repeat){result.Add(item);continue;}
            var zone=TimeZoneInfo.FindSystemTimeZoneById(repeat.TimeZoneId);
            var originalStart=TimeZoneInfo.ConvertTime(item.StartAt,zone);
            var originalEnd=TimeZoneInfo.ConvertTime(item.EndAt,zone);
            var first=TimeZoneInfo.ConvertTime(window.Start,zone).Date.AddDays(-1);
            var last=TimeZoneInfo.ConvertTime(window.End,zone).Date.AddDays(8);
            if(originalStart.Date>last){first=originalStart.Date;last=first.AddDays(8);}
            for(var date=first;date<=last;date=date.AddDays(1))
            {
                if(date<originalStart.Date || !repeat.Days.Contains(((int)date.DayOfWeek+6)%7+1))continue;
                var endDate=date.AddDays((originalEnd.Date-originalStart.Date).Days);
                try
                {
                    var start=ScheduleTimeResolver.Resolve(date,originalStart.Hour.ToString(),originalStart.Minute.ToString(),zone);
                    var end=ScheduleTimeResolver.Resolve(endDate,originalEnd.Hour.ToString(),originalEnd.Minute.ToString(),zone);
                    if(end>start)result.Add(item with{StartAt=start,EndAt=end});
                }
                catch(FormatException){ } // Skip a nonexistent DST occurrence; do not move its wall time.
            }
        }
        return result;
    }
}
public static class ScheduleTextImport
{
    private sealed record Batch
    {
        public int Version {get;init;}
        public string TimeZone {get;init;}="";
        public List<Row>? Events {get;init;}
    }
    private sealed record Row
    {
        public string Title {get;init;}="";
        public string Date {get;init;}="";
        public string Start {get;init;}="";
        public string End {get;init;}="";
        public string? Color {get;init;}
        public string? Notes {get;init;}
        public List<int>? Days {get;init;}
    }
    public static IReadOnlyList<ScheduleEvent> Parse(string text,IEnumerable<string> existingColors)
    {
        if(text.Length>1024*1024)throw new FormatException("导入文本最多 1 MB。");
        var batch=JsonSerializer.Deserialize<Batch>(text,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow})??throw new FormatException("文本为空。");
        if(batch.Version!=1 || batch.Events is not {Count:>0 and <=1000})throw new FormatException("需要 version:1 和 1–1000 条 events。");
        var zone=TimeZoneInfo.FindSystemTimeZoneById(batch.TimeZone);
        var colors=existingColors.ToList();var result=new List<ScheduleEvent>();
        foreach(var row in batch.Events)
        {
            if(row is null)throw new FormatException("日程不能为空。");
            var date=DateTime.ParseExact(row.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture);
            var start=TimeOnly.ParseExact(row.Start,"HH:mm",CultureInfo.InvariantCulture);
            var end=TimeOnly.ParseExact(row.End,"HH:mm",CultureInfo.InvariantCulture);
            if(end==start)throw new FormatException("起止时间不能相同；全天请使用 00:00–23:59。");
            var item=new ScheduleEvent{Title=row.Title,Notes=row.Notes,Color=row.Color??ScheduleColors.Choose(colors),
                StartAt=ScheduleTimeResolver.Resolve(date,start.Hour.ToString(),start.Minute.ToString(),zone),
                EndAt=ScheduleTimeResolver.Resolve(end<start?date.AddDays(1):date,end.Hour.ToString(),end.Minute.ToString(),zone),
                Repeat=row.Days is null?null:new(){Days=row.Days,TimeZoneId=zone.Id}};
            result.Add(item);colors.Add(item.Color);
        }
        var errors=ScheduleValidator.Validate(new(){SchemaVersion=2,Events=result});
        if(errors.Count>0)throw new SettingsValidationException(errors);
        return result;
    }
}
