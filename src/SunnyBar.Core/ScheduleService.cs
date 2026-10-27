namespace SunnyBar.Core;

public sealed class ScheduleService
{
    public ScheduleState Capture(IReadOnlyList<ScheduleEvent> events, DisplayDayWindow window, DateTimeOffset now)
    {
        var mapper = new TimeAxisMapper();
        var segments = new List<ScheduleSegment>();
        var laneEnds = new List<DateTimeOffset>();
        foreach (var item in events.Where(item => item.StartAt < window.End && item.EndAt > window.Start)
                     .OrderBy(item => item.StartAt).ThenBy(item => item.EndAt).ThenBy(item => item.Id, StringComparer.Ordinal))
        {
            var start = item.StartAt < window.Start ? window.Start : item.StartAt;
            var end = item.EndAt > window.End ? window.End : item.EndAt;
            var lane = laneEnds.FindIndex(priorEnd => priorEnd <= start);
            if (lane < 0) { lane = laneEnds.Count; laneEnds.Add(end); }
            else laneEnds[lane] = end;
            segments.Add(new(item, mapper.Map(start, window), mapper.Map(end, window), item.StartAt <= now && now < item.EndAt, lane));
        }
        var current = events.Where(item => item.StartAt <= now && now < item.EndAt).OrderBy(item => item.EndAt).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var next = events.Where(item => item.StartAt > now).OrderBy(item => item.StartAt).ThenBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault();
        return new(segments.AsReadOnly(), Array.AsReadOnly(current), next);
    }
}

public static class ScheduleTimeResolver
{
    public static DateTimeOffset Resolve(DateTime date, string hour, string minute, TimeZoneInfo zone, TimeSpan? preferredOffset = null)
    {
        var time = TimeOnly.ParseExact(DailyMarkerTime.FromParts(hour, minute), "HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var local = DateTime.SpecifyKind(date.Date.Add(time.ToTimeSpan()), DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) throw new FormatException("此时刻在夏令时跳转中不存在，请选择其他时刻。");
        var offset = zone.GetUtcOffset(local);
        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            offset = preferredOffset is { } preferred && offsets.Contains(preferred) ? preferred : offsets.Max();
        }
        return new(local, offset);
    }
}
