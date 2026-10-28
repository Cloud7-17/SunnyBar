using System.Text.Json;
using System.Text.Json.Serialization;

namespace SunnyBar.Core;

public sealed record ScheduleEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "";
    public DateTimeOffset StartAt { get; init; }
    public DateTimeOffset EndAt { get; init; }
    public string Color { get; init; } = "#5AA9E6";
    public string? Notes { get; init; }
    public WeeklyRepeat? Repeat { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed record ScheduleDocument
{
    public int SchemaVersion { get; init; } = 1;
    public List<ScheduleEvent> Events { get; init; } = [];
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}

public sealed record ScheduleSegment(ScheduleEvent Event, double StartProgress, double EndProgress, bool IsCurrent, int Lane);
public sealed record ScheduleState(IReadOnlyList<ScheduleSegment> Segments, IReadOnlyList<ScheduleEvent> Current, ScheduleEvent? Next);

public static class ScheduleValidator
{
    public static IReadOnlyList<string> Validate(ScheduleDocument document)
    {
        var errors = new List<string>();
        if (document.SchemaVersion is not (1 or 2)) errors.Add("不支持此日程文件版本。");
        if (document.Events is null) { errors.Add("日程列表不能为空。"); return errors; }
        if (document.Events.Count > 1000) errors.Add("单次日程最多 1000 条，请先清理过往日程。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in document.Events)
        {
            if (item is null) { errors.Add("日程不能为空。"); continue; }
            if (string.IsNullOrWhiteSpace(item.Id) || item.Id.Length > 80 || !ids.Add(item.Id)) errors.Add("日程 ID 无效或重复。");
            if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 120) errors.Add("日程标题须为 1–120 个字符。");
            if (item.StartAt == DateTimeOffset.MinValue || item.EndAt <= item.StartAt) errors.Add("结束时刻必须晚于开始时刻。");
            if (!SettingsValidator.IsColor(item.Color)) errors.Add("日程颜色须为 #RRGGBB。");
            if (item.Notes?.Length > 2000) errors.Add("备注最多 2000 个字符。");
            if(item.Repeat is {} repeat)
            {
                if(document.SchemaVersion<2)errors.Add("重复日程需要格式版本 2。");
                if(repeat.Days is not {Count:>0 and <=7} || repeat.Days.Any(day=>day<1 || day>7) || repeat.Days.Distinct().Count()!=repeat.Days.Count)errors.Add("重复星期须为不重复的 1–7（周一至周日）。");
                try{TimeZoneInfo.FindSystemTimeZoneById(repeat.TimeZoneId);}catch(Exception e) when(e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException){errors.Add("重复时区无效。");}
                if(item.EndAt-item.StartAt>TimeSpan.FromHours(26))errors.Add("重复区间不能超过一天（含夏令时）。");
            }
        }
        return errors;
    }
}
