namespace SunnyBar.Core;

public sealed record DeepNightSegment(DateTimeOffset Start, DateTimeOffset End, double StartProgress, double EndProgress);

public static class DeepNightService
{
    /// <summary>Night after calendar midnight, ending at the next sunrise; clips through the 04:00 display boundary.</summary>
    public static IReadOnlyList<DeepNightSegment> Calculate(DisplayDayWindow window, TimeZoneInfo zone,
        DateTimeOffset searchStart, SolarCalculation solar)
    {
        var days = new DisplayDayService(zone);
        var midnight = days.ResolveCalendarTime(window.Date, TimeOnly.MinValue);
        var nextMidnight = days.ResolveCalendarTime(window.Date.AddDays(1), TimeOnly.MinValue);
        var events = solar.Events.Where(item => item.Instant >= searchStart && item.Instant < window.End).OrderBy(item => item.Instant).ToArray();
        var boundaries = events.Select(item => item.Instant).Append(midnight).Append(nextMidnight)
            .Append(searchStart).Append(window.Start).Append(window.End)
            .Where(instant => instant >= searchStart && instant <= window.End).Distinct().Order().ToArray();
        var night = !solar.InitiallyDaylight;
        var afterMidnight = false;
        var result = new List<DeepNightSegment>();
        var mapper = new TimeAxisMapper();
        for (var i = 0; i < boundaries.Length - 1; i++)
        {
            var start = boundaries[i];
            if (start == midnight || start == nextMidnight) afterMidnight = true;
            foreach (var item in events.Where(item => item.Instant == start))
            {
                night = item.Kind == SolarEventKind.Sunset;
                if (!night) afterMidnight = false;
            }
            var end = boundaries[i + 1];
            if (!night || !afterMidnight || end <= window.Start || start >= window.End) continue;
            start = start < window.Start ? window.Start : start;
            end = end > window.End ? window.End : end;
            if (result.Count > 0 && result[^1].End == start)
                result[^1] = result[^1] with { End = end, EndProgress = mapper.Map(end, window) };
            else result.Add(new(start, end, mapper.Map(start, window), mapper.Map(end, window)));
        }
        return result.AsReadOnly();
    }
}
