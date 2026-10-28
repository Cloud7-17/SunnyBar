namespace SunnyBar.Core;

public sealed record ScheduleHoverInfo(string CurrentText, string NextText)
{
    public static ScheduleHoverInfo Capture(ScheduleState? state, DateTimeOffset now, TimeZoneInfo zone)
    {
        string Time(DateTimeOffset instant)
        {
            var local = TimeZoneInfo.ConvertTime(instant, zone);
            var today = TimeZoneInfo.ConvertTime(now, zone).Date;
            return local.ToString(local.Date == today ? "HH:mm zzz" : "MM-dd HH:mm zzz");
        }
        string Remaining(DateTimeOffset instant) => new UpcomingDailyMarker("", instant, instant - now).RemainingText;
        var current = state?.Current is { Count: > 0 } events
            ? string.Join("\n", events.Take(3).Select(item =>
                $"当前日程：{item.Title}\n{Time(item.StartAt)} → {Time(item.EndAt)} · 距结束 {Remaining(item.EndAt)}"))
                + (events.Count > 3 ? $"\n另有 {events.Count - 3} 项进行中，可在日程窗口查看。" : "")
            : "当前日程：暂无";
        var next = state?.Next is { } upcoming
            ? $"下一日程：{upcoming.Title}\n{Time(upcoming.StartAt)} · 距开始 {Remaining(upcoming.StartAt)}"
            : "下一日程：暂无";
        return new(current, next);
    }
}
