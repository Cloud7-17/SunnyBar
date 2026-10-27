namespace SunnyBar.Core;

public sealed class SolarEventService(ISolarEventCalculator calculator)
{
    public static TimeSpan TransitionHalfWidth { get; } = TimeSpan.FromMinutes(30);
    private readonly TimeAxisMapper mapper = new();

    public SolarTimeline Calculate(DisplayDayWindow window, GeoLocation location, TimeZoneInfo timeZone, TimeSpan? transitionHalfWidth = null)
    {
        var transition = transitionHalfWidth ?? TransitionHalfWidth;
        if (transition <= TimeSpan.Zero || transition > TimeSpan.FromMinutes(120))
            throw new ArgumentOutOfRangeException(nameof(transitionHalfWidth));
        // Events outside the visible interval still influence its edge colors.
        var result = calculator.Calculate(window.Start - transition, window.End + transition, location);
        var events = result.Events.OrderBy(e => e.Instant).ToArray();
        var markers = events.Where(e => e.Instant >= window.Start && e.Instant < window.End)
            .Select(e => new SolarMarker(TimeZoneInfo.ConvertTime(e.Instant, timeZone), e.Kind, mapper.Map(e.Instant, window)))
            .ToArray();
        var times = new SortedSet<DateTimeOffset> { window.Start, window.End };
        for (var instant = window.Start; instant < window.End; instant = instant.AddMinutes(5))
            times.Add(instant);
        foreach (var solarEvent in events)
            foreach (var offset in new[] { -transition, TimeSpan.Zero, transition })
            {
                var instant = solarEvent.Instant + offset;
                if (instant > window.Start && instant < window.End) times.Add(instant);
            }
        var stops = times.Select(instant => new DaylightStop(mapper.Map(instant, window), BlendAt(instant))).ToArray();
        var state = markers.Length > 0 ? SolarWindowState.Changing
            : stops.All(s => s.Daylight >= 1 - 1e-9) ? SolarWindowState.DayOnly
            : stops.All(s => s.Daylight <= 1e-9) ? SolarWindowState.NightOnly
            : SolarWindowState.Changing;
        return new SolarTimeline(state, Array.AsReadOnly(markers), Array.AsReadOnly(stops));

        double BlendAt(DateTimeOffset instant)
        {
            double daylight = result.InitiallyDaylight ? 1 : 0;
            foreach (var solarEvent in events)
            {
                var t = Math.Clamp((instant - solarEvent.Instant + transition).TotalSeconds
                    / (2 * transition.TotalSeconds), 0, 1);
                var smooth = t * t * (3 - 2 * t);
                daylight += solarEvent.Kind == SolarEventKind.Sunrise ? smooth : -smooth;
            }
            return Math.Clamp(daylight, 0, 1);
        }
    }
}
