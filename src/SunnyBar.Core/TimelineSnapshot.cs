namespace SunnyBar.Core;

public sealed record TimelineTick(DateTimeOffset Instant, string Label, double Progress);
public sealed record TimelineDailyMarker(string Id, string Label, DateTimeOffset Instant, string Color, double Progress);

public sealed record TimelineSnapshot(
    DisplayDayWindow Window,
    DateTimeOffset Now,
    string TimeZoneName,
    double Progress,
    IReadOnlyList<TimelineTick> Ticks,
    SolarTimeline? Solar = null,
    string SolarMessage = "未选择地点 · 使用中性色时间条",
    IReadOnlyList<TimelineDailyMarker>? DailyMarkers = null,
    AppearanceSettings? Appearance = null);
