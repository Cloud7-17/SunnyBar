namespace SunnyBar.Core;

public sealed class DisplayDayService(TimeZoneInfo timeZone)
{
    public static TimeOnly DayStart { get; } = new(4, 0);

    public DisplayDayWindow GetWindow(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, timeZone);
        var date = DateOnly.FromDateTime(local.DateTime);
        if (TimeOnly.FromDateTime(local.DateTime) < DayStart)
            date = date.AddDays(-1);

        return new DisplayDayWindow(date, ResolveLocal(date, DayStart), ResolveLocal(date.AddDays(1), DayStart));
    }

    /// <summary>Resolves a daily wall-clock time within this service's time zone.</summary>
    public DateTimeOffset ResolveDailyTime(DisplayDayWindow window, TimeOnly time) =>
        ResolveLocal(time < DayStart ? window.Date.AddDays(1) : window.Date, time);

    private DateTimeOffset ResolveLocal(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        // A missing wall-clock minute advances to the first valid minute. No offset is invented.
        while (timeZone.IsInvalidTime(local))
            local = local.AddMinutes(1);

        // Daily labels have no offset; use the first occurrence of an ambiguous time.
        // Actual instants (including both occurrences of a repeated hour) retain their own offsets.
        var offset = timeZone.IsAmbiguousTime(local)
            ? timeZone.GetAmbiguousTimeOffsets(local).Max()
            : timeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
