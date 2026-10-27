using System.Globalization;

namespace SunnyBar.Core;

public sealed record UpcomingDailyMarker(string Label, DateTimeOffset Instant, TimeSpan Remaining)
{
    public string RemainingText => Remaining.TotalMinutes < 1 ? "不足 1 分钟"
        : ((int)Math.Ceiling(Remaining.TotalMinutes) / 60, (int)Math.Ceiling(Remaining.TotalMinutes) % 60) switch
        {
            (0, var minutes) => $"{minutes} 分钟",
            (var hours, 0) => $"{hours} 小时",
            (var hours, var minutes) => $"{hours} 小时 {minutes} 分钟"
        };
}

public sealed class NextDailyMarkerService
{
    public UpcomingDailyMarker? Find(DateTimeOffset now, TimeZoneInfo zone, IReadOnlyList<DailyMarkerSettings> markers)
    {
        var days = new DisplayDayService(zone);
        var current = days.GetWindow(now);
        var next = days.GetWindow(current.End);
        return markers.Where(marker => marker.Enabled).SelectMany(marker =>
        {
            var time = TimeOnly.ParseExact(marker.Time, "HH:mm", CultureInfo.InvariantCulture);
            return new[] { days.ResolveDailyTime(current, time), days.ResolveDailyTime(next, time) }
                .Where(instant => instant > now)
                .Select(instant => new UpcomingDailyMarker(marker.Label, instant, instant - now));
        }).OrderBy(marker => marker.Instant).FirstOrDefault();
    }
}
