namespace SunnyBar.Core;

public enum SolarEventKind { Sunrise, Sunset }
public enum SolarWindowState { Changing, DayOnly, NightOnly }

public sealed record SolarEvent(DateTimeOffset Instant, SolarEventKind Kind);
public sealed record SolarCalculation(bool InitiallyDaylight, IReadOnlyList<SolarEvent> Events);
public sealed record SolarMarker(DateTimeOffset Instant, SolarEventKind Kind, double Progress);
public sealed record DaylightStop(double Progress, double Daylight);
public sealed record SolarTimeline(
    SolarWindowState State,
    IReadOnlyList<SolarMarker> Markers,
    IReadOnlyList<DaylightStop> Stops,
    IReadOnlyList<DeepNightSegment>? DeepNight = null);

public interface ISolarEventCalculator
{
    /// <summary>Events in [start, end), ordered by instant. State is immediately before start.</summary>
    SolarCalculation Calculate(DateTimeOffset start, DateTimeOffset end, GeoLocation location);
}
