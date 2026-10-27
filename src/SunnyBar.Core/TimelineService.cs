namespace SunnyBar.Core;

public sealed class TimelineService(IClock clock, ISolarEventCalculator? solarCalculator = null)
{
    private readonly TimeAxisMapper mapper = new();
    private readonly SolarEventService solar = new(solarCalculator ?? new ApproximateSolarCalculator());
    private (DisplayDayWindow Window, GeoLocation Location, TimeZoneInfo Zone, int Transition, SolarTimeline Result)? cachedSolar;

    public TimelineSnapshot Capture(TimeZoneInfo timeZone, GeoLocation? location = null,
        IReadOnlyList<DailyMarkerSettings>? dailyMarkers = null, AppearanceSettings? appearance = null,
        IReadOnlyList<ScheduleEvent>? events = null)
    {
        var now = TimeZoneInfo.ConvertTime(clock.UtcNow, timeZone);
        var days = new DisplayDayService(timeZone);
        var window = days.GetWindow(now);
        appearance ??= new();
        var markers = (dailyMarkers ?? []).Where(marker => marker.Enabled).Select(marker =>
        {
            var instant = days.ResolveDailyTime(window, TimeOnly.ParseExact(marker.Time, "HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            return new TimelineDailyMarker(marker.Id, marker.Label, instant, marker.Color, mapper.Map(instant, window));
        }).OrderBy(marker => marker.Instant).ToArray();
        var ticks = new List<TimelineTick>();
        for (var step = 0; step <= 6; step++)
        {
            var time = new TimeOnly((4 + step * 4) % 24, 0);
            var instant = step == 6 ? window.End : days.ResolveDailyTime(window, time);
            var label = step == 6 ? "次日 04:00" : time.ToString("HH:mm");
            ticks.Add(new TimelineTick(instant, label, mapper.Map(instant, window)));
        }
        SolarTimeline? sunlight = null;
        var message = "未选择地点 · 使用中性色时间条";
        if (location is not null)
        {
            try
            {
                if (cachedSolar is not { } cached || cached.Window != window
                    || cached.Location != location || cached.Zone.Id != timeZone.Id || !cached.Zone.HasSameRules(timeZone)
                    || cached.Transition != appearance.TransitionMinutes)
                    cachedSolar = (window, location, timeZone, appearance.TransitionMinutes,
                        solar.Calculate(window, location, timeZone, TimeSpan.FromMinutes(appearance.TransitionMinutes)));
                sunlight = cachedSolar.Value.Result;
                message = sunlight.State switch
                {
                    SolarWindowState.DayOnly => "本展示窗口持续白昼 · 无日出日落事件",
                    SolarWindowState.NightOnly => "本展示窗口持续黑夜 · 无日出日落事件",
                    _ => $"离线天文估算 · 日出日落前后各 {appearance.TransitionMinutes} 分钟平滑过渡"
                };
            }
            catch (Exception error) when (error is ArgumentException or ArithmeticException or InvalidOperationException)
            {
                message = "日出日落计算失败 · 使用中性色，请重新选择地点或检查系统日期";
            }
        }
        return new TimelineSnapshot(window, now, timeZone.DisplayName, mapper.Map(now, window), ticks.AsReadOnly(), sunlight, message,
            Array.AsReadOnly(markers), appearance, new ScheduleService().Capture(events ?? [], window, now));
    }
}
